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

    Task<Result> LogoutAsync(Guid userId, string tokenJti, CancellationToken cancellationToken);

    Task<Result> CancelAuthenticationAsync(Guid userId, string tokenJti, CancellationToken cancellationToken);

    Task<Result<LoginResponse>> RefreshTokenAsync(
        string refreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
}
