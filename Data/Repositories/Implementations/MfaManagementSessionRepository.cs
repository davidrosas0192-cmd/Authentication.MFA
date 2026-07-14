using Authentication.Fido2.Constants;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;

namespace Authentication.Fido2.Data.Repositories.Implementations;

public class MfaManagementSessionRepository : IMfaManagementSessionRepository
{
    private readonly ApplicationDbContext _context;

    public MfaManagementSessionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(MfaManagementSession session, CancellationToken cancellationToken)
    {
        await _context.MfaManagementSessions.AddAsync(session, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<MfaManagementSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _context.MfaManagementSessions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(MfaManagementSession session, CancellationToken cancellationToken)
    {
        _context.MfaManagementSessions.Update(session);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> HasActiveStepUpSessionAsync(long userId, DateTime sinceUtc, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        return _context.MfaManagementSessions.AnyAsync(
            x => x.UserId == userId
                && x.Status == MfaManagementSessionStatuses.StepUpCompleted
                && x.VerifiedAtUtc != null
                && x.VerifiedAtUtc >= sinceUtc
                && x.ExpiresAtUtc > now,
            cancellationToken
        );
    }
}
