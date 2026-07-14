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

    public Task<MfaLoginEnrollmentSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return _context.MfaLoginEnrollmentSessions.FirstOrDefaultAsync(
            x => x.Id == sessionId,
            cancellationToken
        );
    }

    public Task<MfaLoginEnrollmentSession?> GetActiveByJtiAsync(string tokenJti, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        return _context.MfaLoginEnrollmentSessions.AsNoTracking().FirstOrDefaultAsync(
            x => x.TokenJti == tokenJti
                && x.CompletedAtUtc == null
                && x.ExpiresAtUtc > now
                && x.Status != Constants.MfaLoginEnrollmentSessionStatuses.Cancelled
                && x.Status != Constants.MfaLoginEnrollmentSessionStatuses.Completed,
            cancellationToken
        );
    }

    public async Task UpdateAsync(MfaLoginEnrollmentSession session, CancellationToken cancellationToken)
    {
        _context.MfaLoginEnrollmentSessions.Update(session);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllActiveByUserAsync(long userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var sessions = await _context.MfaLoginEnrollmentSessions
            .Where(x => x.UserId == userId
                && x.CompletedAtUtc == null
                && x.ExpiresAtUtc > now
                && x.Status != Constants.MfaLoginEnrollmentSessionStatuses.Cancelled
                && x.Status != Constants.MfaLoginEnrollmentSessionStatuses.Completed)
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            return;
        }

        foreach (var session in sessions)
        {
            session.Status = Constants.MfaLoginEnrollmentSessionStatuses.Cancelled;
            session.ExpiresAtUtc = now;
            session.UpdatedAtUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}