using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;

namespace Authentication.Fido2.Data.Repositories.Implementations;

public class UserMfaMethodRepository : IUserMfaMethodRepository
{
    private readonly ApplicationDbContext _context;

    public UserMfaMethodRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<List<UserMfaMethod>> GetEnabledByUserIdAsync(long userId, CancellationToken cancellationToken)
    {
        return _context
            .UserMfaMethods.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsEnabled)
            .ToListAsync(cancellationToken);
    }

    public Task<UserMfaMethod?> GetEnabledByUserIdAndMethodAsync(
        long userId,
        string method,
        CancellationToken cancellationToken
    )
    {
        return _context.UserMfaMethods.AsNoTracking().FirstOrDefaultAsync(
            x => x.UserId == userId && x.Method == method && x.IsEnabled,
            cancellationToken
        );
    }
}
