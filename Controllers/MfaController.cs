using Authentication.Fido2.Common;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.DTOs.Mfa;
using Authentication.Fido2.Extensions;
using Authentication.Fido2.Options;
using Authentication.Fido2.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Authentication.Fido2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MfaController : ApiControllerBase
{
    private readonly IMfaService _mfaService;
    private readonly IMfaLoginEnrollmentSessionRepository _mfaLoginEnrollmentSessionRepository;
    private readonly IMfaTempTokenSessionRepository _mfaTempTokenSessionRepository;
    private readonly IAuditService _auditService;
    private readonly ILogger<MfaController> _logger;
    private readonly MfaApiPolicyOptions _mfaApiPolicyOptions;

    [ActivatorUtilitiesConstructor]
    public MfaController(
        IMfaService mfaService,
        IMfaLoginEnrollmentSessionRepository mfaLoginEnrollmentSessionRepository,
        IMfaTempTokenSessionRepository mfaTempTokenSessionRepository,
        IAuditService auditService,
        ILogger<MfaController> logger,
        IOptions<MfaApiPolicyOptions>? mfaApiPolicyOptions = null
    )
    {
        _mfaService = mfaService;
        _mfaLoginEnrollmentSessionRepository = mfaLoginEnrollmentSessionRepository;
        _mfaTempTokenSessionRepository = mfaTempTokenSessionRepository;
        _auditService = auditService;
        _logger = logger;
        _mfaApiPolicyOptions = mfaApiPolicyOptions?.Value ?? new MfaApiPolicyOptions();
    }

    public MfaController(
        IMfaService mfaService,
        IMfaTempTokenSessionRepository mfaTempTokenSessionRepository,
        IAuditService auditService,
        ILogger<MfaController> logger,
        IOptions<MfaApiPolicyOptions>? mfaApiPolicyOptions = null
    )
        : this(
            mfaService,
            new NullMfaLoginEnrollmentSessionRepository(),
            mfaTempTokenSessionRepository,
            auditService,
            logger,
            mfaApiPolicyOptions
        )
    {
    }

    protected override int RetryAfterSeconds =>
        Math.Max(1, _mfaApiPolicyOptions.RetryAfterSecondsOnTooManyRequests);

    [Authorize]
    [HttpGet("methods")]
    public async Task<IActionResult> GetMethods(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(User, out var userId))
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.mfa.methods.read",
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

        var methods = await _mfaService.GetAllowedMethodsAsync(userId, cancellationToken);

        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.mfa.methods.read",
            "Information",
            true,
            userId,
            null,
            null,
            new { allowedCount = methods.Count },
            cancellationToken
        );

        return ToActionResult(
            Result<MfaMethodsResponse>.Success(new MfaMethodsResponse { AllowedMfaMethods = methods })
        );
    }

    [Authorize]
    [HttpGet("setup-options")]
    public async Task<IActionResult> GetDevicesAvailable(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(User, out var userId))
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.mfa.devices.available",
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

    [HttpPost("challenges")]
    [Authorize(AuthenticationSchemes = AuthenticationExtensions.MfaChallengeScheme)]
    public async Task<IActionResult> StartChallenge(        [FromBody] StartMfaChallengeRequest request,        CancellationToken cancellationToken    )
    {
        var mfaContextResult = await ValidateMfaTokenContext(cancellationToken);
        if (mfaContextResult.ErrorResult is not null)
        {
            return mfaContextResult.ErrorResult;
        }

        var response = await _mfaService.StartChallengeAsync(
            mfaContextResult.UserId,
            mfaContextResult.MfaTransactionId,
            request.Method,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken
        );

        return ToActionResult(response);
    }

    [HttpPatch("challenges/current")]
    [Authorize(AuthenticationSchemes = AuthenticationExtensions.MfaChallengeScheme)]
    public async Task<IActionResult> VerifyChallenge(        [FromBody] VerifyMfaChallengeRequest request,        CancellationToken cancellationToken    )
    {
        var mfaContextResult = await ValidateMfaTokenContext(cancellationToken);
        if (mfaContextResult.ErrorResult is not null)
        {
            return mfaContextResult.ErrorResult;
        }

        var response = await _mfaService.VerifyChallengeAsync(
            mfaContextResult.UserId,
            mfaContextResult.MfaTransactionId,
            request.ContinuationToken,
            request.Code,
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

    [Authorize]
    [HttpPost("enrollments")]
    public async Task<IActionResult> StartEnrollment(
        [FromBody] StartMfaEnrollmentRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetUserId(User, out var userId))
        {
            return UnauthorizedProblem("Invalid token.");
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

    [Authorize(AuthenticationSchemes = AuthenticationExtensions.LoginEnrollmentScheme)]
    [HttpPost("login-enrollments")]
    public async Task<IActionResult> StartLoginEnrollment(        [FromBody] StartLoginEnrollmentRequest request,        CancellationToken cancellationToken    )
    {
        var enrollmentContextResult = await ValidateLoginEnrollmentTokenContext(cancellationToken);
        if (enrollmentContextResult.ErrorResult is not null)
        {
            return enrollmentContextResult.ErrorResult;
        }

        var response = await _mfaService.StartLoginEnrollmentAsync(
            enrollmentContextResult.UserId,
            enrollmentContextResult.EnrollmentSessionId,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken
        );

        return ToActionResult(response);
    }

    [Authorize(AuthenticationSchemes = AuthenticationExtensions.LoginEnrollmentScheme)]
    [HttpPatch("login-enrollments/current")]
    public async Task<IActionResult> VerifyLoginEnrollment(
        [FromBody] VerifyLoginEnrollmentRequest request,
        CancellationToken cancellationToken
    )
    {
        var enrollmentContextResult = await ValidateLoginEnrollmentTokenContext(cancellationToken);
        if (enrollmentContextResult.ErrorResult is not null)
        {
            return enrollmentContextResult.ErrorResult;
        }

        var response = await _mfaService.VerifyLoginEnrollmentAsync(
            enrollmentContextResult.UserId,
            enrollmentContextResult.EnrollmentSessionId,
            request,
            cancellationToken
        );

        return ToActionResult(response);
    }

    [Authorize(AuthenticationSchemes = AuthenticationExtensions.LoginEnrollmentScheme)]
    [HttpPost("login-enrollment-sessions/complete")]
    public async Task<IActionResult> CompleteLoginEnrollmentSession(        [FromBody] CompleteLoginEnrollmentSessionRequest request,        CancellationToken cancellationToken    )
    {
        var enrollmentContextResult = await ValidateLoginEnrollmentTokenContext(cancellationToken);
        if (enrollmentContextResult.ErrorResult is not null)
        {
            return enrollmentContextResult.ErrorResult;
        }

        var response = await _mfaService.CompleteLoginEnrollmentSessionAsync(
            enrollmentContextResult.UserId,
            request.EnrollmentSessionId,
            request.ContinuationToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken
        );

        return ToActionResult(response);
    }

    [Authorize]
    [HttpPatch("enrollments/current")]
    public async Task<IActionResult> VerifyEnrollment(
        [FromBody] VerifyMfaEnrollmentRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetUserId(User, out var userId))
        {
            return UnauthorizedProblem("Invalid token.");
        }

        var response = await _mfaService.VerifyEnrollmentAsync(
            userId,
            request,
            cancellationToken
        );

        return ToActionResult(response);
    }

    [Authorize]
    [HttpPost("management-sessions")]
    public async Task<IActionResult> StartManagementSession(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(User, out var userId))
        {
            return UnauthorizedProblem("Invalid token.");
        }

        var response = await _mfaService.StartManagementSessionAsync(
            userId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken
        );

        return ToActionResult(response);
    }

    [Authorize]
    [HttpPost("management-sessions/challenges/start")]
    public async Task<IActionResult> StartManagementChallenge(        [FromBody] StartMfaManagementChallengeRequest request,        CancellationToken cancellationToken    )
    {
        if (!TryGetUserId(User, out var userId))
        {
            return UnauthorizedProblem("Invalid token.");
        }

        var response = await _mfaService.StartManagementChallengeAsync(
            userId,
            request.MfaTransactionId,
            request.ContinuationToken,
            request.Method,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken
        );

        return ToActionResult(response);
    }

    [Authorize]
    [HttpPost("management-sessions/challenges/verify")]
    public async Task<IActionResult> VerifyManagementChallenge(
        [FromBody] VerifyMfaManagementChallengeRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetUserId(User, out var userId))
        {
            return UnauthorizedProblem("Invalid token.");
        }

        var response = await _mfaService.VerifyManagementChallengeAsync(
            userId,
            request.MfaTransactionId,
            request.ContinuationToken,
            request.Code,
            cancellationToken
        );

        return ToActionResult(response);
    }

    [Authorize]
    [HttpPost("management-sessions/complete")]
    public async Task<IActionResult> CompleteManagementSession(
        [FromBody] CompleteMfaManagementSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetUserId(User, out var userId))
        {
            return UnauthorizedProblem("Invalid token.");
        }

        var response = await _mfaService.CompleteManagementSessionAsync(
            userId,
            request.MfaTransactionId,
            request.ContinuationToken,
            cancellationToken
        );

        return ToActionResult(response);
    }

    [Authorize]
    [HttpDelete("management-sessions/{mfaTransactionId}")]
    public async Task<IActionResult> CancelManagementSession(
        Guid mfaTransactionId,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetUserId(User, out var userId))
        {
            return UnauthorizedProblem("Invalid token.");
        }

        var response = await _mfaService.CancelManagementSessionAsync(
            userId,
            mfaTransactionId,
            cancellationToken
        );

        return ToActionResult(response);
    }

    [Authorize]
    [HttpDelete("methods/{method}")]
    public async Task<IActionResult> RemoveMethod(string method, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(User, out var userId))
        {
            return UnauthorizedProblem("Invalid token.");
        }

        var response = await _mfaService.RemoveMethodAsync(userId, method, cancellationToken);
        return ToActionResult(response);
    }

    [Authorize]
    [HttpPost("methods/{method}/reconfigure")]
    public async Task<IActionResult> StartReconfigureMethod(
        string method,
        [FromBody] StartMfaReconfigureRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetUserId(User, out var userId))
        {
            return UnauthorizedProblem("Invalid token.");
        }

        var response = await _mfaService.StartReconfigureMethodAsync(
            userId,
            method,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken
        );
        return ToActionResult(response);
    }

    [Authorize]
    [HttpPatch("methods/{method}/reconfigure/current")]
    public async Task<IActionResult> CompleteReconfigureMethod(
        string method,
        [FromBody] CompleteMfaReconfigureRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetUserId(User, out var userId))
        {
            return UnauthorizedProblem("Invalid token.");
        }

        var response = await _mfaService.CompleteReconfigureMethodAsync(
            userId,
            method,
            request,
            cancellationToken
        );

        return ToActionResult(response);
    }

    private new IActionResult ToActionResult<T>(Result<T> result)
    {
        var payload = result.ToResponsePayload();

        if (result.StatusCode.HasValue)
        {
            var statusCode = result.StatusCode.Value;
            if (
                statusCode == StatusCodes.Status409Conflict
                || statusCode == StatusCodes.Status410Gone
                || statusCode == StatusCodes.Status429TooManyRequests
                || statusCode == StatusCodes.Status503ServiceUnavailable
            )
            {
                if (statusCode == StatusCodes.Status429TooManyRequests)
                {
                    var retryAfterSeconds = Math.Max(1, _mfaApiPolicyOptions.RetryAfterSecondsOnTooManyRequests);
                    Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
                }

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

            return StatusCode(result.StatusCode.Value, payload);
        }

        return result.IsSuccess ? Ok(payload) : BadRequest(payload);
    }

    // NOTE: token_type and JTI are validated at the MfaChallengeScheme middleware level.
    // DB session lookup is intentionally omitted here — endpoints in this controller do not
    // issue access tokens directly (VerifyChallenge delegates that to MfaService).
    // Do NOT merge with Fido2Controller.ValidateMfaTokenContext — they have different security requirements.
    private async Task<(long UserId, Guid MfaTransactionId, IActionResult? ErrorResult)> ValidateMfaTokenContext(
        CancellationToken cancellationToken
    )
    {
        var hasUserId = TryGetUserId(User, out var userId);
        var tokenTransactionId = User.FindFirst("mfa_tx")?.Value;

        // token_type and JTI already validated at authentication scheme level (MfaChallengeScheme)
        if (!hasUserId || !Guid.TryParse(tokenTransactionId, out var mfaTransactionId))
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.mfa.token_validation",
                "Warning",
                false,
                userId,
                null,
                "Invalid MFA transaction ID",
                null,
                cancellationToken
            );

            return (0, Guid.Empty, UnauthorizedProblem("Invalid MFA context."));
        }

        return (userId, mfaTransactionId, null);
    }

    private async Task<(long UserId, Guid EnrollmentSessionId, IActionResult? ErrorResult)> ValidateLoginEnrollmentTokenContext(
        CancellationToken cancellationToken
    )
    {
        var hasUserId = TryGetUserId(User, out var userId);
        var enrollmentSessionIdValue = User.FindFirst("enrollment_sid")?.Value;

        // token_type and JTI already validated at authentication scheme level (LoginEnrollmentScheme)
        if (!hasUserId || !Guid.TryParse(enrollmentSessionIdValue, out var enrollmentSessionId))
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.mfa.login_enrollment.token_validation",
                "Warning",
                false,
                userId,
                null,
                "Invalid enrollment session ID",
                null,
                cancellationToken
            );

            return (0, Guid.Empty, UnauthorizedProblem("Invalid enrollment context."));
        }

        return (userId, enrollmentSessionId, null);
    }

    private sealed class NullMfaLoginEnrollmentSessionRepository : IMfaLoginEnrollmentSessionRepository
    {
        public Task AddAsync(Entities.MfaLoginEnrollmentSession session, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Entities.MfaLoginEnrollmentSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<Entities.MfaLoginEnrollmentSession?>(null);

        public Task<Entities.MfaLoginEnrollmentSession?> GetActiveByJtiAsync(string tokenJti, CancellationToken cancellationToken) =>
            Task.FromResult<Entities.MfaLoginEnrollmentSession?>(null);

        public Task UpdateAsync(Entities.MfaLoginEnrollmentSession session, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeAllActiveByUserAsync(long userId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
