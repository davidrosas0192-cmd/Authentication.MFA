using Authentication.Fido2.Common;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.DTOs.Mfa;
using Authentication.Fido2.Extensions;
using Authentication.Fido2.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authentication.Fido2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MfaController : ControllerBase
{
    private readonly IMfaService _mfaService;
    private readonly ILogger<MfaController> _logger;

    public MfaController(IMfaService mfaService, ILogger<MfaController> logger)
    {
        _mfaService = mfaService;
        _logger = logger;
    }

    [Authorize]
    [HttpGet("methods")]
    public async Task<IActionResult> GetMethods(CancellationToken cancellationToken)
    {
        try
        {
            var userIdValue = User.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(userIdValue) || !long.TryParse(userIdValue, out var userId))
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

    [HttpPost("challenges/start")]
    [Authorize(AuthenticationSchemes = AuthenticationExtensions.MfaScheme)]
    public async Task<IActionResult> StartChallenge(
        [FromBody] StartMfaChallengeRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var userIdValue = User.FindFirst("sub")?.Value;
            var tokenType = User.FindFirst("token_type")?.Value;
            var tokenTransactionId = User.FindFirst("mfa_tx")?.Value;

            if (
                !long.TryParse(userIdValue, out var userId)
                || !string.Equals(tokenType, "mfa", StringComparison.OrdinalIgnoreCase)
                || !Guid.TryParse(tokenTransactionId, out var transactionId)
                || transactionId != request.MfaTransactionId
            )
            {
                return Unauthorized(new { message = "Invalid MFA token." });
            }

            var response = await _mfaService.StartChallengeAsync(
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
            var userIdValue = User.FindFirst("sub")?.Value;
            var tokenType = User.FindFirst("token_type")?.Value;
            var tokenTransactionId = User.FindFirst("mfa_tx")?.Value;

            if (
                !long.TryParse(userIdValue, out var userId)
                || !string.Equals(tokenType, "mfa", StringComparison.OrdinalIgnoreCase)
                || !Guid.TryParse(tokenTransactionId, out var transactionId)
                || transactionId != request.MfaTransactionId
            )
            {
                return Unauthorized(new { message = "Invalid MFA token." });
            }

            var response = await _mfaService.VerifyChallengeAsync(
                userId,
                request,
                cancellationToken
            );

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
            var userIdValue = User.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(userIdValue) || !long.TryParse(userIdValue, out var userId))
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
            var userIdValue = User.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(userIdValue) || !long.TryParse(userIdValue, out var userId))
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
}
