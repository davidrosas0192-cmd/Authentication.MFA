using Authentication.Fido2.Common;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;

namespace Authentication.Fido2.Data.Repositories.Implementations;

public class UserRecoveryCodeRepository : IUserRecoveryCodeRepository
{
    private readonly ApplicationDbContext _context;

    public UserRecoveryCodeRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<(UserRecoveryCodeBatch? Batch, int RemainingCount)> GetStatusAsync(
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        var batch = await _context
            .UserRecoveryCodeBatches.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ReplacedAtUtc == null, cancellationToken);

        if (batch is null)
        {
            return (null, 0);
        }

        var remaining = await _context.UserRecoveryCodes.CountAsync(
            x => x.UserId == userId && x.BatchId == batch.Id && x.UsedAtUtc == null,
            cancellationToken
        );

        return (batch, remaining);
    }

    public Task<bool> HasUnusedCodesAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _context.UserRecoveryCodes.AnyAsync(
            x => x.UserId == userId && x.UsedAtUtc == null && x.Batch.ReplacedAtUtc == null,
            cancellationToken
        );
    }

    public async Task<UserRecoveryCodeBatch> ReplaceBatchAsync(
        Guid userId,
        IReadOnlyCollection<string> codeHashes,
        CancellationToken cancellationToken
    )
    {
        if (codeHashes.Count == 0)
        {
            throw new ArgumentException("At least one recovery code is required.", nameof(codeHashes));
        }

        var now = DateTime.UtcNow;

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        var activeBatches = await _context
            .UserRecoveryCodeBatches.Where(x => x.UserId == userId && x.ReplacedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var batch in activeBatches)
        {
            batch.ReplacedAtUtc = now;
        }

        var newBatch = new UserRecoveryCodeBatch
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            IssuedAtUtc = now,
            ReplacedAtUtc = null,
        };

        await _context.UserRecoveryCodeBatches.AddAsync(newBatch, cancellationToken);

        var entities = codeHashes
            .Select(hash => new UserRecoveryCode
            {
                Id = Guid.NewGuid(),
                BatchId = newBatch.Id,
                UserId = userId,
                CodeHash = hash,
                CreatedAtUtc = now,
            })
            .ToList();

        await _context.UserRecoveryCodes.AddRangeAsync(entities, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return newBatch;
    }

    public async Task<bool> TryConsumeCodeAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var activeBatch = await _context
            .UserRecoveryCodeBatches.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ReplacedAtUtc == null, cancellationToken);

        if (activeBatch is null)
        {
            return false;
        }

        var candidates = await _context
            .UserRecoveryCodes.Where(x => x.UserId == userId && x.BatchId == activeBatch.Id && x.UsedAtUtc == null)
            .ToListAsync(cancellationToken);

        var match = candidates.FirstOrDefault(x => PasswordHasher.Verify(code, x.CodeHash));
        if (match is null)
        {
            return false;
        }

        match.UsedAtUtc = DateTime.UtcNow;

        _context.UserRecoveryCodes.Update(match);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
