using Authentication.Fido2.Common;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.Options;
using Authentication.Fido2.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Authentication.Fido2.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IAuditService _auditService;
    private readonly IMfaService _mfaService;
    private readonly MfaJwtOptions _mfaJwtOptions;

    public AuthService(
        IUserRepository userRepository,
        ITokenService tokenService,
        IAuditService auditService,
        IMfaService mfaService,
        IOptions<MfaJwtOptions> mfaJwtOptions
    )
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _tokenService = tokenService;
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _mfaService = mfaService ?? throw new ArgumentNullException(nameof(mfaService));
        _mfaJwtOptions = mfaJwtOptions.Value;
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

        var allowedMfaMethods = await _mfaService.GetAllowedMethodsAsync(user.Id, cancellationToken);
        if (allowedMfaMethods.Count == 0 && user.IsFido2MfaEnabled)
        {
            allowedMfaMethods.Add("fido2");
        }

        if (allowedMfaMethods.Count > 0)
        {
            var mfaTransactionId = await _mfaService.CreateSelectionChallengeAsync(
                user.Id,
                ipAddress,
                userAgent,
                cancellationToken
            );
            var mfaToken = _tokenService.CreateMfaToken(user, mfaTransactionId);

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
                new { allowedMfaMethods },
                cancellationToken
            );

            return Result<LoginResponse>.Success(
                new LoginResponse
                {
                    Status = "RequiresMfa",
                    RequiresFido2 = allowedMfaMethods.Contains("fido2"),
                    MfaRequired = true,
                    MfaTransactionId = mfaTransactionId,
                    MfaToken = mfaToken,
                    MfaExpiresIn = _mfaJwtOptions.ExpirationMinutes * 60,
                    AllowedMfaMethods = allowedMfaMethods,
                },
                "MFA verification required."
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
