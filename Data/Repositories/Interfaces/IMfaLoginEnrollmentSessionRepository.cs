using Authentication.Fido2.Entities;

namespace Authentication.Fido2.Data.Repositories.Interfaces;

public interface IMfaLoginEnrollmentSessionRepository
{
    Task AddAsync(MfaLoginEnrollmentSession session, CancellationToken cancellationToken);
    Task<MfaLoginEnrollmentSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<MfaLoginEnrollmentSession?> GetActiveByJtiAsync(string tokenJti, CancellationToken cancellationToken);
    Task UpdateAsync(MfaLoginEnrollmentSession session, CancellationToken cancellationToken);
    Task RevokeAllActiveByUserAsync(Guid userId, CancellationToken cancellationToken);
}