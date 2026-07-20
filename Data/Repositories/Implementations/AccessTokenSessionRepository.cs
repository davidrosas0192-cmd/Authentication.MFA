using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;

namespace Authentication.Fido2.Data.Repositories.Implementations;

public class AccessTokenSessionRepository : IAccessTokenSessionRepository
{
    private readonly ApplicationDbContext _context;

    public AccessTokenSessionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AccessTokenSession session, CancellationToken cancellationToken)
    {
        await _context.AccessTokenSessions.AddAsync(session, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<AccessTokenSession?> GetActiveByJtiAsync(string tokenJti, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        return _context.AccessTokenSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TokenJti == tokenJti && x.RevokedAtUtc == null && x.ExpiresAtUtc > now,
                cancellationToken
            );
    }

    public async Task RevokeByJtiAsync(string tokenJti, string reason, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await _context.AccessTokenSessions
            .Where(x => x.TokenJti == tokenJti && x.RevokedAtUtc == null && x.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.RevokedAtUtc, now)
                .SetProperty(x => x.RevokeReason, reason), cancellationToken);
    }

    public async Task RevokeAllActiveByUserAsync(Guid userId, string reason, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await _context.AccessTokenSessions
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null && x.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.RevokedAtUtc, now)
                .SetProperty(x => x.RevokeReason, reason), cancellationToken);
    }

    public async Task<int> DeleteRevokedSessionsAsync(DateTime olderThanUtc, CancellationToken cancellationToken)
    {
        var count = await _context.AccessTokenSessions
            .Where(x => x.RevokedAtUtc != null && x.RevokedAtUtc < olderThanUtc)
            .ExecuteDeleteAsync(cancellationToken);
        
        return count;
    }
}
