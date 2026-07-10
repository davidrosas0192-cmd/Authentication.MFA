using Authentication.Fido2.Common;
using Authentication.Fido2.Controllers;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.DTOs.Mfa;
using Authentication.Fido2.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Authentication.Fido2.Tests.Controllers;

public class MfaControllerTests
{
    [Fact]
    public async Task GetMethods_ReturnsAllowedMethods_WhenUserIsValid()
    {
        var service = new RecordingMfaService { AllowedMethodsToReturn = ["sms", "fido2"] };
        var auditService = new RecordingAuditService();

        var controller = new MfaController(service, new RecordingMfaTempTokenSessionRepository(), auditService, NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42)),
            },
        };

        var result = await controller.GetMethods(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        Assert.Equal(1, service.GetAllowedMethodsCallCount);
    }

    [Fact]
    public async Task GetMethods_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var controller = new MfaController(new RecordingMfaService(), new RecordingMfaTempTokenSessionRepository(), new RecordingAuditService(), NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ControllerTestHelpers.CreateHttpContext() },
        };

        var result = await controller.GetMethods(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task GetDevicesAvailable_ReturnsSetupOptions_WhenUserIsValid()
    {
        var service = new RecordingMfaService
        {
            AllowedMethodsToReturn = ["sms"],
            AvailableSetupMethodsToReturn = ["email", "fido2"],
        };
        var auditService = new RecordingAuditService();

        var controller = new MfaController(service, new RecordingMfaTempTokenSessionRepository(), auditService, NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42)),
            },
        };

        var result = await controller.GetDevicesAvailable(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        Assert.Equal(1, service.GetAllowedMethodsCallCount);
        Assert.Equal(1, service.GetAvailableSetupMethodsCallCount);
    }

    [Fact]
    public async Task GetDevicesAvailable_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var controller = new MfaController(new RecordingMfaService(), new RecordingMfaTempTokenSessionRepository(), new RecordingAuditService(), NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ControllerTestHelpers.CreateHttpContext() },
        };

        var result = await controller.GetDevicesAvailable(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task StartChallenge_ReturnsOk_WhenMfaTokenIsValid()
    {
        var transactionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tokenJti = "mfa-jti-1";
        var service = new RecordingMfaService
        {
            StartChallengeResultToReturn = Result<StartMfaChallengeResponse>.Success(new StartMfaChallengeResponse
            {
                MfaTransactionId = transactionId,
                Method = "sms",
                Status = "pending",
                ExpiresAtUtc = DateTime.UtcNow,
            }),
        };

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
        var auditService = new RecordingAuditService();

        var controller = new MfaController(service, repo, auditService, NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42, transactionId, "mfa", tokenJti)),
            },
        };

        var result = await controller.StartChallenge(new StartMfaChallengeRequest { Method = "sms" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.StartChallengeCallCount);
        Assert.Equal(42, service.LastUserId);
        Assert.Equal(transactionId, service.LastMfaTransactionId);
        Assert.Equal("sms", service.LastMethod);
        Assert.Equal("mfa-jti-1", repo.LastJti);
    }

    [Fact]
    public async Task StartChallenge_ReturnsUnauthorized_WhenMfaTokenSessionIsMissing()
    {
        var transactionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tokenJti = "mfa-jti-1";
        var repo = new RecordingMfaTempTokenSessionRepository();

        var service = new RecordingMfaService();
        var controller = new MfaController(service, repo, new RecordingAuditService(), NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42, transactionId, "mfa", tokenJti)),
            },
        };

        var result = await controller.StartChallenge(new StartMfaChallengeRequest { Method = "sms" }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(0, service.StartChallengeCallCount);
    }

    [Fact]
    public async Task VerifyChallenge_ReturnsOk_AndConsumesToken_WhenTokenIsValid()
    {
        var transactionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var tokenJti = "mfa-jti-2";
        var service = new RecordingMfaService
        {
            VerifyChallengeResultToReturn = Result<LoginResponse>.Success(new LoginResponse
            {
                Status = "Authenticated",
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
            }),
        };

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

        var controller = new MfaController(service, repo, new RecordingAuditService(), NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42, transactionId, "mfa", tokenJti)),
            },
        };

        var result = await controller.VerifyChallenge(new VerifyMfaChallengeRequest { Code = "123456" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.VerifyChallengeCallCount);
        Assert.Equal(1, repo.ConsumeCallCount);
        Assert.Equal(transactionId, repo.LastConsumedTransactionId);
    }

    [Fact]
    public async Task VerifyChallenge_ReturnsUnauthorized_WhenMfaTokenIsInvalid()
    {
        var controller = new MfaController(new RecordingMfaService(), new RecordingMfaTempTokenSessionRepository(), new RecordingAuditService(), NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ControllerTestHelpers.CreateHttpContext() },
        };

        var result = await controller.VerifyChallenge(new VerifyMfaChallengeRequest { Code = "123456" }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task StartEnrollment_ReturnsOk_WhenUserIsValid()
    {
        var service = new RecordingMfaService
        {
            StartEnrollmentResultToReturn = Result<StartMfaEnrollmentResponse>.Success(new StartMfaEnrollmentResponse
            {
                EnrollmentTransactionId = Guid.NewGuid(),
                Method = "sms",
                Status = "pending",
                ExpiresAtUtc = DateTime.UtcNow,
            }),
        };
        var auditService = new RecordingAuditService();

        var controller = new MfaController(service, new RecordingMfaTempTokenSessionRepository(), auditService, NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42)),
            },
        };

        var result = await controller.StartEnrollment(new StartMfaEnrollmentRequest { Method = "sms", ContactValue = "+15555550100" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.StartEnrollmentCallCount);
        Assert.Equal("sms", service.LastStartEnrollmentRequest?.Method);
    }

    [Fact]
    public async Task StartEnrollment_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var controller = new MfaController(new RecordingMfaService(), new RecordingMfaTempTokenSessionRepository(), new RecordingAuditService(), NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ControllerTestHelpers.CreateHttpContext() },
        };

        var result = await controller.StartEnrollment(new StartMfaEnrollmentRequest { Method = "sms", ContactValue = "+15555550100" }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task VerifyEnrollment_ReturnsOk_WhenUserIsValid()
    {
        var service = new RecordingMfaService
        {
            VerifyEnrollmentResultToReturn = Result<VerifyMfaEnrollmentResponse>.Success(new VerifyMfaEnrollmentResponse
            {
                Method = "sms",
                IsVerified = true,
            }),
        };
        var auditService = new RecordingAuditService();

        var controller = new MfaController(service, new RecordingMfaTempTokenSessionRepository(), auditService, NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42)),
            },
        };

        var result = await controller.VerifyEnrollment(new VerifyMfaEnrollmentRequest { EnrollmentTransactionId = Guid.NewGuid(), Code = "123456" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.VerifyEnrollmentCallCount);
    }

    [Fact]
    public async Task VerifyEnrollment_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var controller = new MfaController(new RecordingMfaService(), new RecordingMfaTempTokenSessionRepository(), new RecordingAuditService(), NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ControllerTestHelpers.CreateHttpContext() },
        };

        var result = await controller.VerifyEnrollment(new VerifyMfaEnrollmentRequest { EnrollmentTransactionId = Guid.NewGuid(), Code = "123456" }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}