using Authentication.Fido2.Services.Implementations;
using Authentication.Fido2.Constants;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.Entities;
using Authentication.Fido2.Tests.TestSupport;

namespace Authentication.Fido2.Tests.Services;

public class UserRegistrationServiceTests
{
    [Fact]
    public async Task UpdateUserRole_ReturnsConflict_WhenDemotingLastActiveAdmin()
    {
        var repository = new FakeUserRepository(
            [
                new User { Id = 1, Username = "admin", Email = "admin@example.com", PasswordHash = "hash", Role = UserRoles.Admin, IsActive = true },
            ]
        );

        var service = new UserRegistrationService(repository, new RecordingAuditService());

        var result = await service.UpdateUserRoleAsync(
            actorUserId: 99,
            targetUserId: 1,
            new UpdateUserRoleRequest { Role = UserRoles.User },
            ipAddress: "127.0.0.1",
            userAgent: "Test-Agent",
            cancellationToken: CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task UpdateUserStatus_ReturnsConflict_WhenDeactivatingLastActiveAdmin()
    {
        var repository = new FakeUserRepository(
            [
                new User { Id = 1, Username = "admin", Email = "admin@example.com", PasswordHash = "hash", Role = UserRoles.Admin, IsActive = true },
            ]
        );

        var service = new UserRegistrationService(repository, new RecordingAuditService());

        var result = await service.UpdateUserStatusAsync(
            actorUserId: 99,
            targetUserId: 1,
            new UpdateUserStatusRequest { IsActive = false },
            ipAddress: "127.0.0.1",
            userAgent: "Test-Agent",
            cancellationToken: CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task UpdateUserRole_Succeeds_WhenAnotherActiveAdminExists()
    {
        var repository = new FakeUserRepository(
            [
                new User { Id = 1, Username = "admin1", Email = "admin1@example.com", PasswordHash = "hash", Role = UserRoles.Admin, IsActive = true },
                new User { Id = 2, Username = "admin2", Email = "admin2@example.com", PasswordHash = "hash", Role = UserRoles.Admin, IsActive = true },
            ]
        );

        var service = new UserRegistrationService(repository, new RecordingAuditService());

        var result = await service.UpdateUserRoleAsync(
            actorUserId: 99,
            targetUserId: 1,
            new UpdateUserRoleRequest { Role = UserRoles.Support },
            ipAddress: "127.0.0.1",
            userAgent: "Test-Agent",
            cancellationToken: CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("Support", result.Data.Role);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly List<User> _users;

        public FakeUserRepository(List<User> users)
        {
            _users = users;
        }

        public Task<List<User>> ListAllAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_users.Select(Clone).ToList());
        }

        public Task<User?> GetByIdAsync(long userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_users.FirstOrDefault(x => x.Id == userId));
        }

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
        {
            return Task.FromResult(_users.FirstOrDefault(x => x.Username == username));
        }

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
        {
            return Task.FromResult(_users.FirstOrDefault(x => x.Email == email));
        }

        public Task<int> CountActiveByRoleAsync(string role, CancellationToken cancellationToken)
        {
            return Task.FromResult(_users.Count(x => x.IsActive && x.Role == role));
        }

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            _users.Add(user);
            return Task.CompletedTask;
        }

        public Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken)
        {
            return Task.FromResult(_users.FirstOrDefault(x => x.Username == usernameOrEmail || x.Email == usernameOrEmail));
        }

        public Task EnableFido2MfaAsync(long userId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task DisableFido2MfaAsync(long userId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(User user, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private static User Clone(User user)
        {
            return new User
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                PasswordHash = user.PasswordHash,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAtUtc = user.CreatedAtUtc,
                LastLoginAtUtc = user.LastLoginAtUtc,
            };
        }
    }
}
