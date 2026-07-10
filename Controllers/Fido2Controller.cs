using Authentication.Fido2.Common;
using Authentication.Fido2.DTOs.Fido2;
using Authentication.Fido2.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authentication.Fido2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class Fido2Controller : ControllerBase
{
    private readonly IFido2MfaService _fido2MfaService;

    private readonly ILogger<Fido2Controller> _logger;

    public Fido2Controller(IFido2MfaService fido2MfaService, ILogger<Fido2Controller> logger)
    {
        _fido2MfaService = fido2MfaService;

        _logger = logger;
    }

    [Authorize]
    [HttpPost("enrollment/options")]
    public async Task<IActionResult> CreateEnrollmentOptions(CancellationToken cancellationToken)
    {
        try
        {
            var userIdValue = User.FindFirst("sub")?.Value;

            if (string.IsNullOrWhiteSpace(userIdValue))
            {
                return Unauthorized(new { message = "Invalid token." });
            }

            var userId = long.Parse(userIdValue);

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

    [HttpPost("login/options")]
    public async Task<IActionResult> CreateLoginOptions(
        CreateFido2LoginOptionsRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await _fido2MfaService.CreateLoginOptionsAsync(
                request,
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

    [HttpPost("login/complete")]
    public async Task<IActionResult> CompleteLogin(
        CompleteFido2LoginRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await _fido2MfaService.CompleteLoginAsync(request, cancellationToken);

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
        if (result.IsSuccess)
        {
            var payload = new
            {
                success = true,
                message = result.Message,
                data = result.Data,
            };

            return result.StatusCode.HasValue
                ? StatusCode(result.StatusCode.Value, payload)
                : Ok(payload);
        }

        var errorPayload = new { success = false, message = result.Error ?? result.Message };

        return result.StatusCode.HasValue
            ? StatusCode(result.StatusCode.Value, errorPayload)
            : BadRequest(errorPayload);
    }
}
