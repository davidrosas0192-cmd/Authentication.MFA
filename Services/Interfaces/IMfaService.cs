using Authentication.Fido2.Common;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.DTOs.Mfa;

namespace Authentication.Fido2.Services.Interfaces;

public interface IMfaService
{
    Task<List<string>> GetAllowedMethodsAsync(Guid userId, CancellationToken cancellationToken);
    Task<List<string>> GetAvailableSetupMethodsAsync(Guid userId, CancellationToken cancellationToken);
    Task<(Guid SessionId, string ContinuationToken)> StartLoginEnrollmentSessionAsync(
        Guid userId,
        string tokenJti,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
    Task<Result<StartMfaManagementSessionResponse>> StartManagementSessionAsync(
        Guid userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
    Task<Result<StartMfaChallengeResponse>> StartManagementChallengeAsync(
        Guid userId,
        Guid managementSessionId,
        string continuationToken,
        string method,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
    Task<Result<StartMfaChallengeResponse>> StartChallengeAsync(
        Guid userId,
        Guid mfaTransactionId,
        string method,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
    Task<Result<VerifyMfaManagementChallengeResponse>> VerifyManagementChallengeAsync(
        Guid userId,
        Guid managementSessionId,
        string continuationToken,
        string code,
        CancellationToken cancellationToken
    );
    Task<Result<CompleteMfaManagementSessionResponse>> CompleteManagementSessionAsync(
        Guid userId,
        Guid managementSessionId,
        string continuationToken,
        CancellationToken cancellationToken
    );
    Task<Result<CancelMfaManagementSessionResponse>> CancelManagementSessionAsync(
        Guid userId,
        Guid mfaTransactionId,
        CancellationToken cancellationToken
    );
    Task<Result<LoginResponse>> VerifyChallengeAsync(
        Guid userId,
        Guid mfaTransactionId,
        string continuationToken,
        string code,
        CancellationToken cancellationToken
    );
    Task<Result<StartMfaEnrollmentResponse>> StartEnrollmentAsync(
        Guid userId,
        StartMfaEnrollmentRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
    Task<Result<VerifyMfaEnrollmentResponse>> VerifyEnrollmentAsync(
        Guid userId,
        VerifyMfaEnrollmentRequest request,
        CancellationToken cancellationToken
    );
    Task<Result<StartLoginEnrollmentResponse>> StartLoginEnrollmentAsync(
        Guid userId,
        Guid enrollmentSessionId,
        StartLoginEnrollmentRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
    Task<Result<VerifyLoginEnrollmentResponse>> VerifyLoginEnrollmentAsync(
        Guid userId,
        Guid enrollmentSessionId,
        VerifyLoginEnrollmentRequest request,
        CancellationToken cancellationToken
    );
    Task<Result<LoginResponse>> CompleteLoginEnrollmentSessionAsync(
        Guid userId,
        Guid enrollmentSessionId,
        string continuationToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
    Task<Result<RemoveMfaMethodResponse>> RemoveMethodAsync(
        Guid userId,
        string method,
        CancellationToken cancellationToken
    );
    Task<Result<StartMfaReconfigureResponse>> StartReconfigureMethodAsync(
        Guid userId,
        string method,
        StartMfaReconfigureRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
    Task<Result<CompleteMfaReconfigureResponse>> CompleteReconfigureMethodAsync(
        Guid userId,
        string method,
        CompleteMfaReconfigureRequest request,
        CancellationToken cancellationToken
    );
    Task<Guid> CreateSelectionChallengeAsync(
        Guid userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
}
