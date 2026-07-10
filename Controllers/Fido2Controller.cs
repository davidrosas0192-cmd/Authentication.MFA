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
public class Fido2Controller : ControllerBase
{
    private readonly IFido2MfaService _fido2MfaService;
    private readonly IMfaTempTokenSessionRepository _mfaTempTokenSessionRepository;

    private readonly ILogger<Fido2Controller> _logger;

    public Fido2Controller(
        IFido2MfaService fido2MfaService,
        IMfaTempTokenSessionRepository mfaTempTokenSessionRepository,
        ILogger<Fido2Controller> logger
    )
    {
        _fido2MfaService = fido2MfaService;
        _mfaTempTokenSessionRepository = mfaTempTokenSessionRepository;

        _logger = logger;
    }

    [Authorize]
    [HttpPost("enrollment/options")]
    [HttpPost("/api/fido2/enrollments")]
    public async Task<IActionResult> CreateEnrollmentOptions(CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetUserId(User, out var userId))
            {
                return Unauthorized(new { message = "Invalid token." });
            }

            var response = await _fido2MfaService.CreateEnrollmentOptionsAsync(
                userId,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                Request.Headers.UserAgent.ToString(),
                cancellationToken
            );

            return ToActionResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred creating FIDO2 enrollment options.");

            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("enrollment/complete")]
    [HttpPatch("/api/fido2/enrollments/current")]
    public async Task<IActionResult> CompleteEnrollment(
        CompleteFido2EnrollmentRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await _fido2MfaService.CompleteEnrollmentAsync(
                request,
                cancellationToken
            );

            return ToActionResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred completing FIDO2 enrollment.");

            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(AuthenticationSchemes = AuthenticationExtensions.MfaScheme)]
    [HttpPost("login/options")]
    [HttpPost("/api/fido2/authentications")]
    public async Task<IActionResult> CreateLoginOptions(
        CreateFido2LoginOptionsRequest request,
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

            var response = await _fido2MfaService.CreateLoginOptionsAsync(
                mfaContextResult.UserId,
                mfaContextResult.MfaTransactionId,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                Request.Headers.UserAgent.ToString(),
                cancellationToken
            );

            return ToActionResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred creating FIDO2 login options.");

            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(AuthenticationSchemes = AuthenticationExtensions.MfaScheme)]
    [HttpPost("login/complete")]
    [HttpPatch("/api/fido2/authentications/current")]
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

            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred completing FIDO2 login.");

            return BadRequest(new { message = ex.Message });
        }
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        var payload = result.ToResponsePayload();

        if (result.StatusCode.HasValue)
        {
            return StatusCode(result.StatusCode.Value, payload);
        }

        return result.IsSuccess ? Ok(payload) : BadRequest(payload);
    }

    private async Task<(long UserId, Guid MfaTransactionId, IActionResult? ErrorResult)> ValidateMfaTokenContext(
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
            return (0, Guid.Empty, Unauthorized(new { message = "Invalid MFA token." }));
        }

        var tokenSession = await _mfaTempTokenSessionRepository.GetActiveByJtiAsync(
            tokenJti,
            cancellationToken
        );

        if (tokenSession is null || tokenSession.UserId != userId || tokenSession.MfaTransactionId != mfaTransactionId)
        {
            return (0, Guid.Empty, Unauthorized(new { message = "MFA token is expired or not valid." }));
        }

        return (userId, mfaTransactionId, null);
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out long userId)
    {
        var userIdValue = user.FindFirst("sub")?.Value
            ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return long.TryParse(userIdValue, out userId);
    }
}
