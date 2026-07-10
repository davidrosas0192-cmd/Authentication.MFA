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

    Task<Result<string>> CompleteEnrollmentAsync(
        CompleteFido2EnrollmentRequest request,
        CancellationToken cancellationToken
    );

    Task<Result<Fido2OptionsResponse>> CreateLoginOptionsAsync(
        CreateFido2LoginOptionsRequest request,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken
    );

    Task<Result<LoginResponse>> CompleteLoginAsync(
        CompleteFido2LoginRequest request,
        CancellationToken cancellationToken
    );
}
