using Authentication.Fido2.Entities;

namespace Authentication.Fido2.Data.Repositories.Interfaces;

public interface IFido2CredentialRepository
{
    Task<List<UserFido2Credential>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken
    );

    Task<UserFido2Credential?> GetByCredentialIdAsync(
        byte[] credentialId,
        CancellationToken cancellationToken
    );

    Task<List<UserFido2Credential>> GetByUserHandleAsync(
        byte[] userHandle,
        CancellationToken cancellationToken
    );
    Task<bool> CredentialIdExistsAsync(byte[] credentialId, CancellationToken cancellationToken);

    Task AddAsync(UserFido2Credential credential, CancellationToken cancellationToken);

    Task UpdateAsync(UserFido2Credential credential, CancellationToken cancellationToken);
}
