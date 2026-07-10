using Authentication.Fido2.Common;
using Authentication.Fido2.Controllers;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.DTOs.Fido2;
using Authentication.Fido2.Tests.TestSupport;
using Fido2NetLib;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Authentication.Fido2.Tests.Controllers;

public class Fido2ControllerTests
{
    [Fact]
    public async Task CreateEnrollmentOptions_ReturnsOk_WhenUserIsValid()
    {
        var service = new RecordingFido2MfaService
        {
            CreateEnrollmentOptionsResultToReturn = Result<Fido2OptionsResponse>.Success(new Fido2OptionsResponse
            {
                TransactionId = Guid.NewGuid(),
                Options = new { challenge = "abc" },
            }),
        };
        var auditService = new RecordingAuditService();

        var controller = new Fido2Controller(service, new RecordingMfaTempTokenSessionRepository(), auditService, NullLogger<Fido2Controller>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42)),
            },
        };

        var result = await controller.CreateEnrollmentOptions(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.CreateEnrollmentOptionsCallCount);
        Assert.Equal(42, service.LastUserId);
        Assert.Equal("127.0.0.1", service.LastIpAddress);
    }

    [Fact]
    public async Task CreateEnrollmentOptions_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var controller = new Fido2Controller(new RecordingFido2MfaService(), new RecordingMfaTempTokenSessionRepository(), new RecordingAuditService(), NullLogger<Fido2Controller>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ControllerTestHelpers.CreateHttpContext() },
        };

        var result = await controller.CreateEnrollmentOptions(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task CompleteEnrollment_ReturnsOk_WhenServiceSucceeds()
    {
        var service = new RecordingFido2MfaService
        {
            CompleteEnrollmentResultToReturn = Result<string>.Success("ok"),
        };
        var auditService = new RecordingAuditService();

        var controller = new Fido2Controller(service, new RecordingMfaTempTokenSessionRepository(), auditService, NullLogger<Fido2Controller>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42)),
            },
        };

        var result = await controller.CompleteEnrollment(new CompleteFido2EnrollmentRequest { TransactionId = Guid.NewGuid(), AttestationResponse = new AuthenticatorAttestationRawResponse() }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.CompleteEnrollmentCallCount);
    }

    [Fact]
    public async Task CompleteEnrollment_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var controller = new Fido2Controller(new RecordingFido2MfaService(), new RecordingMfaTempTokenSessionRepository(), new RecordingAuditService(), NullLogger<Fido2Controller>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ControllerTestHelpers.CreateHttpContext() },
        };

        var result = await controller.CompleteEnrollment(new CompleteFido2EnrollmentRequest { TransactionId = Guid.NewGuid(), AttestationResponse = new AuthenticatorAttestationRawResponse() }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task CreateLoginOptions_ReturnsOk_WhenMfaTokenIsValid()
    {
        var transactionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var tokenJti = "mfa-login-jti";
        var service = new RecordingFido2MfaService
        {
            CreateLoginOptionsResultToReturn = Result<Fido2OptionsResponse>.Success(new Fido2OptionsResponse
            {
                TransactionId = Guid.NewGuid(),
                Options = new { challenge = "abc" },
            }),
        };
        var auditService = new RecordingAuditService();

        var repo = new RecordingMfaTempTokenSessionRepository
        {
            ActiveSessionToReturn = new Authentication.Fido2.Entities.MfaTempTokenSession
        {
            UserId = 42,
            MfaTransactionId = transactionId,
            TokenJti = tokenJti,
            IssuedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
        },
        };

        var controller = new Fido2Controller(service, repo, auditService, NullLogger<Fido2Controller>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42, transactionId, "mfa", tokenJti)),
            },
        };

        var result = await controller.CreateLoginOptions(new CreateFido2LoginOptionsRequest { UsernameOrEmail = "demo" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.CreateLoginOptionsCallCount);
        Assert.Equal(42, service.LastUserId);
        Assert.Equal(transactionId, service.LastMfaTransactionId);
    }

    [Fact]
    public async Task CreateLoginOptions_ReturnsUnauthorized_WhenMfaTokenIsMissing()
    {
        var controller = new Fido2Controller(new RecordingFido2MfaService(), new RecordingMfaTempTokenSessionRepository(), new RecordingAuditService(), NullLogger<Fido2Controller>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ControllerTestHelpers.CreateHttpContext() },
        };

        var result = await controller.CreateLoginOptions(new CreateFido2LoginOptionsRequest { UsernameOrEmail = "demo" }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task CompleteLogin_ReturnsOk_AndConsumesToken_WhenMfaTokenIsValid()
    {
        var transactionId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var tokenJti = "mfa-login-jti-2";
        var service = new RecordingFido2MfaService
        {
            CompleteLoginResultToReturn = Result<LoginResponse>.Success(new LoginResponse
            {
                Status = "Authenticated",
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
            }),
        };
        var auditService = new RecordingAuditService();

        var repo = new RecordingMfaTempTokenSessionRepository
        {
            ActiveSessionToReturn = new Authentication.Fido2.Entities.MfaTempTokenSession
        {
            UserId = 42,
            MfaTransactionId = transactionId,
            TokenJti = tokenJti,
            IssuedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
        },
        };

        var controller = new Fido2Controller(service, repo, auditService, NullLogger<Fido2Controller>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42, transactionId, "mfa", tokenJti)),
            },
        };

        var result = await controller.CompleteLogin(new CompleteFido2LoginRequest { TransactionId = Guid.NewGuid(), AssertionResponse = new AuthenticatorAssertionRawResponse() }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.CompleteLoginCallCount);
        Assert.Equal(1, repo.ConsumeCallCount);
    }

    [Fact]
    public async Task CompleteLogin_ReturnsUnauthorized_WhenMfaTokenIsMissing()
    {
        var controller = new Fido2Controller(new RecordingFido2MfaService(), new RecordingMfaTempTokenSessionRepository(), new RecordingAuditService(), NullLogger<Fido2Controller>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ControllerTestHelpers.CreateHttpContext() },
        };

        var result = await controller.CompleteLogin(new CompleteFido2LoginRequest { TransactionId = Guid.NewGuid(), AssertionResponse = new AuthenticatorAssertionRawResponse() }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}