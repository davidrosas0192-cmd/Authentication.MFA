using Authentication.Fido2.Common;
using Authentication.Fido2.Controllers;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Authentication.Fido2.Tests.Controllers;

public class AuthControllerTests
{
    [Fact]
    public async Task Login_ReturnsTokens_WhenServiceSucceeds()
    {
        var service = new RecordingAuthService
        {
            LoginResultFactory = () => Result<LoginResponse>.Success(new LoginResponse
            {
                Status = "Authenticated",
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
                ExpiresIn = 900,
                AllowedMfaMethods = ["sms", "fido2"],
            }),
        };

        var controller = new AuthController(NullLogger<AuthController>.Instance, service)
        {
            ControllerContext = new ControllerContext { HttpContext = ControllerTestHelpers.CreateHttpContext() },
        };

        var result = await controller.Login(
            new LoginRequest { Username = "demo", Password = "Demo123!" },
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        Assert.Equal(1, service.LoginCallCount);
        Assert.Equal("127.0.0.1", service.LastLoginIpAddress);
        Assert.Equal("Test-Agent/1.0", service.LastLoginUserAgent);
    }

    [Fact]
    public async Task Login_ReturnsStatusCode_WhenServiceFails()
    {
        var service = new RecordingAuthService
        {
            LoginResultFactory = () => Result<LoginResponse>.Failure("invalid credentials", 401),
        };

        var controller = new AuthController(NullLogger<AuthController>.Instance, service)
        {
            ControllerContext = new ControllerContext { HttpContext = ControllerTestHelpers.CreateHttpContext() },
        };

        var result = await controller.Login(
            new LoginRequest { Username = "demo", Password = "bad" },
            CancellationToken.None
        );

        var objectResult = ControllerTestHelpers.AssertObjectResult(result, 401);
        Assert.NotNull(objectResult.Value);
        Assert.Equal(1, service.LoginCallCount);
    }

    [Fact]
    public async Task Logout_ReturnsOk_WhenTokenIsValid()
    {
        var service = new RecordingAuthService
        {
            LogoutResultFactory = () => Result.Success("logged out"),
        };

        var controller = new AuthController(NullLogger<AuthController>.Instance, service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42, tokenJti: "jti-logout")),
            },
        };

        var result = await controller.Logout(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.LogoutCallCount);
        Assert.Equal(42, service.LastLogoutUserId);
        Assert.Equal("jti-logout", service.LastLogoutJti);
    }

    [Fact]
    public async Task Logout_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var service = new RecordingAuthService();
        var controller = new AuthController(NullLogger<AuthController>.Instance, service)
        {
            ControllerContext = new ControllerContext { HttpContext = ControllerTestHelpers.CreateHttpContext() },
        };

        var result = await controller.Logout(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(0, service.LogoutCallCount);
    }

    [Fact]
    public async Task CancelAuthentication_ReturnsOk_WhenTokenIsValid()
    {
        var service = new RecordingAuthService
        {
            CancelAuthenticationResultFactory = () => Result.Success("cancelled"),
        };

        var controller = new AuthController(NullLogger<AuthController>.Instance, service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42, tokenJti: "mfa-jti")),
            },
        };

        var result = await controller.CancelAuthentication(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.CancelAuthenticationCallCount);
        Assert.Equal(42, service.LastCancelUserId);
        Assert.Equal("mfa-jti", service.LastCancelJti);
    }

    [Fact]
    public async Task CancelAuthentication_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var service = new RecordingAuthService();
        var controller = new AuthController(NullLogger<AuthController>.Instance, service)
        {
            ControllerContext = new ControllerContext { HttpContext = ControllerTestHelpers.CreateHttpContext() },
        };

        var result = await controller.CancelAuthentication(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(0, service.CancelAuthenticationCallCount);
    }
}