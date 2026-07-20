using Authentication.Fido2.Entities;

namespace Authentication.Fido2.Data.Repositories.Interfaces;

public interface IMfaChallengeRepository
{
    Task AddAsync(MfaChallenge challenge, CancellationToken cancellationToken);
    Task<MfaChallenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task UpdateAsync(MfaChallenge challenge, CancellationToken cancellationToken);
    Task<bool> HasRecentVerifiedChallengeAsync(
        Guid userId,
        string purpose,
        DateTime sinceUtc,
        CancellationToken cancellationToken
    );
    Task<int> DeleteExpiredChallengesAsync(DateTime olderThanUtc, CancellationToken cancellationToken);
    Task<int> DeleteLockedChallengesAsync(DateTime olderThanUtc, CancellationToken cancellationToken);
    Task<int> DeleteCompletedChallengesAsync(DateTime olderThanUtc, CancellationToken cancellationToken);
}
