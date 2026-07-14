using Authentication.Fido2.Common;
using Authentication.Fido2.Controllers;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.DTOs.Mfa;
using Authentication.Fido2.DTOs.Fido2;
using Authentication.Fido2.Entities;
using Authentication.Fido2.Services.Interfaces;

namespace Authentication.Fido2.Tests.TestSupport;

internal sealed class RecordingUserRegistrationService : IUserRegistrationService
{
    public int CallCount { get; private set; }
    public CreateUserRequest? LastRequest { get; private set; }
    public string? LastIpAddress { get; private set; }
    public string? LastUserAgent { get; private set; }
    public Func<Result<CreateUserResponse>>? ResultFactory { get; set; }

    public Task<Result<CreateUserResponse>> CreateUserAsync(
        CreateUserRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        CallCount++;
        LastRequest = request;
        LastIpAddress = ipAddress;
        LastUserAgent = userAgent;
        return Task.FromResult(ResultFactory?.Invoke() ?? throw new InvalidOperationException("CreateUser result not configured."));
    }
}

internal sealed class RecordingAuditService : IAuditService
{
    public int AuthenticationEventCallCount { get; private set; }
    public int SecurityEventCallCount { get; private set; }

    public Task TrackAuthenticationEventAsync(
        long? userId,
        string? usernameOrEmail,
        string stage,
        string method,
        bool isSuccess,
        string? failureReason,
        CancellationToken cancellationToken
    )
    {
        AuthenticationEventCallCount++;
        return Task.CompletedTask;
    }

    public Task TrackSecurityEventAsync(
        string category,
        string eventType,
        string severity,
        bool isSuccess,
        long? userId,
        string? usernameOrEmail,
        string? failureReason,
        object? details,
        CancellationToken cancellationToken
    )
    {
        SecurityEventCallCount++;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingAuthService : IAuthService
{
    public int LoginCallCount { get; private set; }
    public int LogoutCallCount { get; private set; }
    public int CancelAuthenticationCallCount { get; private set; }

    public LoginRequest? LastLoginRequest { get; private set; }
    public string? LastLoginIpAddress { get; private set; }
    public string? LastLoginUserAgent { get; private set; }
    public long LastLogoutUserId { get; private set; }
    public string? LastLogoutJti { get; private set; }
    public long LastCancelUserId { get; private set; }
    public string? LastCancelJti { get; private set; }

    public Func<Result<LoginResponse>>? LoginResultFactory { get; set; }
    public Func<Result>? LogoutResultFactory { get; set; }
    public Func<Result>? CancelAuthenticationResultFactory { get; set; }

    public Task<Result<LoginResponse>> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        LoginCallCount++;
        LastLoginRequest = request;
        LastLoginIpAddress = ipAddress;
        LastLoginUserAgent = userAgent;
        return Task.FromResult(LoginResultFactory?.Invoke() ?? throw new InvalidOperationException("Login result not configured."));
    }

    public Task<Result> LogoutAsync(long userId, string tokenJti, CancellationToken cancellationToken)
    {
        LogoutCallCount++;
        LastLogoutUserId = userId;
        LastLogoutJti = tokenJti;
        return Task.FromResult(LogoutResultFactory?.Invoke() ?? throw new InvalidOperationException("Logout result not configured."));
    }

    public Task<Result> CancelAuthenticationAsync(long userId, string tokenJti, CancellationToken cancellationToken)
    {
        CancelAuthenticationCallCount++;
        LastCancelUserId = userId;
        LastCancelJti = tokenJti;
        return Task.FromResult(CancelAuthenticationResultFactory?.Invoke() ?? throw new InvalidOperationException("CancelAuthentication result not configured."));
    }
}

internal sealed class RecordingMfaService : IMfaService
{
    public int GetAllowedMethodsCallCount { get; private set; }
    public int GetAvailableSetupMethodsCallCount { get; private set; }
    public int StartManagementSessionCallCount { get; private set; }
    public int VerifyManagementChallengeCallCount { get; private set; }
    public int CompleteManagementSessionCallCount { get; private set; }
    public int CancelManagementSessionCallCount { get; private set; }
    public int StartChallengeCallCount { get; private set; }
    public int VerifyChallengeCallCount { get; private set; }
    public int StartEnrollmentCallCount { get; private set; }
    public int VerifyEnrollmentCallCount { get; private set; }
    public int RemoveMethodCallCount { get; private set; }
    public int StartReconfigureMethodCallCount { get; private set; }
    public int CompleteReconfigureMethodCallCount { get; private set; }
    public int CreateSelectionChallengeCallCount { get; private set; }

    public long LastUserId { get; private set; }
    public Guid LastMfaTransactionId { get; private set; }
    public string? LastMethod { get; private set; }
    public string? LastCode { get; private set; }
    public StartMfaEnrollmentRequest? LastStartEnrollmentRequest { get; private set; }
    public VerifyMfaEnrollmentRequest? LastVerifyEnrollmentRequest { get; private set; }
    public string? LastMethodRouteValue { get; private set; }
    public StartMfaReconfigureRequest? LastStartReconfigureRequest { get; private set; }
    public CompleteMfaReconfigureRequest? LastCompleteReconfigureRequest { get; private set; }

    public List<string> AllowedMethodsToReturn { get; set; } = [];
    public List<string> AvailableSetupMethodsToReturn { get; set; } = [];
    public Result<StartMfaManagementSessionResponse> StartManagementSessionResultToReturn { get; set; } = Result<StartMfaManagementSessionResponse>.Failure("Not configured");
    public Result<VerifyMfaManagementChallengeResponse> VerifyManagementChallengeResultToReturn { get; set; } = Result<VerifyMfaManagementChallengeResponse>.Failure("Not configured");
    public Result<CompleteMfaManagementSessionResponse> CompleteManagementSessionResultToReturn { get; set; } = Result<CompleteMfaManagementSessionResponse>.Failure("Not configured");
    public Result<CancelMfaManagementSessionResponse> CancelManagementSessionResultToReturn { get; set; } = Result<CancelMfaManagementSessionResponse>.Failure("Not configured");
    public Result<StartMfaChallengeResponse> StartChallengeResultToReturn { get; set; } = Result<StartMfaChallengeResponse>.Failure("Not configured");
    public Result<LoginResponse> VerifyChallengeResultToReturn { get; set; } = Result<LoginResponse>.Failure("Not configured");
    public Result<StartMfaEnrollmentResponse> StartEnrollmentResultToReturn { get; set; } = Result<StartMfaEnrollmentResponse>.Failure("Not configured");
    public Result<VerifyMfaEnrollmentResponse> VerifyEnrollmentResultToReturn { get; set; } = Result<VerifyMfaEnrollmentResponse>.Failure("Not configured");
    public Result<RemoveMfaMethodResponse> RemoveMethodResultToReturn { get; set; } = Result<RemoveMfaMethodResponse>.Failure("Not configured");
    public Result<StartMfaReconfigureResponse> StartReconfigureMethodResultToReturn { get; set; } = Result<StartMfaReconfigureResponse>.Failure("Not configured");
    public Result<CompleteMfaReconfigureResponse> CompleteReconfigureMethodResultToReturn { get; set; } = Result<CompleteMfaReconfigureResponse>.Failure("Not configured");
    public Guid SelectionChallengeToReturn { get; set; }

    public Task<List<string>> GetAllowedMethodsAsync(long userId, CancellationToken cancellationToken)
    {
        GetAllowedMethodsCallCount++;
        LastUserId = userId;
        return Task.FromResult(AllowedMethodsToReturn);
    }

    public Task<List<string>> GetAvailableSetupMethodsAsync(long userId, CancellationToken cancellationToken)
    {
        GetAvailableSetupMethodsCallCount++;
        LastUserId = userId;
        return Task.FromResult(AvailableSetupMethodsToReturn);
    }

    public Task<Result<StartMfaManagementSessionResponse>> StartManagementSessionAsync(
        long userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        StartManagementSessionCallCount++;
        LastUserId = userId;
        return Task.FromResult(StartManagementSessionResultToReturn);
    }

    public Task<Result<StartMfaChallengeResponse>> StartChallengeAsync(
        long userId,
        Guid mfaTransactionId,
        string method,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        StartChallengeCallCount++;
        LastUserId = userId;
        LastMfaTransactionId = mfaTransactionId;
        LastMethod = method;
        return Task.FromResult(StartChallengeResultToReturn);
    }

    public Task<Result<StartMfaChallengeResponse>> StartManagementChallengeAsync(
        long userId,
        Guid managementSessionId,
        string continuationToken,
        string method,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        StartChallengeCallCount++;
        LastUserId = userId;
        LastMfaTransactionId = managementSessionId;
        LastMethod = method;
        return Task.FromResult(StartChallengeResultToReturn);
    }

    public Task<Result<LoginResponse>> VerifyChallengeAsync(
        long userId,
        Guid mfaTransactionId,
        string continuationToken,
        string code,
        CancellationToken cancellationToken
    )
    {
        VerifyChallengeCallCount++;
        LastUserId = userId;
        LastMfaTransactionId = mfaTransactionId;
        LastCode = code;
        return Task.FromResult(VerifyChallengeResultToReturn);
    }

    public Task<Result<VerifyMfaManagementChallengeResponse>> VerifyManagementChallengeAsync(
        long userId,
        Guid mfaTransactionId,
        string continuationToken,
        string code,
        CancellationToken cancellationToken
    )
    {
        VerifyManagementChallengeCallCount++;
        LastUserId = userId;
        LastMfaTransactionId = mfaTransactionId;
        LastCode = code;
        return Task.FromResult(VerifyManagementChallengeResultToReturn);
    }

    public Task<Result<CompleteMfaManagementSessionResponse>> CompleteManagementSessionAsync(
        long userId,
        Guid mfaTransactionId,
        string continuationToken,
        CancellationToken cancellationToken
    )
    {
        CompleteManagementSessionCallCount++;
        LastUserId = userId;
        LastMfaTransactionId = mfaTransactionId;
        return Task.FromResult(CompleteManagementSessionResultToReturn);
    }

    public Task<Result<CancelMfaManagementSessionResponse>> CancelManagementSessionAsync(
        long userId,
        Guid mfaTransactionId,
        CancellationToken cancellationToken
    )
    {
        CancelManagementSessionCallCount++;
        LastUserId = userId;
        LastMfaTransactionId = mfaTransactionId;
        return Task.FromResult(CancelManagementSessionResultToReturn);
    }

    public Task<Result<StartMfaEnrollmentResponse>> StartEnrollmentAsync(
        long userId,
        StartMfaEnrollmentRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        StartEnrollmentCallCount++;
        LastUserId = userId;
        LastStartEnrollmentRequest = request;
        return Task.FromResult(StartEnrollmentResultToReturn);
    }

    public Task<Result<VerifyMfaEnrollmentResponse>> VerifyEnrollmentAsync(
        long userId,
        VerifyMfaEnrollmentRequest request,
        CancellationToken cancellationToken
    )
    {
        VerifyEnrollmentCallCount++;
        LastUserId = userId;
        LastVerifyEnrollmentRequest = request;
        return Task.FromResult(VerifyEnrollmentResultToReturn);
    }

    public Task<Result<RemoveMfaMethodResponse>> RemoveMethodAsync(
        long userId,
        string method,
        CancellationToken cancellationToken
    )
    {
        RemoveMethodCallCount++;
        LastUserId = userId;
        LastMethodRouteValue = method;
        return Task.FromResult(RemoveMethodResultToReturn);
    }

    public Task<Result<StartMfaReconfigureResponse>> StartReconfigureMethodAsync(
        long userId,
        string method,
        StartMfaReconfigureRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        StartReconfigureMethodCallCount++;
        LastUserId = userId;
        LastMethodRouteValue = method;
        LastStartReconfigureRequest = request;
        return Task.FromResult(StartReconfigureMethodResultToReturn);
    }

    public Task<Result<CompleteMfaReconfigureResponse>> CompleteReconfigureMethodAsync(
        long userId,
        string method,
        CompleteMfaReconfigureRequest request,
        CancellationToken cancellationToken
    )
    {
        CompleteReconfigureMethodCallCount++;
        LastUserId = userId;
        LastMethodRouteValue = method;
        LastCompleteReconfigureRequest = request;
        return Task.FromResult(CompleteReconfigureMethodResultToReturn);
    }

    public Task<Guid> CreateSelectionChallengeAsync(
        long userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        CreateSelectionChallengeCallCount++;
        LastUserId = userId;
        return Task.FromResult(SelectionChallengeToReturn);
    }
}

internal sealed class RecordingFido2MfaService : IFido2MfaService
{
    public int CreateEnrollmentOptionsCallCount { get; private set; }
    public int CompleteEnrollmentCallCount { get; private set; }
    public int CreateLoginOptionsCallCount { get; private set; }
    public int CompleteLoginCallCount { get; private set; }

    public long LastUserId { get; private set; }
    public Guid LastMfaTransactionId { get; private set; }
    public string? LastIpAddress { get; private set; }
    public string? LastUserAgent { get; private set; }
    public CompleteFido2EnrollmentRequest? LastCompleteEnrollmentRequest { get; private set; }
    public CompleteFido2LoginRequest? LastCompleteLoginRequest { get; private set; }

    public Result<Fido2OptionsResponse> CreateEnrollmentOptionsResultToReturn { get; set; } = Result<Fido2OptionsResponse>.Failure("Not configured");
    public Result<CompleteFido2EnrollmentResponse> CompleteEnrollmentResultToReturn { get; set; } = Result<CompleteFido2EnrollmentResponse>.Failure("Not configured");
    public Result<Fido2OptionsResponse> CreateLoginOptionsResultToReturn { get; set; } = Result<Fido2OptionsResponse>.Failure("Not configured");
    public Result<LoginResponse> CompleteLoginResultToReturn { get; set; } = Result<LoginResponse>.Failure("Not configured");

    public Task<Result<Fido2OptionsResponse>> CreateEnrollmentOptionsAsync(
        long userId,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken
    )
    {
        CreateEnrollmentOptionsCallCount++;
        LastUserId = userId;
        LastIpAddress = ipAddress;
        LastUserAgent = userAgent;
        return Task.FromResult(CreateEnrollmentOptionsResultToReturn);
    }

    public Task<Result<CompleteFido2EnrollmentResponse>> CompleteEnrollmentAsync(CompleteFido2EnrollmentRequest request, long userId, CancellationToken cancellationToken)
    {
        CompleteEnrollmentCallCount++;
        LastCompleteEnrollmentRequest = request;
        LastUserId = userId;
        return Task.FromResult(CompleteEnrollmentResultToReturn);
    }

    public Task<Result<Fido2OptionsResponse>> CreateLoginOptionsAsync(
        long userId,
        Guid mfaTransactionId,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken
    )
    {
        CreateLoginOptionsCallCount++;
        LastUserId = userId;
        LastMfaTransactionId = mfaTransactionId;
        LastIpAddress = ipAddress;
        LastUserAgent = userAgent;
        return Task.FromResult(CreateLoginOptionsResultToReturn);
    }

    public Task<Result<LoginResponse>> CompleteLoginAsync(
        CompleteFido2LoginRequest request,
        long userId,
        Guid mfaTransactionId,
        CancellationToken cancellationToken
    )
    {
        CompleteLoginCallCount++;
        LastCompleteLoginRequest = request;
        LastUserId = userId;
        LastMfaTransactionId = mfaTransactionId;
        return Task.FromResult(CompleteLoginResultToReturn);
    }
}

internal sealed class RecordingMfaTempTokenSessionRepository : IMfaTempTokenSessionRepository
{
    public MfaTempTokenSession? ActiveSessionToReturn { get; set; }
    public int ConsumeCallCount { get; private set; }
    public int AddCallCount { get; private set; }
    public int RevokeByJtiCallCount { get; private set; }
    public int RevokeAllActiveByUserCallCount { get; private set; }

    public string? LastJti { get; private set; }
    public Guid LastConsumedTransactionId { get; private set; }

    public Task AddAsync(MfaTempTokenSession session, CancellationToken cancellationToken)
    {
        AddCallCount++;
        return Task.CompletedTask;
    }

    public Task<MfaTempTokenSession?> GetActiveByJtiAsync(string tokenJti, CancellationToken cancellationToken)
    {
        LastJti = tokenJti;
        return Task.FromResult(ActiveSessionToReturn);
    }

    public Task ConsumeByTransactionAsync(Guid mfaTransactionId, CancellationToken cancellationToken)
    {
        ConsumeCallCount++;
        LastConsumedTransactionId = mfaTransactionId;
        return Task.CompletedTask;
    }

    public Task RevokeByJtiAsync(string tokenJti, string reason, CancellationToken cancellationToken)
    {
        RevokeByJtiCallCount++;
        LastJti = tokenJti;
        return Task.CompletedTask;
    }

    public Task RevokeAllActiveByUserAsync(long userId, string reason, CancellationToken cancellationToken)
    {
        RevokeAllActiveByUserCallCount++;
        return Task.CompletedTask;
    }
}