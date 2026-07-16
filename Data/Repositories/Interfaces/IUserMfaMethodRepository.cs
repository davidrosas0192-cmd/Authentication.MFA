using Authentication.Fido2.Entities;

namespace Authentication.Fido2.Data.Repositories.Interfaces;

public interface IUserMfaMethodRepository
{
    Task<List<UserMfaMethod>> GetEnabledByUserIdAsync(long userId, CancellationToken cancellationToken);
    Task<List<UserMfaMethod>> GetByUserIdAsync(long userId, CancellationToken cancellationToken);
    Task<UserMfaMethod?> GetEnabledByUserIdAndMethodAsync(
        long userId,
        string method,
        CancellationToken cancellationToken
    );
    Task<UserMfaMethod?> GetByUserIdAndMethodAsync(
        long userId,
        string method,
        CancellationToken cancellationToken
    );
    Task<bool> IsContactValueInUseAsync(
        string contactValue,
        string method,
        long excludeUserId,
        CancellationToken cancellationToken
    );
    Task AddAsync(UserMfaMethod method, CancellationToken cancellationToken);
    Task UpdateAsync(UserMfaMethod method, CancellationToken cancellationToken);
}
