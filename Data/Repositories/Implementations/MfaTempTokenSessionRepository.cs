using Authentication.Fido2.Constants;
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
        await _context.MfaTokenSessions.AddAsync(
            new MfaSession
            {
                Id = session.Id,
                UserId = session.UserId,
                SessionType = MfaSessionTypes.TempToken,
                TokenJti = session.TokenJti,
                ExpiresAtUtc = session.ExpiresAtUtc,
                CreatedAtUtc = session.IssuedAtUtc,
                ModifiedAtUtc = session.IssuedAtUtc,
                MfaTransactionId = session.MfaTransactionId,
                IssuedAtUtc = session.IssuedAtUtc,
                ConsumedAtUtc = session.ConsumedAtUtc,
                RevokedAtUtc = session.RevokedAtUtc,
                IpAddress = session.IpAddress,
                UserAgent = session.UserAgent,
            },
            cancellationToken
        );
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<MfaTempTokenSession?> GetActiveByJtiAsync(
        string tokenJti,
        CancellationToken cancellationToken
    )
    {
        var now = DateTime.UtcNow;

        var session = await _context
            .MfaTokenSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.SessionType == MfaSessionTypes.TempToken
                    && x.TokenJti == tokenJti
                    && x.ConsumedAtUtc == null
                    && x.RevokedAtUtc == null
                    && x.ExpiresAtUtc > now,
                cancellationToken
            );

        if (session is null)
        {
            return null;
        }

        return new MfaTempTokenSession
        {
            Id = session.Id,
            UserId = session.UserId,
            MfaTransactionId = session.MfaTransactionId ?? Guid.Empty,
            TokenJti = session.TokenJti,
            IssuedAtUtc = session.IssuedAtUtc ?? session.CreatedAtUtc,
            ExpiresAtUtc = session.ExpiresAtUtc,
            ConsumedAtUtc = session.ConsumedAtUtc,
            RevokedAtUtc = session.RevokedAtUtc,
            IpAddress = session.IpAddress,
            UserAgent = session.UserAgent,
        };
    }

    public async Task ConsumeByTransactionAsync(Guid mfaTransactionId, CancellationToken cancellationToken)
    {
        var sessions = await _context
            .MfaTokenSessions.Where(
                x =>
                    x.SessionType == MfaSessionTypes.TempToken
                    && x.MfaTransactionId == mfaTransactionId
                    && x.ConsumedAtUtc == null
            )
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

    public async Task RevokeByJtiAsync(string tokenJti, string reason, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var sessions = await _context
            .MfaTokenSessions.Where(
                x =>
                    x.SessionType == MfaSessionTypes.TempToken
                    && x.TokenJti == tokenJti
                    && x.ConsumedAtUtc == null
                    && x.RevokedAtUtc == null
                    && x.ExpiresAtUtc > now
            )
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            return;
        }

        foreach (var session in sessions)
        {
            session.RevokedAtUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllActiveByUserAsync(long userId, string reason, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var sessions = await _context
            .MfaTokenSessions.Where(
                x =>
                    x.SessionType == MfaSessionTypes.TempToken
                    && x.UserId == userId
                    && x.ConsumedAtUtc == null
                    && x.RevokedAtUtc == null
                    && x.ExpiresAtUtc > now
            )
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            return;
        }

        foreach (var session in sessions)
        {
            session.RevokedAtUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}