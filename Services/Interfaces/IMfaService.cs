using Authentication.Fido2.Common;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.DTOs.Mfa;

namespace Authentication.Fido2.Services.Interfaces;

public interface IMfaService
{
    Task<List<string>> GetAllowedMethodsAsync(long userId, CancellationToken cancellationToken);
    Task<Result<StartMfaChallengeResponse>> StartChallengeAsync(
        StartMfaChallengeRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
    Task<Result<LoginResponse>> VerifyChallengeAsync(
        VerifyMfaChallengeRequest request,
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
    Task<Guid> CreateSelectionChallengeAsync(
        long userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
}
