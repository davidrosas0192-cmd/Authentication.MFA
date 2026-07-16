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
public class AuthController : ApiControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly IAuthService _authService;

    public AuthController(ILogger<AuthController> logger, IAuthService authService)
    {
        _logger = logger;
        _authService = authService;
    }

    [HttpPost("/api/sessions")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken
    )
    {
        var response = await _authService.LoginAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers["User-Agent"].ToString(),
            cancellationToken
        );

        return ToActionResult(response);
    }

    [Authorize]
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

    [HttpPost("/api/sessions/refresh")]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        var response = await _authService.RefreshTokenAsync(
            request.RefreshToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers["User-Agent"].ToString(),
            cancellationToken
        );

        return ToActionResult(response);
    }

    [Authorize(AuthenticationSchemes = AuthenticationExtensions.MfaChallengeScheme)]
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

}
