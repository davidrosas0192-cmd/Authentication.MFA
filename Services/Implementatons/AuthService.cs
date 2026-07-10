using Authentication.Fido2.Common;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.Services.Interfaces;

namespace Authentication.Fido2.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IAuditService _auditService;

    public AuthService(IUserRepository userRepository, ITokenService tokenService, IAuditService auditService)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _tokenService = tokenService;
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
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
            await _auditService.TrackAuthenticationEventAsync(
                null,
                request.Username,
                "password_login",
                "password",
                false,
                "Invalid credentials",
                cancellationToken
            );
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.password.login",
                "Warning",
                false,
                null,
                request.Username,
                "Invalid credentials",
                null,
                cancellationToken
            );

            return Result<LoginResponse>.Failure(
                "Invalid credentials.",
                StatusCodes.Status401Unauthorized
            );
        }

        if (user.IsFido2MfaEnabled)
        {
            await _auditService.TrackAuthenticationEventAsync(
                user.Id,
                user.Username,
                "password_login",
                "password",
                true,
                null,
                cancellationToken
            );
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.password.login_requires_mfa",
                "Information",
                true,
                user.Id,
                user.Username,
                null,
                new { requiresFido2 = true },
                cancellationToken
            );

            return Result<LoginResponse>.Success(
                new LoginResponse
                {
                    Status = "RequiresFido2",
                    RequiresFido2 = true,
                },
                "FIDO2 verification required."
            );
        }

        await _auditService.TrackAuthenticationEventAsync(
            user.Id,
            user.Username,
            "password_login",
            "password",
            true,
            null,
            cancellationToken
        );
        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.password.login_success",
            "Information",
            true,
            user.Id,
            user.Username,
            null,
            null,
            cancellationToken
        );

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
