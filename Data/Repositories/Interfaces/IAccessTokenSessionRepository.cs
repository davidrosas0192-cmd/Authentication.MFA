using Authentication.Fido2.Entities;

namespace Authentication.Fido2.Data.Repositories.Interfaces;

public interface IAccessTokenSessionRepository
{
    Task AddAsync(AccessTokenSession session, CancellationToken cancellationToken);
    Task<AccessTokenSession?> GetActiveByJtiAsync(string tokenJti, CancellationToken cancellationToken);
    Task RevokeByJtiAsync(string tokenJti, string reason, CancellationToken cancellationToken);
    Task RevokeAllActiveByUserAsync(long userId, string reason, CancellationToken cancellationToken);
}
