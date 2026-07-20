using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;

namespace Authentication.Fido2.Data.Repositories.Implementations;

public class MfaChallengeRepository : IMfaChallengeRepository
{
    private readonly ApplicationDbContext _context;

    public MfaChallengeRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(MfaChallenge challenge, CancellationToken cancellationToken)
    {
        await _context.MfaChallenges.AddAsync(challenge, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<MfaChallenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _context.MfaChallenges.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(MfaChallenge challenge, CancellationToken cancellationToken)
    {
        _context.MfaChallenges.Update(challenge);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> HasRecentVerifiedChallengeAsync(
        Guid userId,
        string purpose,
        DateTime sinceUtc,
        CancellationToken cancellationToken
    )
    {
        var now = DateTime.UtcNow;

        return _context.MfaChallenges.AnyAsync(
            x => x.UserId == userId
                && x.Purpose == purpose
                && x.Status == "verified"
                && x.VerifiedAtUtc != null
                && x.VerifiedAtUtc >= sinceUtc
                && x.ExpiresAtUtc > now,
            cancellationToken
        );
    }

    public async Task<int> DeleteExpiredChallengesAsync(DateTime olderThanUtc, CancellationToken cancellationToken)
    {
        var count = await _context.MfaChallenges
            .Where(x => x.ExpiresAtUtc < olderThanUtc && x.Status == "expired")
            .ExecuteDeleteAsync(cancellationToken);
        
        return count;
    }

    public async Task<int> DeleteLockedChallengesAsync(DateTime olderThanUtc, CancellationToken cancellationToken)
    {
        var count = await _context.MfaChallenges
            .Where(x => x.CreatedAtUtc < olderThanUtc && x.Status == "locked")
            .ExecuteDeleteAsync(cancellationToken);
        
        return count;
    }

    public async Task<int> DeleteCompletedChallengesAsync(DateTime olderThanUtc, CancellationToken cancellationToken)
    {
        var count = await _context.MfaChallenges
            .Where(x => x.CreatedAtUtc < olderThanUtc && 
                   (x.Status == "verified" || x.Status == "consumed"))
            .ExecuteDeleteAsync(cancellationToken);
        
        return count;
    }
}
