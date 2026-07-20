using Authentication.Fido2.Common;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.DTOs.Fido2;

namespace Authentication.Fido2.Services.Interfaces;

public interface IFido2MfaService
{
    Task<Result<Fido2OptionsResponse>> CreateEnrollmentOptionsAsync(
        Guid userId,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken
    );

    Task<Result<CompleteFido2EnrollmentResponse>> CompleteEnrollmentAsync(
        CompleteFido2EnrollmentRequest request,
        Guid userId,
        CancellationToken cancellationToken
    );

    Task<Result<Fido2OptionsResponse>> CreateLoginOptionsAsync(
        Guid userId,
        Guid mfaTransactionId,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken
    );

    Task<Result<LoginResponse>> CompleteLoginAsync(
        CompleteFido2LoginRequest request,
        Guid userId,
        Guid mfaTransactionId,
        CancellationToken cancellationToken
    );
}
