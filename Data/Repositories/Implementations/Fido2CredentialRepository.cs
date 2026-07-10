using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;

namespace Authentication.Fido2.Data.Repositories.Interfaces;

public class Fido2CredentialRepository : IFido2CredentialRepository
{
    private readonly ApplicationDbContext _dbContext;

    public Fido2CredentialRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<List<UserFido2Credential>> GetByUserIdAsync(
        long userId,
        CancellationToken cancellationToken
    )
    {
        return _dbContext
            .UserFido2Credentials.AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public Task<UserFido2Credential?> GetByCredentialIdAsync(
        byte[] credentialId,
        CancellationToken cancellationToken
    )
    {
        return _dbContext.UserFido2Credentials.FirstOrDefaultAsync(
            x => x.CredentialId == credentialId,
            cancellationToken
        );
    }

    public Task<List<UserFido2Credential>> GetByUserHandleAsync(
        byte[] userHandle,
        CancellationToken cancellationToken
    )
    {
        return _dbContext
            .UserFido2Credentials.AsNoTracking()
            .Where(x => x.UserHandle == userHandle)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> CredentialIdExistsAsync(
        byte[] credentialId,
        CancellationToken cancellationToken
    )
    {
        return _dbContext.UserFido2Credentials.AnyAsync(
            x => x.CredentialId == credentialId,
            cancellationToken
        );
    }

    public async Task AddAsync(UserFido2Credential credential, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);

        _dbContext.UserFido2Credentials.Add(credential);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        UserFido2Credential credential,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(credential);

        _dbContext.UserFido2Credentials.Update(credential);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
