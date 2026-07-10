using Authentication.Fido2.Common;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Authentication.Fido2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly IAuthService _authService;

    public AuthController(ILogger<AuthController> logger, IAuthService authService)
    {
        _logger = logger;
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await _authService.LoginAsync(
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers["User-Agent"].ToString(),
                cancellationToken
            );

            return ToActionResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during login.");
            return Problem("An error occurred during login. Please try again later.");
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
