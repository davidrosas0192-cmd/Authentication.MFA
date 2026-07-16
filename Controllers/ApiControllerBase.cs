using Authentication.Fido2.Common;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Authentication.Fido2.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Seconds to advertise in the Retry-After header when returning 429.
    /// Override in subclasses to use a configured value.
    /// </summary>
    protected virtual int RetryAfterSeconds => 45;

    protected IActionResult ToActionResult<T>(Result<T> result)
    {
        var payload = result.ToResponsePayload();

        if (result.StatusCode.HasValue)
        {
            var statusCode = result.StatusCode.Value;
            if (statusCode >= StatusCodes.Status400BadRequest)
            {
                if (statusCode == StatusCodes.Status429TooManyRequests)
                {
                    Response.Headers["Retry-After"] = Math.Max(1, RetryAfterSeconds).ToString();
                }

                var problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = GetProblemTitle(statusCode),
                    Detail = result.Error ?? result.Message,
                };

                if (!string.IsNullOrWhiteSpace(result.Error))
                    problemDetails.Extensions["code"] = result.Error;

                return StatusCode(statusCode, problemDetails);
            }

            return StatusCode(statusCode, payload);
        }

        if (result.IsSuccess)
            return Ok(payload);

        var badRequest = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Detail = result.Error ?? result.Message,
        };

        if (!string.IsNullOrWhiteSpace(result.Error))
            badRequest.Extensions["code"] = result.Error;

        return BadRequest(badRequest);
    }

    protected IActionResult ToActionResult(Result result)
    {
        var payload = result.ToResponsePayload();

        if (result.StatusCode.HasValue)
        {
            var statusCode = result.StatusCode.Value;
            if (statusCode >= StatusCodes.Status400BadRequest)
            {
                if (statusCode == StatusCodes.Status429TooManyRequests)
                {
                    Response.Headers["Retry-After"] = Math.Max(1, RetryAfterSeconds).ToString();
                }

                var problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = GetProblemTitle(statusCode),
                    Detail = result.Error ?? result.Message,
                };

                if (!string.IsNullOrWhiteSpace(result.Error))
                    problemDetails.Extensions["code"] = result.Error;

                return StatusCode(statusCode, problemDetails);
            }

            return StatusCode(statusCode, payload);
        }

        if (result.IsSuccess)
            return Ok(payload);

        var badRequest = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Detail = result.Error ?? result.Message,
        };

        if (!string.IsNullOrWhiteSpace(result.Error))
            badRequest.Extensions["code"] = result.Error;

        return BadRequest(badRequest);
    }

    protected static bool TryGetUserId(ClaimsPrincipal user, out long userId)
    {
        var value = user.FindFirst("sub")?.Value
            ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return long.TryParse(value, out userId);
    }

    protected static IActionResult UnauthorizedProblem(string detail) =>
        new UnauthorizedObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = detail,
        });

    protected static string GetProblemTitle(int statusCode) => statusCode switch
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
