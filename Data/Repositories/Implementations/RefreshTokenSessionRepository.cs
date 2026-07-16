using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;

namespace Authentication.Fido2.Data.Repositories.Implementations;

public class RefreshTokenSessionRepository : IRefreshTokenSessionRepository
{
    private readonly ApplicationDbContext _context;

    public RefreshTokenSessionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RefreshTokenSession session, CancellationToken cancellationToken)
    {
        await _context.RefreshTokenSessions.AddAsync(session, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<RefreshTokenSession?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await _context.RefreshTokenSessions.FirstOrDefaultAsync(
            x => x.TokenHash == tokenHash
                && x.RevokedAtUtc == null
                && x.ExpiresAtUtc > now,
            cancellationToken
        );
    }

    public async Task<RefreshTokenSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.RefreshTokenSessions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(RefreshTokenSession session, CancellationToken cancellationToken)
    {
        _context.RefreshTokenSessions.Update(session);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeByIdAsync(Guid id, string reason, CancellationToken cancellationToken)
    {
        var session = await _context.RefreshTokenSessions.FirstOrDefaultAsync(
            x => x.Id == id && x.RevokedAtUtc == null,
            cancellationToken
        );

        if (session != null)
        {
            session.RevokedAtUtc = DateTime.UtcNow;
            session.RevokeReason = reason;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeAllByUserAsync(long userId, string reason, CancellationToken cancellationToken)
    {
        var sessions = await _context.RefreshTokenSessions
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var session in sessions)
        {
            session.RevokedAtUtc = now;
            session.RevokeReason = reason;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<RefreshTokenSession>> GetActiveByUserAsync(long userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await _context.RefreshTokenSessions
            .Where(x => x.UserId == userId
                && x.RevokedAtUtc == null
                && x.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasActiveTokenAsync(long userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await _context.RefreshTokenSessions.AnyAsync(
            x => x.UserId == userId
                && x.RevokedAtUtc == null
                && x.ExpiresAtUtc > now,
            cancellationToken
        );
    }

    public async Task<int> DeleteRevokedSessionsAsync(DateTime olderThanUtc, CancellationToken cancellationToken)
    {
        var count = await _context.RefreshTokenSessions
            .Where(x => x.RevokedAtUtc != null && x.RevokedAtUtc < olderThanUtc)
            .ExecuteDeleteAsync(cancellationToken);
        
        return count;
    }

    public async Task<int> DeleteExpiredSessionsAsync(DateTime olderThanUtc, CancellationToken cancellationToken)
    {
        var count = await _context.RefreshTokenSessions
            .Where(x => x.ExpiresAtUtc < olderThanUtc && x.RevokedAtUtc == null)
            .ExecuteDeleteAsync(cancellationToken);
        
        return count;
    }
}
