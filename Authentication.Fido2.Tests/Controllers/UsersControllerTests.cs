using Authentication.Fido2.Common;
using Authentication.Fido2.Controllers;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Authentication.Fido2.Tests.Controllers;

public class UsersControllerTests
{
    [Fact]
    public async Task CreateUser_ReturnsOk_WhenServiceSucceeds()
    {
        var service = new RecordingUserRegistrationService
        {
            ResultFactory = () => Result<CreateUserResponse>.Success(new CreateUserResponse
            {
                UserId = 12,
                Username = "demo",
                Email = "demo@example.com",
                Role = "User",
            }),
        };

        var controller = new UsersController(service, NullLogger<UsersController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ControllerTestHelpers.CreateHttpContext() },
        };

        var result = await controller.CreateUser(
            new CreateUserRequest { Username = "demo", Email = "demo@example.com", Password = "Demo123!" },
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        Assert.Equal(1, service.CallCount);
        Assert.Equal("127.0.0.1", service.LastIpAddress);
        Assert.Equal("Test-Agent/1.0", service.LastUserAgent);
    }

    [Fact]
    public async Task CreateUser_ReturnsStatusCode_WhenServiceFails()
    {
        var service = new RecordingUserRegistrationService
        {
            ResultFactory = () => Result<CreateUserResponse>.Failure("duplicate user", 400),
        };

        var controller = new UsersController(service, NullLogger<UsersController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ControllerTestHelpers.CreateHttpContext() },
        };

        var result = await controller.CreateUser(
            new CreateUserRequest { Username = "demo", Email = "demo@example.com", Password = "Demo123!" },
            CancellationToken.None
        );

        var objectResult = ControllerTestHelpers.AssertObjectResult(result, 400);
        Assert.NotNull(objectResult.Value);
        Assert.Equal(1, service.CallCount);
    }

    [Fact]
    public async Task CreateUserByAdmin_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var service = new RecordingUserRegistrationService
        {
            ResultFactory = () => Result<CreateUserResponse>.Success(new CreateUserResponse
            {
                UserId = 12,
                Username = "demo",
                Email = "demo@example.com",
                Role = "Support",
            }),
        };

        var controller = new UsersController(service, NullLogger<UsersController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ControllerTestHelpers.CreateHttpContext() },
        };

        var result = await controller.CreateUserByAdmin(
            new CreateUserRequest { Username = "demo", Email = "demo@example.com", Password = "Demo123!", Role = "support" },
            CancellationToken.None
        );

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(0, service.AdminCallCount);
    }

    [Fact]
    public async Task CreateUserByAdmin_ReturnsOk_WhenTokenIsValid()
    {
        var service = new RecordingUserRegistrationService
        {
            ResultFactory = () => Result<CreateUserResponse>.Success(new CreateUserResponse
            {
                UserId = 12,
                Username = "demo",
                Email = "demo@example.com",
                Role = "Support",
            }),
        };

        var controller = new UsersController(service, NullLogger<UsersController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42)),
            },
        };

        var result = await controller.CreateUserByAdmin(
            new CreateUserRequest { Username = "demo", Email = "demo@example.com", Password = "Demo123!", Role = "support" },
            CancellationToken.None
        );

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.AdminCallCount);
        Assert.Equal(42, service.LastActorUserId);
    }

    [Fact]
    public async Task GetAdminUsers_ReturnsOk_WhenServiceSucceeds()
    {
        var service = new RecordingUserRegistrationService
        {
            ListResultFactory = () => Result<AdminUsersListResponse>.Success(new AdminUsersListResponse
            {
                Users =
                [
                    new AdminUserSummaryResponse
                    {
                        UserId = 1,
                        Username = "admin",
                        Email = "admin@example.com",
                        Role = "Admin",
                        IsActive = true,
                        CreatedAtUtc = DateTime.UtcNow,
                    },
                ],
            }),
        };

        var controller = new UsersController(service, NullLogger<UsersController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ControllerTestHelpers.CreateHttpContext() },
        };

        var result = await controller.GetAdminUsers(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.GetUsersCallCount);
    }

    [Fact]
    public async Task UpdateUserRole_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var service = new RecordingUserRegistrationService
        {
            AdminUserResultFactory = () => Result<AdminUserSummaryResponse>.Success(new AdminUserSummaryResponse
            {
                UserId = 2,
                Username = "agent",
                Email = "agent@example.com",
                Role = "Support",
                IsActive = true,
            }),
        };

        var controller = new UsersController(service, NullLogger<UsersController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ControllerTestHelpers.CreateHttpContext() },
        };

        var result = await controller.UpdateUserRole(2, new UpdateUserRoleRequest { Role = "support" }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(0, service.UpdateRoleCallCount);
    }

    [Fact]
    public async Task UpdateUserRole_ReturnsOk_WhenTokenIsValid()
    {
        var service = new RecordingUserRegistrationService
        {
            AdminUserResultFactory = () => Result<AdminUserSummaryResponse>.Success(new AdminUserSummaryResponse
            {
                UserId = 2,
                Username = "agent",
                Email = "agent@example.com",
                Role = "Support",
                IsActive = true,
            }),
        };

        var controller = new UsersController(service, NullLogger<UsersController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42)),
            },
        };

        var result = await controller.UpdateUserRole(2, new UpdateUserRoleRequest { Role = "support" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.UpdateRoleCallCount);
        Assert.Equal(42, service.LastActorUserId);
        Assert.Equal(2, service.LastTargetUserId);
    }

    [Fact]
    public async Task UpdateUserStatus_ReturnsOk_WhenTokenIsValid()
    {
        var service = new RecordingUserRegistrationService
        {
            AdminUserResultFactory = () => Result<AdminUserSummaryResponse>.Success(new AdminUserSummaryResponse
            {
                UserId = 2,
                Username = "agent",
                Email = "agent@example.com",
                Role = "Support",
                IsActive = false,
            }),
        };

        var controller = new UsersController(service, NullLogger<UsersController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42)),
            },
        };

        var result = await controller.UpdateUserStatus(2, new UpdateUserStatusRequest { IsActive = false }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.UpdateStatusCallCount);
        Assert.Equal(42, service.LastActorUserId);
        Assert.Equal(2, service.LastTargetUserId);
    }
}