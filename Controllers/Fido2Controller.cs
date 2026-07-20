using Authentication.Fido2.Common;
using Authentication.Fido2.DTOs.Fido2;
using Authentication.Fido2.Extensions;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Authentication.Fido2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class Fido2Controller : ApiControllerBase
{
    private readonly IFido2MfaService _fido2MfaService;
    private readonly IMfaTempTokenSessionRepository _mfaTempTokenSessionRepository;
    private readonly IAuditService _auditService;

    private readonly ILogger<Fido2Controller> _logger;

    public Fido2Controller(
        IFido2MfaService fido2MfaService,
        IMfaTempTokenSessionRepository mfaTempTokenSessionRepository,
        IAuditService auditService,
        ILogger<Fido2Controller> logger
    )
    {
        _fido2MfaService = fido2MfaService;
        _mfaTempTokenSessionRepository = mfaTempTokenSessionRepository;
        _auditService = auditService;

        _logger = logger;
    }

    [Authorize]
    [HttpPost("enrollments")]
    public async Task<IActionResult> CreateEnrollmentOptions(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(User, out var userId))
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.fido2.enrollment.options",
                "Warning",
                false,
                null,
                null,
                "Invalid token",
                null,
                cancellationToken
            );
            return UnauthorizedProblem("Invalid token.");
        }

        var response = await _fido2MfaService.CreateEnrollmentOptionsAsync(
            userId,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            Request.Headers.UserAgent.ToString(),
            cancellationToken
        );

        return ToActionResult(response);
    }

    [Authorize]
    [HttpPatch("enrollments/current")]
    public async Task<IActionResult> CompleteEnrollment(
        CompleteFido2EnrollmentRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetUserId(User, out var userId))
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.fido2.enrollment.complete",
                "Warning",
                false,
                null,
                null,
                "Invalid token",
                null,
                cancellationToken
            );

            return UnauthorizedProblem("Invalid token.");
        }

        var response = await _fido2MfaService.CompleteEnrollmentAsync(
            request,
            userId,
            cancellationToken
        );

        return ToActionResult(response);
    }

    [Authorize(AuthenticationSchemes = AuthenticationExtensions.MfaChallengeScheme)]
    [HttpPost("authentications")]
    public async Task<IActionResult> CreateLoginOptions(
        CreateFido2LoginOptionsRequest request,
        CancellationToken cancellationToken
    )
    {
        var mfaContextResult = await ValidateMfaTokenContext(cancellationToken);
        if (mfaContextResult.ErrorResult is not null)
        {
            return mfaContextResult.ErrorResult;
        }

        var response = await _fido2MfaService.CreateLoginOptionsAsync(
            mfaContextResult.UserId,
            mfaContextResult.MfaTransactionId,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            Request.Headers.UserAgent.ToString(),
            cancellationToken
        );

        return ToActionResult(response);
    }

    [Authorize(AuthenticationSchemes = AuthenticationExtensions.MfaChallengeScheme)]
    [HttpPatch("authentications/current")]
    public async Task<IActionResult> CompleteLogin(
        CompleteFido2LoginRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var mfaContextResult = await ValidateMfaTokenContext(cancellationToken);
            if (mfaContextResult.ErrorResult is not null)
            {
                return mfaContextResult.ErrorResult;
            }

            var response = await _fido2MfaService.CompleteLoginAsync(
                request,
                mfaContextResult.UserId,
                mfaContextResult.MfaTransactionId,
                cancellationToken
            );

            if (response.IsSuccess)
            {
                await _mfaTempTokenSessionRepository.ConsumeByTransactionAsync(
                    mfaContextResult.MfaTransactionId,
                    cancellationToken
                );
            }

            return ToActionResult(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized FIDO2 login attempt.");

            return UnauthorizedProblem("Invalid MFA token.");
        }
        // Generic exceptions handled by GlobalExceptionFilter
    }

    // NOTE: This method performs an additional DB session lookup (GetActiveByJtiAsync) to prevent
    // token replay attacks — Fido2Controller is the one that issues the final access token.
    // Do NOT merge with MfaController.ValidateMfaTokenContext — they have different security requirements.
    private async Task<(Guid UserId, Guid MfaTransactionId, IActionResult? ErrorResult)> ValidateMfaTokenContext(
        CancellationToken cancellationToken
    )
    {
        var hasUserId = TryGetUserId(User, out var userId);
        var tokenType = User.FindFirst("token_type")?.Value;
        var tokenTransactionId = User.FindFirst("mfa_tx")?.Value;
        var tokenJti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

        if (
            !hasUserId
            || !string.Equals(tokenType, "mfa", StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(tokenTransactionId, out var mfaTransactionId)
            || string.IsNullOrWhiteSpace(tokenJti)
        )
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.fido2.mfa_token_validation",
                "Warning",
                false,
                null,
                null,
                "Invalid MFA token",
                null,
                cancellationToken
            );

            return (Guid.Empty, Guid.Empty, UnauthorizedProblem("Invalid MFA token."));

        }

        var tokenSession = await _mfaTempTokenSessionRepository.GetActiveByJtiAsync(
            tokenJti,
            cancellationToken
        );

        if (tokenSession is null || tokenSession.UserId != userId || tokenSession.MfaTransactionId != mfaTransactionId)
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.fido2.mfa_token_validation",
                "Warning",
                false,
                userId,
                null,
                "MFA token expired or not valid",
                new { tokenJti, mfaTransactionId },
                cancellationToken
            );

            return (Guid.Empty, Guid.Empty, UnauthorizedProblem("MFA token is expired or not valid."));
        }

        return (userId, mfaTransactionId, null);
    }
}
