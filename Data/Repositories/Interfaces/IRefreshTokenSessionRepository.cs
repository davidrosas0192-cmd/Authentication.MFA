using Authentication.Fido2.Entities;

namespace Authentication.Fido2.Data.Repositories.Interfaces;

public interface IRefreshTokenSessionRepository
{
    Task AddAsync(RefreshTokenSession session, CancellationToken cancellationToken);
    Task<RefreshTokenSession?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<RefreshTokenSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task UpdateAsync(RefreshTokenSession session, CancellationToken cancellationToken);
    Task RevokeByIdAsync(Guid id, string reason, CancellationToken cancellationToken);
    Task RevokeAllByUserAsync(long userId, string reason, CancellationToken cancellationToken);
    Task<List<RefreshTokenSession>> GetActiveByUserAsync(long userId, CancellationToken cancellationToken);
    Task<bool> HasActiveTokenAsync(long userId, CancellationToken cancellationToken);
    Task<int> DeleteRevokedSessionsAsync(DateTime olderThanUtc, CancellationToken cancellationToken);
    Task<int> DeleteExpiredSessionsAsync(DateTime olderThanUtc, CancellationToken cancellationToken);
}
