using Authentication.Fido2.Entities;

namespace Authentication.Fido2.Data.Repositories.Interfaces;

public interface IUserMfaMethodRepository
{
    Task<List<UserMfaMethod>> GetEnabledByUserIdAsync(long userId, CancellationToken cancellationToken);
    Task<UserMfaMethod?> GetEnabledByUserIdAndMethodAsync(
        long userId,
        string method,
        CancellationToken cancellationToken
    );
}
