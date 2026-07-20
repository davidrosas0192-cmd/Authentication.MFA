using Authentication.Fido2.Common;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.DTOs.Mfa;
using Authentication.Fido2.Entities;
using Authentication.Fido2.Options;
using Authentication.Fido2.Services.Implementations;
using Authentication.Fido2.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Authentication.Fido2.Tests.Services;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_ReturnsRequiresEnrollment_WhenNoMethodsAreEnabled_AndSetupOptionsExist()
    {
        var userId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var user = new User
        {
            Id = userId,
            Username = "demo",
            Email = "demo@example.com",
            PasswordHash = PasswordHasher.Hash("StrongPass123!"),
            IsActive = true,
        };

        var mfaService = new FakeMfaService
        {
            AllowedMethodsToReturn = [],
            AvailableSetupMethodsToReturn = ["email", "fido2"],
            LoginEnrollmentSessionToReturn = (Guid.Parse("11111111-1111-1111-1111-111111111111"), "bootstrap-ct-1"),
        };

        var accessRepo = new FakeAccessTokenSessionRepository();
        var mfaTempRepo = new FakeMfaTempTokenSessionRepository();
        var enrollmentRepo = new FakeMfaLoginEnrollmentSessionRepository();

        var service = new AuthService(
            new FakeUserRepository(user),
            new FakeTokenService(),
            accessRepo,
            mfaTempRepo,
            enrollmentRepo,
            mfaService,
            new FakeAuditService(),
            Microsoft.Extensions.Options.Options.Create(new JwtOptions
            {
                Issuer = "issuer",
                Audience = "audience",
                SecretKey = "CHANGE_THIS_SECRET_KEY_USE_USER_SECRETS_OR_KEY_VAULT_MIN_32_CHARS",
                AccessTokenExpirationMinutes = 15,
                RefreshTokenExpirationDays = 7,
            }),
            Microsoft.Extensions.Options.Options.Create(new MfaJwtOptions
            {
                Issuer = "mfa-issuer",
                Audience = "mfa-audience",
                SecretKey = "MFA_TEMP_TOKEN_SECRET_MIN_32_CHARS_DIFFERENT",
                ExpirationMinutes = 5,
            })
        );

        var result = await service.LoginAsync(
            new LoginRequest { Username = "demo", Password = "StrongPass123!" },
            "127.0.0.1",
            "Test-Agent/1.0",
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("RequiresEnrollment", result.Data!.Status);
        Assert.NotNull(result.Data.EnrollmentToken);
        Assert.Equal(300, result.Data.EnrollmentExpiresIn);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), result.Data.EnrollmentSessionId);
        Assert.Equal("bootstrap-ct-1", result.Data.EnrollmentContinuationToken);
        Assert.Contains("email", result.Data.AvailableMfaSetupOptions);
        Assert.DoesNotContain("fido2", result.Data.AvailableMfaSetupOptions);
        Assert.Null(result.Data.AccessToken);
        Assert.Equal(1, accessRepo.RevokeAllActiveByUserCallCount);
        Assert.Equal(1, mfaTempRepo.RevokeAllActiveByUserCallCount);
        Assert.Equal(1, enrollmentRepo.RevokeAllActiveByUserCallCount);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly User _user;

        public FakeUserRepository(User user)
        {
            _user = user;
        }

        public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(_user.Id == userId ? _user : null);

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(string.Equals(_user.Username, username, StringComparison.OrdinalIgnoreCase) ? _user : null);

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(string.Equals(_user.Email, email, StringComparison.OrdinalIgnoreCase) ? _user : null);

        public Task AddAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(
                string.Equals(_user.Username, usernameOrEmail, StringComparison.OrdinalIgnoreCase)
                || string.Equals(_user.Email, usernameOrEmail, StringComparison.OrdinalIgnoreCase)
                    ? _user
                    : null
            );

        public Task EnableFido2MfaAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DisableFido2MfaAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeTokenService : ITokenService
    {
        public string CreateAccessToken(User user, string tokenJti) => "access-token";

        public string CreateMfaToken(User user, Guid mfaTransactionId, string tokenJti) => "mfa-token";

        public string CreateLoginEnrollmentToken(User user, Guid enrollmentSessionId, string tokenJti) => "login-enrollment-token";

        public string CreateRefreshToken() => "refresh-token";

        public string HashRefreshToken(string token) => token;
    }

    private sealed class FakeAccessTokenSessionRepository : IAccessTokenSessionRepository
    {
        public int RevokeAllActiveByUserCallCount { get; private set; }

        public Task AddAsync(AccessTokenSession session, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<AccessTokenSession?> GetActiveByJtiAsync(string tokenJti, CancellationToken cancellationToken) =>
            Task.FromResult<AccessTokenSession?>(null);

        public Task RevokeByJtiAsync(string tokenJti, string reason, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteRevokedSessionsAsync(DateTime beforeUtc, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeAllActiveByUserAsync(Guid userId, string reason, CancellationToken cancellationToken)
        {
            RevokeAllActiveByUserCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMfaTempTokenSessionRepository : IMfaTempTokenSessionRepository
    {
        public int RevokeAllActiveByUserCallCount { get; private set; }

        public Task AddAsync(MfaTempTokenSession session, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<MfaTempTokenSession?> GetActiveByJtiAsync(string tokenJti, CancellationToken cancellationToken) =>
            Task.FromResult<MfaTempTokenSession?>(null);

        public Task ConsumeByTransactionAsync(Guid mfaTransactionId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeByJtiAsync(string tokenJti, string reason, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeAllActiveByUserAsync(Guid userId, string reason, CancellationToken cancellationToken)
        {
            RevokeAllActiveByUserCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMfaLoginEnrollmentSessionRepository : IMfaLoginEnrollmentSessionRepository
    {
        public int RevokeAllActiveByUserCallCount { get; private set; }

        public Task AddAsync(MfaLoginEnrollmentSession session, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<MfaLoginEnrollmentSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<MfaLoginEnrollmentSession?>(null);

        public Task<MfaLoginEnrollmentSession?> GetActiveByJtiAsync(string tokenJti, CancellationToken cancellationToken) =>
            Task.FromResult<MfaLoginEnrollmentSession?>(null);

        public Task UpdateAsync(MfaLoginEnrollmentSession session, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeAllActiveByUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            RevokeAllActiveByUserCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuditService : IAuditService
    {
        public Task TrackAuthenticationEventAsync(Guid? userId, string? usernameOrEmail, string stage, string method, bool isSuccess, string? failureReason, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task TrackSecurityEventAsync(string category, string eventType, string severity, bool isSuccess, Guid? userId, string? usernameOrEmail, string? failureReason, object? details, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeMfaService : IMfaService
    {
        public List<string> AllowedMethodsToReturn { get; set; } = [];
        public List<string> AvailableSetupMethodsToReturn { get; set; } = [];
        public (Guid SessionId, string ContinuationToken) LoginEnrollmentSessionToReturn { get; set; }

        public Task<List<string>> GetAllowedMethodsAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(AllowedMethodsToReturn);
        public Task<List<string>> GetAvailableSetupMethodsAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(AvailableSetupMethodsToReturn);
        public Task<(Guid SessionId, string ContinuationToken)> StartLoginEnrollmentSessionAsync(Guid userId, string tokenJti, string? ipAddress, string? userAgent, CancellationToken cancellationToken) => Task.FromResult(LoginEnrollmentSessionToReturn);
        public Task<Result<StartMfaManagementSessionResponse>> StartManagementSessionAsync(Guid userId, string? ipAddress, string? userAgent, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Result<StartMfaChallengeResponse>> StartManagementChallengeAsync(Guid userId, Guid managementSessionId, string continuationToken, string method, string? ipAddress, string? userAgent, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Result<StartMfaChallengeResponse>> StartChallengeAsync(Guid userId, Guid mfaTransactionId, string method, string? ipAddress, string? userAgent, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Result<VerifyMfaManagementChallengeResponse>> VerifyManagementChallengeAsync(Guid userId, Guid managementSessionId, string continuationToken, string code, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Result<CompleteMfaManagementSessionResponse>> CompleteManagementSessionAsync(Guid userId, Guid managementSessionId, string continuationToken, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Result<CancelMfaManagementSessionResponse>> CancelManagementSessionAsync(Guid userId, Guid mfaTransactionId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Result<LoginResponse>> VerifyChallengeAsync(Guid userId, Guid mfaTransactionId, string continuationToken, string code, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Result<StartMfaEnrollmentResponse>> StartEnrollmentAsync(Guid userId, StartMfaEnrollmentRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Result<VerifyMfaEnrollmentResponse>> VerifyEnrollmentAsync(Guid userId, VerifyMfaEnrollmentRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Result<StartLoginEnrollmentResponse>> StartLoginEnrollmentAsync(Guid userId, Guid enrollmentSessionId, StartLoginEnrollmentRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Result<VerifyLoginEnrollmentResponse>> VerifyLoginEnrollmentAsync(Guid userId, Guid enrollmentSessionId, VerifyLoginEnrollmentRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Result<LoginResponse>> CompleteLoginEnrollmentSessionAsync(Guid userId, Guid enrollmentSessionId, string continuationToken, string? ipAddress, string? userAgent, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Result<RemoveMfaMethodResponse>> RemoveMethodAsync(Guid userId, string method, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Result<StartMfaReconfigureResponse>> StartReconfigureMethodAsync(Guid userId, string method, StartMfaReconfigureRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Result<CompleteMfaReconfigureResponse>> CompleteReconfigureMethodAsync(Guid userId, string method, CompleteMfaReconfigureRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Guid> CreateSelectionChallengeAsync(Guid userId, string? ipAddress, string? userAgent, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}