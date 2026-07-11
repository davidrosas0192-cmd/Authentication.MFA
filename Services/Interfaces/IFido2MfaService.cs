using Authentication.Fido2.Common;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.DTOs.Fido2;

namespace Authentication.Fido2.Services.Interfaces;

public interface IFido2MfaService
{
    Task<Result<Fido2OptionsResponse>> CreateEnrollmentOptionsAsync(
        long userId,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken
    );

    Task<Result<CompleteFido2EnrollmentResponse>> CompleteEnrollmentAsync(
        CompleteFido2EnrollmentRequest request,
        long userId,
        CancellationToken cancellationToken
    );

    Task<Result<Fido2OptionsResponse>> CreateLoginOptionsAsync(
        long userId,
        Guid mfaTransactionId,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken
    );

    Task<Result<LoginResponse>> CompleteLoginAsync(
        CompleteFido2LoginRequest request,
        long userId,
        Guid mfaTransactionId,
        CancellationToken cancellationToken
    );
}
