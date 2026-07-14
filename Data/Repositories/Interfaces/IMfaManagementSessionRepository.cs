using Authentication.Fido2.Entities;

namespace Authentication.Fido2.Data.Repositories.Interfaces;

public interface IMfaManagementSessionRepository
{
    Task AddAsync(MfaManagementSession session, CancellationToken cancellationToken);
    Task<MfaManagementSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task UpdateAsync(MfaManagementSession session, CancellationToken cancellationToken);
    Task<bool> HasActiveStepUpSessionAsync(long userId, DateTime sinceUtc, CancellationToken cancellationToken);
}
