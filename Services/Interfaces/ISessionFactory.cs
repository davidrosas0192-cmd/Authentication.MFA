using Authentication.Fido2.Common;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.Entities;
using Authentication.Fido2.Options;
using Microsoft.Extensions.Options;

namespace Authentication.Fido2.Services.Interfaces;

/// <summary>
/// Creates and persists a full authenticated session (AccessTokenSession + RefreshTokenSession).
/// Centralizes the token issuance pattern used across AuthService, MfaService, and Fido2MfaService.
/// </summary>
public interface ISessionFactory
{
    /// <summary>
    /// Issues a new access + refresh token pair and persists both sessions to the database.
    /// </summary>
    Task<(string AccessToken, string RefreshToken)> CreateAuthenticatedSessionAsync(
        User user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
}
