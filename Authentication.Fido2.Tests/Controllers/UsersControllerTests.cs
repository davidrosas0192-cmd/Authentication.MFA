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
}