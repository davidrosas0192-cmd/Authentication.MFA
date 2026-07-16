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
    private readonly IMfaLoginEnrollmentSessionRepository _mfaLoginEnrollmentSessionRepository;
    private readonly IRefreshTokenSessionRepository _refreshTokenSessionRepository;
    private readonly IRateLimitingService _rateLimitingService;
    private readonly ISessionFactory _sessionFactory;
    private readonly IAuditService _auditService;
    private readonly IMfaService _mfaService;
    private readonly JwtOptions _jwtOptions;
    private readonly MfaJwtOptions _mfaJwtOptions;

    public AuthService(
        IUserRepository userRepository,
        ITokenService tokenService,
        IAccessTokenSessionRepository accessTokenSessionRepository,
        IMfaTempTokenSessionRepository mfaTempTokenSessionRepository,
        IMfaLoginEnrollmentSessionRepository mfaLoginEnrollmentSessionRepository,
        IRefreshTokenSessionRepository refreshTokenSessionRepository,
        IRateLimitingService rateLimitingService,
        ISessionFactory sessionFactory,
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
        _mfaLoginEnrollmentSessionRepository =
            mfaLoginEnrollmentSessionRepository
            ?? throw new ArgumentNullException(nameof(mfaLoginEnrollmentSessionRepository));
        _refreshTokenSessionRepository =
            refreshTokenSessionRepository
            ?? throw new ArgumentNullException(nameof(refreshTokenSessionRepository));
        _rateLimitingService =
            rateLimitingService ?? throw new ArgumentNullException(nameof(rateLimitingService));
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
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
        // Rate limiting: 10 attempts per 15 minutes per IP
        var rateLimitKey = $"login_{ipAddress ?? "unknown"}";
        if (!_rateLimitingService.IsAllowed(rateLimitKey, maxAttempts: 10, windowSeconds: 900))
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.password.login_rate_limited",
                "Warning",
                false,
                null,
                null,
                "Rate limit exceeded",
                new { ipAddress },
                cancellationToken
            );

            return Result<LoginResponse>.Failure(
                "Too many login attempts. Please try again later.",
                StatusCodes.Status429TooManyRequests
            );
        }

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
        var availableSetupOptions = await _mfaService.GetAvailableSetupMethodsAsync(user.Id, cancellationToken);

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
                    MfaRequired = true,
                    MfaToken = mfaToken,
                    MfaExpiresIn = _mfaJwtOptions.ExpirationMinutes * 60,
                    AllowedMfaMethods = allowedMfaMethods,
                },
                "MFA verification required."
            );
        }

        var bootstrapSetupOptions = availableSetupOptions
            .Where(x => x == MfaMethodTypes.Sms || x == MfaMethodTypes.Email)
            .ToList();

        if (bootstrapSetupOptions.Count > 0)
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
            await _mfaLoginEnrollmentSessionRepository.RevokeAllActiveByUserAsync(
                user.Id,
                cancellationToken
            );

            var tokenJti = Guid.NewGuid().ToString("N");
            var (enrollmentSessionId, continuationToken) = await _mfaService.StartLoginEnrollmentSessionAsync(
                user.Id,
                tokenJti,
                ipAddress,
                userAgent,
                cancellationToken
            );
            var enrollmentToken = _tokenService.CreateLoginEnrollmentToken(user, enrollmentSessionId, tokenJti);

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
                "auth.password.login_requires_enrollment",
                "Information",
                true,
                user.Id,
                user.Username,
                null,
                new { availableSetupOptions = bootstrapSetupOptions },
                cancellationToken
            );

            return Result<LoginResponse>.Success(
                new LoginResponse
                {
                    Status = "RequiresEnrollment",
                    EnrollmentToken = enrollmentToken,
                    EnrollmentExpiresIn = _mfaJwtOptions.ExpirationMinutes * 60,
                    EnrollmentSessionId = enrollmentSessionId,
                    EnrollmentContinuationToken = continuationToken,
                    AvailableMfaSetupOptions = bootstrapSetupOptions,
                },
                "MFA enrollment required before completing authentication."
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

        var (accessToken, refreshToken) = await _sessionFactory.CreateAuthenticatedSessionAsync(
            user,
            ipAddress,
            userAgent,
            cancellationToken
        );

        return Result<LoginResponse>.Success(
            new LoginResponse
            {
                Status = "Authenticated",
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = 15 * 60,
                AllowedMfaMethods = allowedMfaMethods,
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

    public async Task<Result<LoginResponse>> RefreshTokenAsync(
        string refreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        // Rate limiting: 30 refresh attempts per 15 minutes per IP
        var rateLimitKey = $"refresh_{ipAddress ?? "unknown"}";
        if (!_rateLimitingService.IsAllowed(rateLimitKey, maxAttempts: 30, windowSeconds: 900))
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.refresh_token.rate_limited",
                "Warning",
                false,
                null,
                null,
                "Rate limit exceeded",
                new { ipAddress },
                cancellationToken
            );

            return Result<LoginResponse>.Failure(
                "Too many token refresh attempts. Please try again later.",
                StatusCodes.Status429TooManyRequests
            );
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result<LoginResponse>.Failure(
                "Invalid refresh token.",
                StatusCodes.Status401Unauthorized
            );
        }

        var tokenHash = _tokenService.HashRefreshToken(refreshToken);
        var refreshTokenSession = await _refreshTokenSessionRepository.GetByTokenHashAsync(
            tokenHash,
            cancellationToken
        );

        if (refreshTokenSession is null)
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.refresh_token_rejected",
                "Warning",
                false,
                null,
                null,
                "Invalid or revoked refresh token",
                null,
                cancellationToken
            );

            return Result<LoginResponse>.Failure(
                "Invalid or expired refresh token.",
                StatusCodes.Status401Unauthorized
            );
        }

        var user = await _userRepository.GetByIdAsync(refreshTokenSession.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            await _refreshTokenSessionRepository.RevokeByIdAsync(
                refreshTokenSession.Id,
                "user_inactive",
                cancellationToken
            );

            return Result<LoginResponse>.Failure(
                "User not found or inactive.",
                StatusCodes.Status404NotFound
            );
        }

        // Revoke old refresh token
        refreshTokenSession.RevokedAtUtc = DateTime.UtcNow;
        refreshTokenSession.RevokeReason = "rotated";
        await _refreshTokenSessionRepository.UpdateAsync(refreshTokenSession, cancellationToken);

        // Create new access token
        var accessTokenJti = Guid.NewGuid().ToString("N");
        var accessToken = _tokenService.CreateAccessToken(user, accessTokenJti);
        var newRefreshToken = _tokenService.CreateRefreshToken();

        // Create new access token session
        var accessTokenSession = new Entities.AccessTokenSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenJti = accessTokenJti,
            IssuedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes),
            IpAddress = ipAddress,
            UserAgent = userAgent,
        };

        await _accessTokenSessionRepository.AddAsync(accessTokenSession, cancellationToken);

        // Create new refresh token session
        var newRefreshTokenSession = new Entities.RefreshTokenSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = _tokenService.HashRefreshToken(newRefreshToken),
            AccessTokenSessionId = accessTokenSession.Id,
            IssuedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(5),
            LastRotatedAtUtc = DateTime.UtcNow,
            PreviousTokenSessionId = refreshTokenSession.Id,
            IpAddress = ipAddress,
            UserAgent = userAgent,
        };

        await _refreshTokenSessionRepository.AddAsync(newRefreshTokenSession, cancellationToken);

        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.refresh_token_rotated",
            "Information",
            true,
            user.Id,
            user.Username,
            null,
            null,
            cancellationToken
        );

        var allowedMfaMethods = await _mfaService.GetAllowedMethodsAsync(user.Id, cancellationToken);

        return Result<LoginResponse>.Success(
            new LoginResponse
            {
                Status = "Authenticated",
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                ExpiresIn = 15 * 60,
                AllowedMfaMethods = allowedMfaMethods,
            },
            "Tokens refreshed successfully."
        );
    }
}
