using Authentication.Fido2.Common;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

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
}