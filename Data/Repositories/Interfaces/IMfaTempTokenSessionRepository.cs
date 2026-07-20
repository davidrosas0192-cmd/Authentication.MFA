using Authentication.Fido2.Entities;

namespace Authentication.Fido2.Data.Repositories.Interfaces;

public interface IMfaTempTokenSessionRepository
{
    Task AddAsync(MfaTempTokenSession session, CancellationToken cancellationToken);
    Task<MfaTempTokenSession?> GetActiveByJtiAsync(string tokenJti, CancellationToken cancellationToken);
    Task ConsumeByTransactionAsync(Guid mfaTransactionId, CancellationToken cancellationToken);
    Task RevokeByJtiAsync(string tokenJti, string reason, CancellationToken cancellationToken);
    Task RevokeAllActiveByUserAsync(Guid userId, string reason, CancellationToken cancellationToken);
}