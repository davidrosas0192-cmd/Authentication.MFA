using Authentication.Fido2.Common;
using Authentication.Fido2.Controllers;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.DTOs.Mfa;
using Authentication.Fido2.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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

        var result = await controller.VerifyChallenge(new VerifyMfaChallengeRequest { ContinuationToken = "token-1", Code = "123456" }, CancellationToken.None);

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

        var result = await controller.VerifyChallenge(new VerifyMfaChallengeRequest { ContinuationToken = "token-1", Code = "123456" }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task VerifyChallenge_ReturnsProblemDetails_WhenConflictOccurs()
    {
        var transactionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var tokenJti = "mfa-jti-2";
        var service = new RecordingMfaService
        {
            VerifyChallengeResultToReturn = Result<LoginResponse>.Failure(
                "MFA_FLOW_ALREADY_ADVANCED",
                StatusCodes.Status409Conflict
            ),
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

        var result = await controller.VerifyChallenge(new VerifyMfaChallengeRequest { ContinuationToken = "token-1", Code = "123456" }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal("MFA_FLOW_ALREADY_ADVANCED", problem.Extensions["code"]);
    }

    [Fact]
    public async Task VerifyChallenge_ReturnsRetryAfterHeader_FromPolicy_WhenTooManyRequests()
    {
        var transactionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var tokenJti = "mfa-jti-2";
        var service = new RecordingMfaService
        {
            VerifyChallengeResultToReturn = Result<LoginResponse>.Failure(
                "RATE_LIMITED",
                StatusCodes.Status429TooManyRequests
            ),
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

        var controller = new MfaController(
            service,
            repo,
            new RecordingAuditService(),
            NullLogger<MfaController>.Instance,
            Microsoft.Extensions.Options.Options.Create(new Authentication.Fido2.Options.MfaApiPolicyOptions
            {
                RetryAfterSecondsOnTooManyRequests = 61,
            })
        )
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42, transactionId, "mfa", tokenJti)),
            },
        };

        var result = await controller.VerifyChallenge(
            new VerifyMfaChallengeRequest { ContinuationToken = "token-1", Code = "123456" },
            CancellationToken.None
        );

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, objectResult.StatusCode);
        Assert.True(controller.Response.Headers.TryGetValue("Retry-After", out var retryAfter));
        Assert.Equal("61", retryAfter.ToString());
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
                RecoveryCodes = ["ABCD-EFGH-IJKL"],
            }),
        };
    }

    [Fact]
    public async Task StartManagementSession_ReturnsOk_WhenUserIsValid()
    {
        var tx = Guid.NewGuid();
        var service = new RecordingMfaService
        {
            StartManagementSessionResultToReturn = Result<StartMfaManagementSessionResponse>.Success(new StartMfaManagementSessionResponse
            {
                Status = "StepUpRequired",
                MfaTransactionId = tx,
                AvailableMethods = ["sms"],
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            }),
        };

        var controller = new MfaController(service, new RecordingMfaTempTokenSessionRepository(), new RecordingAuditService(), NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42)),
            },
        };

        var result = await controller.StartManagementSession(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.StartManagementSessionCallCount);
    }

    [Fact]
    public async Task StartManagementChallenge_ReturnsOk_WhenUserIsValid()
    {
        var tx = Guid.NewGuid();
        var service = new RecordingMfaService
        {
            StartChallengeResultToReturn = Result<StartMfaChallengeResponse>.Success(new StartMfaChallengeResponse
            {
                MfaTransactionId = tx,
                Method = "sms",
                Status = "pending",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            }),
        };

        var controller = new MfaController(service, new RecordingMfaTempTokenSessionRepository(), new RecordingAuditService(), NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42)),
            },
        };

        var result = await controller.StartManagementChallenge(
            new StartMfaManagementChallengeRequest { MfaTransactionId = tx, ContinuationToken = "step-token-1", Method = "sms" },
            CancellationToken.None
        );

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.StartChallengeCallCount);
        Assert.Equal(tx, service.LastMfaTransactionId);
    }

    [Fact]
    public async Task StartManagementChallenge_ReturnsProblemDetails_WhenConflictOccurs()
    {
        var tx = Guid.NewGuid();
        var service = new RecordingMfaService
        {
            StartChallengeResultToReturn = Result<StartMfaChallengeResponse>.Failure(
                "MFA_FLOW_ALREADY_ADVANCED",
                StatusCodes.Status409Conflict
            ),
        };

        var controller = new MfaController(service, new RecordingMfaTempTokenSessionRepository(), new RecordingAuditService(), NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42)),
            },
        };

        var result = await controller.StartManagementChallenge(
            new StartMfaManagementChallengeRequest { MfaTransactionId = tx, ContinuationToken = "step-token-1", Method = "sms" },
            CancellationToken.None
        );

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);

        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal("MFA_FLOW_ALREADY_ADVANCED", problem.Extensions["code"]);
    }

    [Fact]
    public async Task VerifyManagementChallenge_ReturnsOk_WhenUserIsValid()
    {
        var tx = Guid.NewGuid();
        var service = new RecordingMfaService
        {
            VerifyManagementChallengeResultToReturn = Result<VerifyMfaManagementChallengeResponse>.Success(new VerifyMfaManagementChallengeResponse
            {
                Status = "StepUpCompleted",
                VerifiedAtUtc = DateTime.UtcNow,
                StepUpValidUntilUtc = DateTime.UtcNow.AddMinutes(10),
            }),
        };

        var controller = new MfaController(service, new RecordingMfaTempTokenSessionRepository(), new RecordingAuditService(), NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42)),
            },
        };

        var result = await controller.VerifyManagementChallenge(
            new VerifyMfaManagementChallengeRequest { MfaTransactionId = tx, ContinuationToken = "step-token-2", Code = "123456" },
            CancellationToken.None
        );

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.VerifyManagementChallengeCallCount);
    }

    [Fact]
    public async Task CompleteManagementSession_ReturnsOk_WhenUserIsValid()
    {
        var tx = Guid.NewGuid();
        var service = new RecordingMfaService
        {
            CompleteManagementSessionResultToReturn = Result<CompleteMfaManagementSessionResponse>.Success(new CompleteMfaManagementSessionResponse
            {
                Status = "Completed",
                CompletedAtUtc = DateTime.UtcNow,
            }),
        };

        var controller = new MfaController(service, new RecordingMfaTempTokenSessionRepository(), new RecordingAuditService(), NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42)),
            },
        };

        var result = await controller.CompleteManagementSession(
            new CompleteMfaManagementSessionRequest { MfaTransactionId = tx, ContinuationToken = "step-token-3" },
            CancellationToken.None
        );

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.CompleteManagementSessionCallCount);
        Assert.Equal(tx, service.LastMfaTransactionId);
    }

    [Fact]
    public async Task CancelManagementSession_ReturnsOk_WhenUserIsValid()
    {
        var tx = Guid.NewGuid();
        var service = new RecordingMfaService
        {
            CancelManagementSessionResultToReturn = Result<CancelMfaManagementSessionResponse>.Success(new CancelMfaManagementSessionResponse
            {
                Status = "Cancelled",
                CancelledAtUtc = DateTime.UtcNow,
            }),
        };

        var controller = new MfaController(service, new RecordingMfaTempTokenSessionRepository(), new RecordingAuditService(), NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42)),
            },
        };

        var result = await controller.CancelManagementSession(tx, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.CancelManagementSessionCallCount);
        Assert.Equal(tx, service.LastMfaTransactionId);
    }

    [Fact]
    public async Task StartReconfigureMethod_ReturnsOk_WhenUserIsValid()
    {
        var service = new RecordingMfaService
        {
            StartReconfigureMethodResultToReturn = Result<StartMfaReconfigureResponse>.Success(new StartMfaReconfigureResponse
            {
                ReconfigureTransactionId = Guid.NewGuid(),
                Method = "email",
                Status = "pending",
                ExpiresAtUtc = DateTime.UtcNow,
            }),
        };

        var controller = new MfaController(service, new RecordingMfaTempTokenSessionRepository(), new RecordingAuditService(), NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42)),
            },
        };

        var result = await controller.StartReconfigureMethod(
            "email",
            new StartMfaReconfigureRequest { ContactValue = "user@example.com" },
            CancellationToken.None
        );

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.StartReconfigureMethodCallCount);
        Assert.Equal("email", service.LastMethodRouteValue);
    }

    [Fact]
    public async Task CompleteReconfigureMethod_ReturnsOk_WhenUserIsValid()
    {
        var service = new RecordingMfaService
        {
            CompleteReconfigureMethodResultToReturn = Result<CompleteMfaReconfigureResponse>.Success(new CompleteMfaReconfigureResponse
            {
                Method = "email",
                IsReconfigured = true,
            }),
        };

        var controller = new MfaController(service, new RecordingMfaTempTokenSessionRepository(), new RecordingAuditService(), NullLogger<MfaController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelpers.CreateHttpContext(ControllerTestHelpers.CreateUserPrincipal(42)),
            },
        };

        var result = await controller.CompleteReconfigureMethod(
            "email",
            new CompleteMfaReconfigureRequest { ReconfigureTransactionId = Guid.NewGuid(), Code = "123456" },
            CancellationToken.None
        );

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.CompleteReconfigureMethodCallCount);
        Assert.Equal("email", service.LastMethodRouteValue);
    }
}