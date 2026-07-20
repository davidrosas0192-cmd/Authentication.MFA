using Authentication.Fido2.Common;
using Authentication.Fido2.Constants;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.DTOs.Mfa;
using Authentication.Fido2.Entities;
using Authentication.Fido2.Options;
using Authentication.Fido2.Services.Implementations;
using Authentication.Fido2.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Authentication.Fido2.Tests.Services;

public class MfaServiceManagementTests
{
    [Fact]
    public async Task RemoveMethodAsync_ReturnsForbidden_WhenStepUpIsMissing()
    {
        var userId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var service = BuildService(new FakeMfaChallengeRepository(), new FakeMfaManagementSessionRepository());

        var result = await service.RemoveMethodAsync(userId, MfaMethodTypes.Sms, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task StartReconfigureMethodAsync_ReturnsForbidden_WhenStepUpIsMissing()
    {
        var userId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var service = BuildService(new FakeMfaChallengeRepository(), new FakeMfaManagementSessionRepository());

        var result = await service.StartReconfigureMethodAsync(
            userId,
            MfaMethodTypes.Email,
            new StartMfaReconfigureRequest { ContactValue = "user@example.com" },
            null,
            null,
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task CompleteManagementSessionAsync_MarksSessionConsumed_WhenSessionIsVerified()
    {
        var userId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var session = new MfaManagementSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = MfaManagementSessionStatuses.StepUpCompleted,
            ContinuationToken = "token-1",
            StepVersion = 2,
            VerifiedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
        };

        var sessionRepository = new FakeMfaManagementSessionRepository(session);
        var service = BuildService(new FakeMfaChallengeRepository(), sessionRepository);

        var result = await service.CompleteManagementSessionAsync(userId, session.Id, "token-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(MfaManagementSessionStatuses.Completed, sessionRepository.StoredSession?.Status);
    }

    [Fact]
    public async Task CancelManagementSessionAsync_MarksSessionRevoked_WhenSessionIsPending()
    {
        var userId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var session = new MfaManagementSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = MfaManagementSessionStatuses.StepUpRequired,
            ContinuationToken = "token-1",
            StepVersion = 1,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
        };

        var sessionRepository = new FakeMfaManagementSessionRepository(session);
        var service = BuildService(new FakeMfaChallengeRepository(), sessionRepository);

        var result = await service.CancelManagementSessionAsync(userId, session.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(MfaManagementSessionStatuses.Cancelled, sessionRepository.StoredSession?.Status);
    }

    [Fact]
    public async Task StartChallengeAsync_ReturnsGone_WhenLoginTransactionIsExpired()
    {
        var userId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var challenge = new MfaChallenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = MfaChallengePurposes.Login,
            Status = MfaChallengeStatuses.PendingSelection,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1),
        };

        var service = BuildService(
            new FakeMfaChallengeRepository(challenge),
            new FakeMfaManagementSessionRepository()
        );

        var result = await service.StartChallengeAsync(
            userId,
            challenge.Id,
            MfaMethodTypes.Sms,
            null,
            null,
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status410Gone, result.StatusCode);
    }

    [Fact]
    public async Task VerifyChallengeAsync_ReturnsGone_WhenLoginChallengeIsExpired()
    {
        var userId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var challenge = new MfaChallenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = MfaChallengePurposes.Login,
            Method = MfaMethodTypes.Sms,
            ContinuationToken = "ct-1",
            Status = MfaChallengeStatuses.Pending,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1),
        };

        var service = BuildService(
            new FakeMfaChallengeRepository(challenge),
            new FakeMfaManagementSessionRepository()
        );

        var result = await service.VerifyChallengeAsync(
            userId,
            challenge.Id,
            "ct-1",
            "123456",
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status410Gone, result.StatusCode);
    }

    [Fact]
    public async Task VerifyEnrollmentAsync_ReturnsForbidden_WhenStepUpIsMissing_BeforeChallengeEvaluation()
    {
        var userId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var challenge = new MfaChallenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = MfaChallengePurposes.Enrollment,
            Method = MfaMethodTypes.Sms,
            ContactValue = "+1234567890",
            ContinuationToken = "ct-2",
            Status = MfaChallengeStatuses.Pending,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1),
        };

        var service = BuildService(
            new FakeMfaChallengeRepository(challenge),
            new FakeMfaManagementSessionRepository()
        );

        var result = await service.VerifyEnrollmentAsync(
            userId,
            new VerifyMfaEnrollmentRequest
            {
                EnrollmentTransactionId = challenge.Id,
                ContinuationToken = "ct-2",
                Code = "123456",
            },
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    private static MfaService BuildService(
        FakeMfaChallengeRepository challengeRepository,
        FakeMfaManagementSessionRepository managementSessionRepository
    )
    {
        return new MfaService(
            new FakeUserMfaMethodRepository(),
            new FakeUserRecoveryCodeRepository(),
            challengeRepository,
            new FakeMfaLoginEnrollmentSessionRepository(),
            managementSessionRepository,
            new FakeUserRepository(),
            new FakeTwilioOtpService(),
            new FakeTokenService(),
            new FakeAccessTokenSessionRepository(),
            new FakeMfaTempTokenSessionRepository(),
            new FakeSessionFactory(),
            new FakeAuditService(),
            Microsoft.Extensions.Options.Options.Create(new JwtOptions
            {
                Issuer = "issuer",
                Audience = "audience",
                SecretKey = "secret",
                AccessTokenExpirationMinutes = 15,
                RefreshTokenExpirationDays = 7,
            })
        );
    }

    private sealed class FakeMfaLoginEnrollmentSessionRepository : IMfaLoginEnrollmentSessionRepository
    {
        public Task AddAsync(MfaLoginEnrollmentSession session, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<MfaLoginEnrollmentSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<MfaLoginEnrollmentSession?>(null);

        public Task<MfaLoginEnrollmentSession?> GetActiveByJtiAsync(string tokenJti, CancellationToken cancellationToken) =>
            Task.FromResult<MfaLoginEnrollmentSession?>(null);

        public Task UpdateAsync(MfaLoginEnrollmentSession session, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeAllActiveByUserAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeMfaChallengeRepository : IMfaChallengeRepository
    {
        public MfaChallenge? StoredChallenge { get; private set; }

        public FakeMfaChallengeRepository(MfaChallenge? seed = null)
        {
            StoredChallenge = seed;
        }

        public Task AddAsync(MfaChallenge challenge, CancellationToken cancellationToken)
        {
            StoredChallenge = challenge;
            return Task.CompletedTask;
        }

        public Task<MfaChallenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(StoredChallenge?.Id == id ? StoredChallenge : null);
        }

        public Task UpdateAsync(MfaChallenge challenge, CancellationToken cancellationToken)
        {
            StoredChallenge = challenge;
            return Task.CompletedTask;
        }

        public Task<bool> HasRecentVerifiedChallengeAsync(
            Guid userId,
            string purpose,
            DateTime sinceUtc,
            CancellationToken cancellationToken
        )
        {
            var hasRecent = StoredChallenge is not null
                && StoredChallenge.UserId == userId
                && string.Equals(StoredChallenge.Purpose, purpose, StringComparison.Ordinal)
                && string.Equals(StoredChallenge.Status, MfaChallengeStatuses.Verified, StringComparison.Ordinal)
                && StoredChallenge.VerifiedAtUtc.HasValue
                && StoredChallenge.VerifiedAtUtc.Value >= sinceUtc
                && StoredChallenge.ExpiresAtUtc > DateTime.UtcNow;

            return Task.FromResult(hasRecent);
        }

        public Task DeleteExpiredChallengesAsync(DateTime beforeUtc, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteLockedChallengesAsync(DateTime beforeUtc, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteCompletedChallengesAsync(DateTime beforeUtc, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeMfaManagementSessionRepository : IMfaManagementSessionRepository
    {
        public MfaManagementSession? StoredSession { get; private set; }

        public FakeMfaManagementSessionRepository(MfaManagementSession? seed = null)
        {
            StoredSession = seed;
        }

        public Task AddAsync(MfaManagementSession session, CancellationToken cancellationToken)
        {
            StoredSession = session;
            return Task.CompletedTask;
        }

        public Task<MfaManagementSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(StoredSession?.Id == id ? StoredSession : null);
        }

        public Task UpdateAsync(MfaManagementSession session, CancellationToken cancellationToken)
        {
            StoredSession = session;
            return Task.CompletedTask;
        }

        public Task<bool> HasActiveStepUpSessionAsync(Guid userId, DateTime sinceUtc, CancellationToken cancellationToken)
        {
            var hasActive = StoredSession is not null
                && StoredSession.UserId == userId
                && string.Equals(StoredSession.Status, MfaManagementSessionStatuses.StepUpCompleted, StringComparison.Ordinal)
                && StoredSession.VerifiedAtUtc != null
                && StoredSession.VerifiedAtUtc >= sinceUtc
                && StoredSession.ExpiresAtUtc > DateTime.UtcNow;

            return Task.FromResult(hasActive);
        }
    }

    private sealed class FakeUserMfaMethodRepository : IUserMfaMethodRepository
    {
        public Task<List<UserMfaMethod>> GetEnabledByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(new List<UserMfaMethod>());

        public Task<List<UserMfaMethod>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(new List<UserMfaMethod>());

        public Task<UserMfaMethod?> GetEnabledByUserIdAndMethodAsync(Guid userId, string method, CancellationToken cancellationToken) =>
            Task.FromResult<UserMfaMethod?>(null);

        public Task<UserMfaMethod?> GetByUserIdAndMethodAsync(Guid userId, string method, CancellationToken cancellationToken) =>
            Task.FromResult<UserMfaMethod?>(null);

        public Task<bool> IsContactValueInUseAsync(string contactValue, string method, Guid excludeUserId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task AddAsync(UserMfaMethod method, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateAsync(UserMfaMethod method, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeUserRecoveryCodeRepository : IUserRecoveryCodeRepository
    {
        public Task<(UserRecoveryCodeBatch? Batch, int RemainingCount)> GetStatusAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<(UserRecoveryCodeBatch?, int)>((null, 0));

        public Task<bool> HasUnusedCodesAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<UserRecoveryCodeBatch> ReplaceBatchAsync(Guid userId, IReadOnlyCollection<string> codeHashes, CancellationToken cancellationToken) =>
            Task.FromResult(new UserRecoveryCodeBatch());

        public Task<bool> TryConsumeCodeAsync(Guid userId, string code, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(new User { Id = userId, Username = "user", Email = "user@example.com", PasswordHash = "hash", IsActive = true });

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken) => Task.FromResult<User?>(null);

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult<User?>(null);

        public Task AddAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken) => Task.FromResult<User?>(null);

        public Task EnableFido2MfaAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DisableFido2MfaAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeTwilioOtpService : ITwilioOtpService
    {
        public Task<string> StartVerificationAsync(string destination, string channel, CancellationToken cancellationToken) =>
            Task.FromResult("sid");

        public Task<bool> CheckVerificationAsync(string destination, string code, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class FakeTokenService : ITokenService
    {
        public string CreateAccessToken(User user, string tokenJti) => "access";

        public string CreateLoginEnrollmentToken(User user, Guid enrollmentSessionId, string tokenJti) => "login-enrollment";

        public string CreateMfaToken(User user, Guid mfaTransactionId, string tokenJti) => "mfa";

        public string CreateRefreshToken() => "refresh";

        public string HashRefreshToken(string token) => token;
    }

    private sealed class FakeAccessTokenSessionRepository : IAccessTokenSessionRepository
    {
        public Task AddAsync(AccessTokenSession session, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<AccessTokenSession?> GetActiveByJtiAsync(string tokenJti, CancellationToken cancellationToken) =>
            Task.FromResult<AccessTokenSession?>(null);

        public Task RevokeByJtiAsync(string tokenJti, string reason, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteRevokedSessionsAsync(DateTime beforeUtc, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeAllActiveByUserAsync(Guid userId, string reason, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeSessionFactory : ISessionFactory
    {
        public Task<(string AccessToken, string RefreshToken)> CreateAuthenticatedSessionAsync(User user, string? ipAddress, string? userAgent, CancellationToken cancellationToken) =>
            Task.FromResult(("access", "refresh"));
    }

    private sealed class FakeMfaTempTokenSessionRepository : IMfaTempTokenSessionRepository
    {
        public Task AddAsync(MfaTempTokenSession session, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<MfaTempTokenSession?> GetActiveByJtiAsync(string tokenJti, CancellationToken cancellationToken) =>
            Task.FromResult<MfaTempTokenSession?>(null);

        public Task ConsumeByTransactionAsync(Guid mfaTransactionId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeByJtiAsync(string tokenJti, string reason, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeAllActiveByUserAsync(Guid userId, string reason, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeAuditService : IAuditService
    {
        public Task TrackAuthenticationEventAsync(
            Guid? userId,
            string? usernameOrEmail,
            string stage,
            string method,
            bool isSuccess,
            string? failureReason,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;

        public Task TrackSecurityEventAsync(
            string category,
            string eventType,
            string severity,
            bool isSuccess,
            Guid? userId,
            string? usernameOrEmail,
            string? failureReason,
            object? details,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;
    }
}
