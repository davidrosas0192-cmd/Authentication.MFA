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
    private readonly IAuditService _auditService;

    private readonly ILogger<Fido2Controller> _logger;

    public Fido2Controller(
        IFido2MfaService fido2MfaService,
        IMfaTempTokenSessionRepository mfaTempTokenSessionRepository,
        IAuditService auditService,
        ILogger<Fido2Controller> logger
    )
    {
        _fido2MfaService = fido2MfaService;
        _mfaTempTokenSessionRepository = mfaTempTokenSessionRepository;
        _auditService = auditService;

        _logger = logger;
    }

    [Authorize]
    [HttpPost("enrollments")]
    public async Task<IActionResult> CreateEnrollmentOptions(CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetUserId(User, out var userId))
            {
                await _auditService.TrackSecurityEventAsync(
                    "Authentication",
                    "auth.fido2.enrollment.options",
                    "Warning",
                    false,
                    null,
                    null,
                    "Invalid token",
                    null,
                    cancellationToken
                );
                return UnauthorizedProblem("Invalid token.");
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

            return Problem("An error occurred while creating FIDO2 enrollment options.");
        }
    }

    [Authorize]
    [HttpPatch("enrollments/current")]
    public async Task<IActionResult> CompleteEnrollment(
        CompleteFido2EnrollmentRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (!TryGetUserId(User, out var userId))
            {
                await _auditService.TrackSecurityEventAsync(
                    "Authentication",
                    "auth.fido2.enrollment.complete",
                    "Warning",
                    false,
                    null,
                    null,
                    "Invalid token",
                    null,
                    cancellationToken
                );

                return UnauthorizedProblem("Invalid token.");
            }

            var response = await _fido2MfaService.CompleteEnrollmentAsync(
                request,
                userId,
                cancellationToken
            );

            return ToActionResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred completing FIDO2 enrollment.");

            return Problem("An error occurred while completing FIDO2 enrollment.");
        }
    }

    [Authorize(AuthenticationSchemes = AuthenticationExtensions.MfaScheme)]
    [HttpPost("authentications")]
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

            return Problem("An error occurred while creating FIDO2 login options.");
        }
    }

    [Authorize(AuthenticationSchemes = AuthenticationExtensions.MfaScheme)]
    [HttpPatch("authentications/current")]
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

            return UnauthorizedProblem("Invalid MFA token.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred completing FIDO2 login.");

            return Problem("An error occurred while completing FIDO2 login.");
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
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.fido2.mfa_token_validation",
                "Warning",
                false,
                null,
                null,
                "Invalid MFA token",
                null,
                cancellationToken
            );

            return (0, Guid.Empty, UnauthorizedProblem("Invalid MFA token."));

        }

        var tokenSession = await _mfaTempTokenSessionRepository.GetActiveByJtiAsync(
            tokenJti,
            cancellationToken
        );

        if (tokenSession is null || tokenSession.UserId != userId || tokenSession.MfaTransactionId != mfaTransactionId)
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.fido2.mfa_token_validation",
                "Warning",
                false,
                userId,
                null,
                "MFA token expired or not valid",
                new { tokenJti, mfaTransactionId },
                cancellationToken
            );

            return (0, Guid.Empty, UnauthorizedProblem("MFA token is expired or not valid."));
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
}
