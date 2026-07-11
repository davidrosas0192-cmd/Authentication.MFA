using Authentication.Fido2.Common;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.DTOs.Mfa;

namespace Authentication.Fido2.Services.Interfaces;

public interface IMfaService
{
    Task<List<string>> GetAllowedMethodsAsync(long userId, CancellationToken cancellationToken);
    Task<List<string>> GetAvailableSetupMethodsAsync(long userId, CancellationToken cancellationToken);
    Task<Result<StartMfaChallengeResponse>> StartChallengeAsync(
        long userId,
        Guid mfaTransactionId,
        string method,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
    Task<Result<LoginResponse>> VerifyChallengeAsync(
        long userId,
        Guid mfaTransactionId,
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
