using Authentication.Fido2.Common;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.DTOs.Mfa;

namespace Authentication.Fido2.Services.Interfaces;

public interface IMfaService
{
    Task<List<string>> GetAllowedMethodsAsync(long userId, CancellationToken cancellationToken);
    Task<List<string>> GetAvailableSetupMethodsAsync(long userId, CancellationToken cancellationToken);
    Task<Result<StartMfaManagementSessionResponse>> StartManagementSessionAsync(
        long userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
    Task<Result<StartMfaChallengeResponse>> StartManagementChallengeAsync(
        long userId,
        Guid managementSessionId,
        string continuationToken,
        string method,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
    Task<Result<StartMfaChallengeResponse>> StartChallengeAsync(
        long userId,
        Guid mfaTransactionId,
        string method,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
    Task<Result<VerifyMfaManagementChallengeResponse>> VerifyManagementChallengeAsync(
        long userId,
        Guid managementSessionId,
        string continuationToken,
        string code,
        CancellationToken cancellationToken
    );
    Task<Result<CompleteMfaManagementSessionResponse>> CompleteManagementSessionAsync(
        long userId,
        Guid managementSessionId,
        string continuationToken,
        CancellationToken cancellationToken
    );
    Task<Result<CancelMfaManagementSessionResponse>> CancelManagementSessionAsync(
        long userId,
        Guid mfaTransactionId,
        CancellationToken cancellationToken
    );
    Task<Result<LoginResponse>> VerifyChallengeAsync(
        long userId,
        Guid mfaTransactionId,
        string continuationToken,
        string code,
        CancellationToken cancellationToken
    );
    Task<Result<StartMfaEnrollmentResponse>> StartEnrollmentAsync(
        long userId,
        StartMfaEnrollmentRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
    Task<Result<VerifyMfaEnrollmentResponse>> VerifyEnrollmentAsync(
        long userId,
        VerifyMfaEnrollmentRequest request,
        CancellationToken cancellationToken
    );
    Task<Result<RemoveMfaMethodResponse>> RemoveMethodAsync(
        long userId,
        string method,
        CancellationToken cancellationToken
    );
    Task<Result<StartMfaReconfigureResponse>> StartReconfigureMethodAsync(
        long userId,
        string method,
        StartMfaReconfigureRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
    Task<Result<CompleteMfaReconfigureResponse>> CompleteReconfigureMethodAsync(
        long userId,
        string method,
        CompleteMfaReconfigureRequest request,
        CancellationToken cancellationToken
    );
    Task<Guid> CreateSelectionChallengeAsync(
        long userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
}
