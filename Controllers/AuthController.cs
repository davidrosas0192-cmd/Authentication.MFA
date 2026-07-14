using Authentication.Fido2.Common;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.Extensions;
using Authentication.Fido2.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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
    [HttpPost("/api/sessions")]
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

    [Authorize]
    [HttpPost("logout")]
    [HttpDelete("/api/sessions/current")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(User, out var userId))
        {
            return UnauthorizedProblem("Invalid token.");
        }

        var tokenJti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrWhiteSpace(tokenJti))
        {
            return UnauthorizedProblem("Invalid token.");
        }

        var response = await _authService.LogoutAsync(userId, tokenJti, cancellationToken);

        return ToActionResult(response);
    }

    [Authorize(AuthenticationSchemes = AuthenticationExtensions.MfaScheme)]
    [HttpPost("cancel-authentication")]
    [HttpDelete("/api/mfa/sessions/current")]
    public async Task<IActionResult> CancelAuthentication(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(User, out var userId))
        {
            return UnauthorizedProblem("Invalid token.");
        }

        var tokenJti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrWhiteSpace(tokenJti))
        {
            return UnauthorizedProblem("Invalid MFA token.");
        }

        var response = await _authService.CancelAuthenticationAsync(
            userId,
            tokenJti,
            cancellationToken
        );

        return ToActionResult(response);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        var payload = result.ToResponsePayload();

        if (result.StatusCode.HasValue)
        {
            var statusCode = result.StatusCode.Value;
            if (statusCode >= StatusCodes.Status400BadRequest)
            {
                var problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = GetProblemTitle(statusCode),
                    Detail = result.Error ?? result.Message,
                };

                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    problemDetails.Extensions["code"] = result.Error;
                }

                return StatusCode(statusCode, problemDetails);
            }

            return StatusCode(statusCode, payload);
        }

        if (result.IsSuccess)
        {
            return Ok(payload);
        }

        var badRequest = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Detail = result.Error ?? result.Message,
        };

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            badRequest.Extensions["code"] = result.Error;
        }

        return BadRequest(badRequest);
    }

    private IActionResult ToActionResult(Result result)
    {
        var payload = result.ToResponsePayload();

        if (result.StatusCode.HasValue)
        {
            var statusCode = result.StatusCode.Value;
            if (statusCode >= StatusCodes.Status400BadRequest)
            {
                var problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = GetProblemTitle(statusCode),
                    Detail = result.Error ?? result.Message,
                };

                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    problemDetails.Extensions["code"] = result.Error;
                }

                return StatusCode(statusCode, problemDetails);
            }

            return StatusCode(statusCode, payload);
        }

        if (result.IsSuccess)
        {
            return Ok(payload);
        }

        var badRequest = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Detail = result.Error ?? result.Message,
        };

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            badRequest.Extensions["code"] = result.Error;
        }

        return BadRequest(badRequest);
    }

    private static IActionResult UnauthorizedProblem(string detail)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = detail,
        };

        return new UnauthorizedObjectResult(problem);
    }

    private static string GetProblemTitle(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "Bad Request",
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "Not Found",
            StatusCodes.Status409Conflict => "Conflict",
            StatusCodes.Status410Gone => "Gone",
            StatusCodes.Status429TooManyRequests => "Too Many Requests",
            StatusCodes.Status503ServiceUnavailable => "Service Unavailable",
            _ => "Request Failed",
        };
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out long userId)
    {
        var userIdValue = user.FindFirst("sub")?.Value
            ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return long.TryParse(userIdValue, out userId);
    }
}
