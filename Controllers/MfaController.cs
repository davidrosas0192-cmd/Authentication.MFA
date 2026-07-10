using Authentication.Fido2.Common;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.DTOs.Mfa;
using Authentication.Fido2.Extensions;
using Authentication.Fido2.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Authentication.Fido2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MfaController : ControllerBase
{
    private readonly IMfaService _mfaService;
    private readonly IMfaTempTokenSessionRepository _mfaTempTokenSessionRepository;
    private readonly ILogger<MfaController> _logger;

    public MfaController(
        IMfaService mfaService,
        IMfaTempTokenSessionRepository mfaTempTokenSessionRepository,
        ILogger<MfaController> logger
    )
    {
        _mfaService = mfaService;
        _mfaTempTokenSessionRepository = mfaTempTokenSessionRepository;
        _logger = logger;
    }

    [Authorize]
    [HttpGet("methods")]
    public async Task<IActionResult> GetMethods(CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetUserId(User, out var userId))
            {
                return Unauthorized(new { message = "Invalid token." });
            }

            var methods = await _mfaService.GetAllowedMethodsAsync(userId, cancellationToken);

            return ToActionResult(
                Result<MfaMethodsResponse>.Success(new MfaMethodsResponse { AllowedMfaMethods = methods })
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred getting MFA methods.");
            return Problem("An error occurred while retrieving MFA methods.");
        }
    }

    [Authorize]
    [HttpGet("devices/available")]
    public async Task<IActionResult> GetDevicesAvailable(CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetUserId(User, out var userId))
            {
                return Unauthorized(new { message = "Invalid token." });
            }

            var allowedMethods = await _mfaService.GetAllowedMethodsAsync(userId, cancellationToken);
            var availableSetupOptions = await _mfaService.GetAvailableSetupMethodsAsync(
                userId,
                cancellationToken
            );

            return ToActionResult(
                Result<MfaDevicesAvailableResponse>.Success(
                    new MfaDevicesAvailableResponse
                    {
                        AllowedMfaMethods = allowedMethods,
                        AvailableMfaSetupOptions = availableSetupOptions,
                    }
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred getting MFA devices available.");
            return Problem("An error occurred while retrieving MFA devices available.");
        }
    }

    [HttpPost("challenges/start")]
    [Authorize(AuthenticationSchemes = AuthenticationExtensions.MfaScheme)]
    public async Task<IActionResult> StartChallenge(
        [FromBody] StartMfaChallengeRequest request,
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

            if (mfaContextResult.MfaTransactionId != request.MfaTransactionId)
            {
                return Unauthorized(new { message = "Invalid MFA transaction." });
            }

            var response = await _mfaService.StartChallengeAsync(
                mfaContextResult.UserId,
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                cancellationToken
            );

            return ToActionResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred starting MFA challenge.");
            return Problem("An error occurred while starting MFA challenge.");
        }
    }

    [HttpPost("challenges/verify")]
    [Authorize(AuthenticationSchemes = AuthenticationExtensions.MfaScheme)]
    public async Task<IActionResult> VerifyChallenge(
        [FromBody] VerifyMfaChallengeRequest request,
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

            if (mfaContextResult.MfaTransactionId != request.MfaTransactionId)
            {
                return Unauthorized(new { message = "Invalid MFA transaction." });
            }

            var response = await _mfaService.VerifyChallengeAsync(
                mfaContextResult.UserId,
                request,
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred verifying MFA challenge.");
            return Problem("An error occurred while verifying MFA challenge.");
        }
    }

    [Authorize]
    [HttpPost("enrollment/start")]
    public async Task<IActionResult> StartEnrollment(
        [FromBody] StartMfaEnrollmentRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (!TryGetUserId(User, out var userId))
            {
                return Unauthorized(new { message = "Invalid token." });
            }

            var response = await _mfaService.StartEnrollmentAsync(
                userId,
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                cancellationToken
            );

            return ToActionResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred starting MFA enrollment.");
            return Problem("An error occurred while starting MFA enrollment.");
        }
    }

    [Authorize]
    [HttpPost("enrollment/verify")]
    public async Task<IActionResult> VerifyEnrollment(
        [FromBody] VerifyMfaEnrollmentRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (!TryGetUserId(User, out var userId))
            {
                return Unauthorized(new { message = "Invalid token." });
            }

            var response = await _mfaService.VerifyEnrollmentAsync(
                userId,
                request,
                cancellationToken
            );

            return ToActionResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred verifying MFA enrollment.");
            return Problem("An error occurred while verifying MFA enrollment.");
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
