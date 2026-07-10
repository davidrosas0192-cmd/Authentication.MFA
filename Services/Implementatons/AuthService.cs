using Authentication.Fido2.Common;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.Services.Interfaces;

namespace Authentication.Fido2.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;

    public AuthService(IUserRepository userRepository, ITokenService tokenService)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _tokenService = tokenService;
    }

    public async Task<Result<LoginResponse>> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        var user = await _userRepository.GetByUsernameOrEmailAsync(
            request.Username,
            cancellationToken
        );

        if (user is null || !user.IsActive || user.PasswordHash != request.Password)
        {
            return Result<LoginResponse>.Failure(
                "Invalid credentials.",
                StatusCodes.Status401Unauthorized
            );
        }

        if (user.IsFido2MfaEnabled)
        {
            return Result<LoginResponse>.Success(
                new LoginResponse
                {
                    Status = "RequiresFido2",
                    RequiresFido2 = true,
                },
                "FIDO2 verification required."
            );
        }

        return Result<LoginResponse>.Success(
            new LoginResponse
            {
                Status = "Authenticated",
                AccessToken = _tokenService.CreateAccessToken(user),
                RefreshToken = _tokenService.CreateRefreshToken(),
                ExpiresIn = 15 * 60,
            },
            "Authentication succeeded."
        );
    }
}
