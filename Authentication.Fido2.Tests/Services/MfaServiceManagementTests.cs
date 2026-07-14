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
        var service = BuildService(new FakeMfaChallengeRepository(), new FakeMfaManagementSessionRepository());

        var result = await service.RemoveMethodAsync(42, MfaMethodTypes.Sms, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task StartReconfigureMethodAsync_ReturnsForbidden_WhenStepUpIsMissing()
    {
        var service = BuildService(new FakeMfaChallengeRepository(), new FakeMfaManagementSessionRepository());

        var result = await service.StartReconfigureMethodAsync(
            42,
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
        var session = new MfaManagementSession
        {
            Id = Guid.NewGuid(),
            UserId = 42,
            Status = MfaManagementSessionStatuses.StepUpCompleted,
            ContinuationToken = "token-1",
            StepVersion = 2,
            VerifiedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
        };

        var sessionRepository = new FakeMfaManagementSessionRepository(session);
        var service = BuildService(new FakeMfaChallengeRepository(), sessionRepository);

        var result = await service.CompleteManagementSessionAsync(42, session.Id, "token-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(MfaManagementSessionStatuses.Completed, sessionRepository.StoredSession?.Status);
    }

    [Fact]
    public async Task CancelManagementSessionAsync_MarksSessionRevoked_WhenSessionIsPending()
    {
        var session = new MfaManagementSession
        {
            Id = Guid.NewGuid(),
            UserId = 42,
            Status = MfaManagementSessionStatuses.StepUpRequired,
            ContinuationToken = "token-1",
            StepVersion = 1,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
        };

        var sessionRepository = new FakeMfaManagementSessionRepository(session);
        var service = BuildService(new FakeMfaChallengeRepository(), sessionRepository);

        var result = await service.CancelManagementSessionAsync(42, session.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(MfaManagementSessionStatuses.Cancelled, sessionRepository.StoredSession?.Status);
    }

    [Fact]
    public async Task StartChallengeAsync_ReturnsGone_WhenLoginTransactionIsExpired()
    {
        var challenge = new MfaChallenge
        {
            Id = Guid.NewGuid(),
            UserId = 42,
            Purpose = MfaChallengePurposes.Login,
            Status = MfaChallengeStatuses.PendingSelection,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1),
        };

        var service = BuildService(
            new FakeMfaChallengeRepository(challenge),
            new FakeMfaManagementSessionRepository()
        );

        var result = await service.StartChallengeAsync(
            42,
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
        var challenge = new MfaChallenge
        {
            Id = Guid.NewGuid(),
            UserId = 42,
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
            42,
            challenge.Id,
            "ct-1",
            "123456",
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status410Gone, result.StatusCode);
    }

    [Fact]
    public async Task VerifyEnrollmentAsync_ReturnsGone_WhenEnrollmentChallengeIsExpired()
    {
        var challenge = new MfaChallenge
        {
            Id = Guid.NewGuid(),
            UserId = 42,
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
            42,
            new VerifyMfaEnrollmentRequest
            {
                EnrollmentTransactionId = challenge.Id,
                ContinuationToken = "ct-2",
                Code = "123456",
            },
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status410Gone, result.StatusCode);
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
            managementSessionRepository,
            new FakeUserRepository(),
            new FakeTwilioOtpService(),
            new FakeTokenService(),
            new FakeAccessTokenSessionRepository(),
            new FakeMfaTempTokenSessionRepository(),
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
            long userId,
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

        public Task<bool> HasActiveStepUpSessionAsync(long userId, DateTime sinceUtc, CancellationToken cancellationToken)
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
        public Task<List<UserMfaMethod>> GetEnabledByUserIdAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult(new List<UserMfaMethod>());

        public Task<List<UserMfaMethod>> GetByUserIdAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult(new List<UserMfaMethod>());

        public Task<UserMfaMethod?> GetEnabledByUserIdAndMethodAsync(long userId, string method, CancellationToken cancellationToken) =>
            Task.FromResult<UserMfaMethod?>(null);

        public Task<UserMfaMethod?> GetByUserIdAndMethodAsync(long userId, string method, CancellationToken cancellationToken) =>
            Task.FromResult<UserMfaMethod?>(null);

        public Task AddAsync(UserMfaMethod method, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateAsync(UserMfaMethod method, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeUserRecoveryCodeRepository : IUserRecoveryCodeRepository
    {
        public Task<(UserRecoveryCodeBatch? Batch, int RemainingCount)> GetStatusAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult<(UserRecoveryCodeBatch?, int)>((null, 0));

        public Task<bool> HasUnusedCodesAsync(long userId, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<UserRecoveryCodeBatch> ReplaceBatchAsync(long userId, IReadOnlyCollection<string> codeHashes, CancellationToken cancellationToken) =>
            Task.FromResult(new UserRecoveryCodeBatch());

        public Task<bool> TryConsumeCodeAsync(long userId, string code, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public Task<User?> GetByIdAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(new User { Id = userId, Username = "user", Email = "user@example.com", PasswordHash = "hash", IsActive = true });

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken) => Task.FromResult<User?>(null);

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult<User?>(null);

        public Task AddAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken) => Task.FromResult<User?>(null);

        public Task EnableFido2MfaAsync(long userId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DisableFido2MfaAsync(long userId, CancellationToken cancellationToken) => Task.CompletedTask;

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

        public string CreateMfaToken(User user, Guid mfaTransactionId, string tokenJti) => "mfa";

        public string CreateRefreshToken() => "refresh";
    }

    private sealed class FakeAccessTokenSessionRepository : IAccessTokenSessionRepository
    {
        public Task AddAsync(AccessTokenSession session, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<AccessTokenSession?> GetActiveByJtiAsync(string tokenJti, CancellationToken cancellationToken) =>
            Task.FromResult<AccessTokenSession?>(null);

        public Task RevokeByJtiAsync(string tokenJti, string reason, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeAllActiveByUserAsync(long userId, string reason, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeMfaTempTokenSessionRepository : IMfaTempTokenSessionRepository
    {
        public Task AddAsync(MfaTempTokenSession session, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<MfaTempTokenSession?> GetActiveByJtiAsync(string tokenJti, CancellationToken cancellationToken) =>
            Task.FromResult<MfaTempTokenSession?>(null);

        public Task ConsumeByTransactionAsync(Guid mfaTransactionId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeByJtiAsync(string tokenJti, string reason, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeAllActiveByUserAsync(long userId, string reason, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeAuditService : IAuditService
    {
        public Task TrackAuthenticationEventAsync(
            long? userId,
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
            long? userId,
            string? usernameOrEmail,
            string? failureReason,
            object? details,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;
    }
}
