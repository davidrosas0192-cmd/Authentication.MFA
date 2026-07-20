using Authentication.Fido2.Entities;

namespace Authentication.Fido2.Data.Repositories.Interfaces;

public interface IUserMfaMethodRepository
{
    Task<List<UserMfaMethod>> GetEnabledByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<List<UserMfaMethod>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<UserMfaMethod?> GetEnabledByUserIdAndMethodAsync(
        Guid userId,
        string method,
        CancellationToken cancellationToken
    );
    Task<UserMfaMethod?> GetByUserIdAndMethodAsync(
        Guid userId,
        string method,
        CancellationToken cancellationToken
    );
    Task<bool> IsContactValueInUseAsync(
        string contactValue,
        string method,
        Guid excludeUserId,
        CancellationToken cancellationToken
    );
    Task AddAsync(UserMfaMethod method, CancellationToken cancellationToken);
    Task UpdateAsync(UserMfaMethod method, CancellationToken cancellationToken);
}
