using Authentication.Fido2.Entities;

namespace Authentication.Fido2.Data.Repositories.Interfaces;

public interface IFido2TransactionRepository
{
    Task AddAsync(Fido2Transaction transaction, CancellationToken cancellationToken);

    Task<Fido2Transaction?> GetByIdAsync(Guid transactionId, CancellationToken cancellationToken);

    Task UpdateAsync(Fido2Transaction transaction, CancellationToken cancellationToken);
}
