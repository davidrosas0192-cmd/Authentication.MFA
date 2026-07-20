using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;
using Authentication.Fido2.Constants;

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
        await _context.MfaSessions.AddAsync(
            new MfaSession
            {
                Id = session.Id,
                UserId = session.UserId,
                SessionType = MfaSessionTypes.LoginEnrollment,
                Status = session.Status,
                ContinuationToken = session.ContinuationToken,
                StepVersion = session.StepVersion,
                TokenJti = session.TokenJti,
                ChallengeId = session.ChallengeId,
                ExpiresAtUtc = session.ExpiresAtUtc,
                CompletedAtUtc = session.CompletedAtUtc,
                CreatedAtUtc = session.CreatedAtUtc,
                ModifiedAtUtc = session.ModifiedAtUtc,
            },
            cancellationToken
        );
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<MfaLoginEnrollmentSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _context.MfaSessions.FirstOrDefaultAsync(
            x => x.SessionType == MfaSessionTypes.LoginEnrollment && x.Id == sessionId,
            cancellationToken
        );

        return session is null ? null : MapToLoginEnrollmentSession(session);
    }

    public async Task<MfaLoginEnrollmentSession?> GetActiveByJtiAsync(string tokenJti, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var session = await _context.MfaSessions.AsNoTracking().FirstOrDefaultAsync(
            x => x.SessionType == MfaSessionTypes.LoginEnrollment
                && x.TokenJti == tokenJti
                && x.CompletedAtUtc == null
                && x.ExpiresAtUtc > now
                && x.Status != MfaLoginEnrollmentSessionStatuses.Cancelled
                && x.Status != MfaLoginEnrollmentSessionStatuses.Completed,
            cancellationToken
        );

        return session is null ? null : MapToLoginEnrollmentSession(session);
    }

    public async Task UpdateAsync(MfaLoginEnrollmentSession session, CancellationToken cancellationToken)
    {
        var persisted = await _context.MfaSessions.FirstOrDefaultAsync(
            x => x.SessionType == MfaSessionTypes.LoginEnrollment && x.Id == session.Id,
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

    public async Task RevokeAllActiveByUserAsync(long userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var sessions = await _context.MfaSessions
            .Where(x => x.SessionType == MfaSessionTypes.LoginEnrollment
                && x.UserId == userId
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

    private static MfaLoginEnrollmentSession MapToLoginEnrollmentSession(MfaSession session)
    {
        return new MfaLoginEnrollmentSession
        {
            Id = session.Id,
            UserId = session.UserId,
            Status = session.Status ?? MfaLoginEnrollmentSessionStatuses.EnrollmentRequired,
            ContinuationToken = session.ContinuationToken ?? string.Empty,
            StepVersion = session.StepVersion ?? 0,
            TokenJti = session.TokenJti,
            ChallengeId = session.ChallengeId,
            ExpiresAtUtc = session.ExpiresAtUtc,
            CompletedAtUtc = session.CompletedAtUtc,
            CreatedAtUtc = session.CreatedAtUtc,
            ModifiedAtUtc = session.ModifiedAtUtc,
        };
    }
}