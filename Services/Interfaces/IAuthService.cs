using Authentication.Fido2.Common;
using Authentication.Fido2.DTOs.Auth;

namespace Authentication.Fido2.Services.Interfaces;

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
}
