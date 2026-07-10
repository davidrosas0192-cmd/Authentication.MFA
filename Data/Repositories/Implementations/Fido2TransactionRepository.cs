using Authentication.Fido2.Entities;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authentication.Fido2.Data.Repositories.Implementations;

public class Fido2TransactionRepository : IFido2TransactionRepository
{
    private readonly ApplicationDbContext _dbContext;

    public Fido2TransactionRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task AddAsync(Fido2Transaction transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        _dbContext.Fido2Transations.Add(transaction);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Fido2Transaction?> GetByIdAsync(
        Guid transactionId,
        CancellationToken cancellationToken
    )
    {
        return _dbContext.Fido2Transations.FirstOrDefaultAsync(
            x => x.Id == transactionId,
            cancellationToken
        );
    }

    public async Task UpdateAsync(Fido2Transaction transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        _dbContext.Fido2Transations.Update(transaction);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
