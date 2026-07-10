using Authentication.Fido2.Common;
using Authentication.Fido2.Constants;
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
    private readonly IAccessTokenSessionRepository _accessTokenSessionRepository;
    private readonly IMfaTempTokenSessionRepository _mfaTempTokenSessionRepository;
    private readonly IAuditService _auditService;
    private readonly IMfaService _mfaService;
    private readonly JwtOptions _jwtOptions;
    private readonly MfaJwtOptions _mfaJwtOptions;

    public AuthService(
        IUserRepository userRepository,
        ITokenService tokenService,
        IAccessTokenSessionRepository accessTokenSessionRepository,
        IMfaTempTokenSessionRepository mfaTempTokenSessionRepository,
        IAuditService auditService,
        IMfaService mfaService,
        IOptions<JwtOptions> jwtOptions,
        IOptions<MfaJwtOptions> mfaJwtOptions
    )
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _tokenService = tokenService;
        _accessTokenSessionRepository =
            accessTokenSessionRepository
            ?? throw new ArgumentNullException(nameof(accessTokenSessionRepository));
        _mfaTempTokenSessionRepository =
            mfaTempTokenSessionRepository
            ?? throw new ArgumentNullException(nameof(mfaTempTokenSessionRepository));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _mfaService = mfaService ?? throw new ArgumentNullException(nameof(mfaService));
        _jwtOptions = jwtOptions.Value;
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

        if (user is null || !user.IsActive || !PasswordHasher.Verify(request.Password, user.PasswordHash))
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
            await _accessTokenSessionRepository.RevokeAllActiveByUserAsync(
                user.Id,
                "new_login",
                cancellationToken
            );
            await _mfaTempTokenSessionRepository.RevokeAllActiveByUserAsync(
                user.Id,
                "new_login",
                cancellationToken
            );

            var mfaTransactionId = await _mfaService.CreateSelectionChallengeAsync(
                user.Id,
                ipAddress,
                userAgent,
                cancellationToken
            );
            var tokenJti = Guid.NewGuid().ToString("N");
            var mfaToken = _tokenService.CreateMfaToken(user, mfaTransactionId, tokenJti);

            await _mfaTempTokenSessionRepository.AddAsync(
                new Entities.MfaTempTokenSession
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    MfaTransactionId = mfaTransactionId,
                    TokenJti = tokenJti,
                    IssuedAtUtc = DateTime.UtcNow,
                    ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_mfaJwtOptions.ExpirationMinutes),
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                },
                cancellationToken
            );

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

        await _accessTokenSessionRepository.RevokeAllActiveByUserAsync(
            user.Id,
            "new_login",
            cancellationToken
        );
        await _mfaTempTokenSessionRepository.RevokeAllActiveByUserAsync(
            user.Id,
            "new_login",
            cancellationToken
        );

        var accessTokenJti = Guid.NewGuid().ToString("N");
        var accessToken = _tokenService.CreateAccessToken(user, accessTokenJti);
        var refreshToken = _tokenService.CreateRefreshToken();

        await _accessTokenSessionRepository.AddAsync(
            new Entities.AccessTokenSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenJti = accessTokenJti,
                IssuedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes),
                IpAddress = ipAddress,
                UserAgent = userAgent,
            },
            cancellationToken
        );

        return Result<LoginResponse>.Success(
            new LoginResponse
            {
                Status = "Authenticated",
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = 15 * 60,
                AvailableMfaSetupOptions = [
                    MfaMethodTypes.Sms,
                    MfaMethodTypes.Email,
                    MfaMethodTypes.Fido2,
                ],
            },
            "Authentication succeeded."
        );
    }

    public async Task<Result> LogoutAsync(
        long userId,
        string tokenJti,
        CancellationToken cancellationToken
    )
    {
        await _accessTokenSessionRepository.RevokeByJtiAsync(tokenJti, "logout", cancellationToken);
        await _mfaTempTokenSessionRepository.RevokeAllActiveByUserAsync(userId, "logout", cancellationToken);

        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.logout",
            "Information",
            true,
            userId,
            null,
            null,
            null,
            cancellationToken
        );

        return Result.Success("Session closed.");
    }

    public async Task<Result> CancelAuthenticationAsync(
        long userId,
        string tokenJti,
        CancellationToken cancellationToken
    )
    {
        await _mfaTempTokenSessionRepository.RevokeByJtiAsync(
            tokenJti,
            "cancel_authentication",
            cancellationToken
        );

        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.cancel_authentication",
            "Information",
            true,
            userId,
            null,
            null,
            null,
            cancellationToken
        );

        return Result.Success("Authentication canceled.");
    }
}
