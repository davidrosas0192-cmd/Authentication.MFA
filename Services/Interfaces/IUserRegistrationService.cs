using Authentication.Fido2.Common;
using Authentication.Fido2.DTOs.Auth;

namespace Authentication.Fido2.Services.Interfaces;

public interface IUserRegistrationService
{
    Task<Result<CreateUserResponse>> CreateUserAsync(
        CreateUserRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
}