using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;

namespace Authentication.Fido2.Data.Repositories.Implementations;

public class MfaTempTokenSessionRepository : IMfaTempTokenSessionRepository
{
    private readonly ApplicationDbContext _context;

    public MfaTempTokenSessionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(MfaTempTokenSession session, CancellationToken cancellationToken)
    {
        await _context.MfaTempTokenSessions.AddAsync(session, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<MfaTempTokenSession?> GetActiveByJtiAsync(string tokenJti, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        return _context.MfaTempTokenSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TokenJti == tokenJti
                    && x.ConsumedAtUtc == null
                    && x.RevokedAtUtc == null
                    && x.ExpiresAtUtc > now,
                cancellationToken
            );
    }

    public async Task ConsumeByTransactionAsync(Guid mfaTransactionId, CancellationToken cancellationToken)
    {
        var sessions = await _context
            .MfaTempTokenSessions.Where(x => x.MfaTransactionId == mfaTransactionId && x.ConsumedAtUtc == null)
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            return;
        }

        var consumedAt = DateTime.UtcNow;
        foreach (var session in sessions)
        {
            session.ConsumedAtUtc = consumedAt;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}