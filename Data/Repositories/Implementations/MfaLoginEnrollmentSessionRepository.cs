using Authentication.Fido2.Constants;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;

namespace Authentication.Fido2.Data.Repositories.Implementations;

public class MfaLoginEnrollmentSessionRepository : IMfaLoginEnrollmentSessionRepository
{
    private readonly ApplicationDbContext _context;

    public MfaLoginEnrollmentSessionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(MfaLoginEnrollmentSession session, CancellationToken cancellationToken)
    {
        await _context.MfaLoginEnrollmentSessions.AddAsync(session, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<MfaLoginEnrollmentSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return await _context.MfaLoginEnrollmentSessions.FirstOrDefaultAsync(
            x => x.Id == sessionId,
            cancellationToken
        );
    }

    public async Task<MfaLoginEnrollmentSession?> GetActiveByJtiAsync(string tokenJti, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        return await _context.MfaLoginEnrollmentSessions.AsNoTracking().FirstOrDefaultAsync(
            x => x.TokenJti == tokenJti
                && x.CompletedAtUtc == null
                && x.ExpiresAtUtc > now
                && x.Status != MfaLoginEnrollmentSessionStatuses.Cancelled
                && x.Status != MfaLoginEnrollmentSessionStatuses.Completed,
            cancellationToken
        );
    }

    public async Task UpdateAsync(MfaLoginEnrollmentSession session, CancellationToken cancellationToken)
    {
        var persisted = await _context.MfaLoginEnrollmentSessions.FirstOrDefaultAsync(
            x => x.Id == session.Id,
            cancellationToken
        );

        if (persisted is null)
        {
            throw new InvalidOperationException("Login enrollment session not found.");
        }

        persisted.Status = session.Status;
        persisted.ContinuationToken = session.ContinuationToken;
        persisted.StepVersion = session.StepVersion;
        persisted.TokenJti = session.TokenJti;
        persisted.ChallengeId = session.ChallengeId;
        persisted.ExpiresAtUtc = session.ExpiresAtUtc;
        persisted.CompletedAtUtc = session.CompletedAtUtc;
        persisted.ModifiedAtUtc = session.ModifiedAtUtc;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllActiveByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var sessions = await _context.MfaLoginEnrollmentSessions
            .Where(x => x.UserId == userId
                && x.CompletedAtUtc == null
                && x.ExpiresAtUtc > now
                && x.Status != MfaLoginEnrollmentSessionStatuses.Cancelled
                && x.Status != MfaLoginEnrollmentSessionStatuses.Completed)
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            return;
        }

        foreach (var session in sessions)
        {
            session.Status = MfaLoginEnrollmentSessionStatuses.Cancelled;
            session.ExpiresAtUtc = now;
            session.ModifiedAtUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
