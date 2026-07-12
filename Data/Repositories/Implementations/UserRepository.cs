using Authentication.Fido2.Data;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;

namespace Authentication.Fido2.Data.Repositories.Implementations;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext applicationDbContext)
    {
        _context = applicationDbContext;
    }

    public Task<List<User>> ListAllAsync(CancellationToken cancellationToken)
    {
        return _context.Users.AsNoTracking().OrderBy(u => u.Id).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await _context.Users.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        User response = await _context
            .Users.AsNoTracking()
            .Where(q => q.Email == email)
            .FirstOrDefaultAsync(cancellationToken);

        return response;
    }

    public Task<User?> GetByIdAsync(long userId, CancellationToken cancellationToken)
    {
        return _context
            .Users.AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == userId, cancellationToken);
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        return _context
            .Users.AsNoTracking()
            .FirstOrDefaultAsync(q => q.Username == username, cancellationToken);
    }

    public Task<int> CountActiveByRoleAsync(string role, CancellationToken cancellationToken)
    {
        return _context.Users.AsNoTracking().CountAsync(
            q => q.IsActive && q.Role == role,
            cancellationToken
        );
    }

    public Task<User?> GetByUsernameOrEmailAsync(
        string usernameOrEmail,
        CancellationToken cancellationToken
    )
    {
        return _context
            .Users.AsNoTracking()
            .FirstOrDefaultAsync(
                q => q.Username == usernameOrEmail || q.Email == usernameOrEmail,
                cancellationToken
            );
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        _context.Users.Update(user);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task EnableFido2MfaAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        user.IsFido2MfaEnabled = true;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DisableFido2MfaAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        user.IsFido2MfaEnabled = false;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
