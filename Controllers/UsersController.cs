using Authentication.Fido2.Common;
using Authentication.Fido2.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Authentication.Fido2.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Authentication.Fido2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserRegistrationService _userRegistrationService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserRegistrationService userRegistrationService,
        ILogger<UsersController> logger
    )
    {
        _userRegistrationService = userRegistrationService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await _userRegistrationService.CreateUserAsync(
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                cancellationToken
            );

            return ToActionResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred creating a user.");
            return Problem("An error occurred while creating the user.");
        }
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("/api/admin/users")]
    public async Task<IActionResult> CreateUserByAdmin(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (!TryGetUserId(User, out var actorUserId))
            {
                return Unauthorized(new { message = "Invalid token." });
            }

            var response = await _userRegistrationService.CreateUserByAdminAsync(
                actorUserId,
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                cancellationToken
            );

            return ToActionResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred creating a user by admin.");
            return Problem("An error occurred while creating the user.");
        }
    }

    [Authorize(Policy = "SupportOrAdmin")]
    [HttpGet("/api/admin/users")]
    public async Task<IActionResult> GetAdminUsers(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _userRegistrationService.GetUsersForAdminAsync(cancellationToken);
            return ToActionResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred reading users by admin/support.");
            return Problem("An error occurred while retrieving users.");
        }
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPatch("/api/admin/users/{userId:long}/role")]
    public async Task<IActionResult> UpdateUserRole(
        long userId,
        [FromBody] UpdateUserRoleRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (!TryGetUserId(User, out var actorUserId))
            {
                return Unauthorized(new { message = "Invalid token." });
            }

            var response = await _userRegistrationService.UpdateUserRoleAsync(
                actorUserId,
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
            _logger.LogError(ex, "An error occurred updating user role for {UserId}.", userId);
            return Problem("An error occurred while updating user role.");
        }
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPatch("/api/admin/users/{userId:long}/status")]
    public async Task<IActionResult> UpdateUserStatus(
        long userId,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (!TryGetUserId(User, out var actorUserId))
            {
                return Unauthorized(new { message = "Invalid token." });
            }

            var response = await _userRegistrationService.UpdateUserStatusAsync(
                actorUserId,
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
            _logger.LogError(ex, "An error occurred updating user status for {UserId}.", userId);
            return Problem("An error occurred while updating user status.");
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

    private static bool TryGetUserId(ClaimsPrincipal user, out long userId)
    {
        var userIdValue = user.FindFirst("sub")?.Value
            ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return long.TryParse(userIdValue, out userId);
    }
}