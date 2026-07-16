using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.Entities;
using Authentication.Fido2.Options;
using Authentication.Fido2.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Authentication.Fido2.Services.Implementations;

public class SessionFactory : ISessionFactory
{
    private readonly ITokenService _tokenService;
    private readonly IAccessTokenSessionRepository _accessTokenSessionRepository;
    private readonly IRefreshTokenSessionRepository _refreshTokenSessionRepository;
    private readonly JwtOptions _jwtOptions;

    public SessionFactory(
        ITokenService tokenService,
        IAccessTokenSessionRepository accessTokenSessionRepository,
        IRefreshTokenSessionRepository refreshTokenSessionRepository,
        IOptions<JwtOptions> jwtOptions
    )
    {
        _tokenService = tokenService;
        _accessTokenSessionRepository = accessTokenSessionRepository;
        _refreshTokenSessionRepository = refreshTokenSessionRepository;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<(string AccessToken, string RefreshToken)> CreateAuthenticatedSessionAsync(
        User user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        var jti = Guid.NewGuid().ToString("N");
        var accessToken = _tokenService.CreateAccessToken(user, jti);
        var refreshToken = _tokenService.CreateRefreshToken();

        var accessSession = new AccessTokenSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenJti = jti,
            IssuedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes),
            IpAddress = ipAddress,
            UserAgent = userAgent,
        };

        await _accessTokenSessionRepository.AddAsync(accessSession, cancellationToken);

        var refreshSession = new RefreshTokenSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = _tokenService.HashRefreshToken(refreshToken),
            AccessTokenSessionId = accessSession.Id,
            IssuedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(5),
            IpAddress = ipAddress,
            UserAgent = userAgent,
        };

        await _refreshTokenSessionRepository.AddAsync(refreshSession, cancellationToken);

        return (accessToken, refreshToken);
    }
}
