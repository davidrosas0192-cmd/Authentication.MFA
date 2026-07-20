using Authentication.Fido2.Common;
using Authentication.Fido2.Constants;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.DTOs.Mfa;
using Authentication.Fido2.Entities;
using Authentication.Fido2.Options;
using Authentication.Fido2.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Authentication.Fido2.Services.Implementations;

public class MfaService : IMfaService
{
    private const int RecoveryCodeLength = 12;
    private const string RecoveryCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int ManagementStepUpWindowMinutes = 10;

    private readonly IUserMfaMethodRepository _mfaMethodRepository;
    private readonly IUserRecoveryCodeRepository _userRecoveryCodeRepository;
    private readonly IMfaChallengeRepository _mfaChallengeRepository;
    private readonly IMfaLoginEnrollmentSessionRepository _mfaLoginEnrollmentSessionRepository;
    private readonly IMfaManagementSessionRepository _mfaManagementSessionRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITwilioOtpService _twilioOtpService;
    private readonly ITokenService _tokenService;
    private readonly IAccessTokenSessionRepository _accessTokenSessionRepository;
    private readonly IMfaTempTokenSessionRepository _mfaTempTokenSessionRepository;
    private readonly IRateLimitingService _rateLimitingService;
    private readonly IDistributedLockService _distributedLockService;
    private readonly ISessionFactory _sessionFactory;
    private readonly IAuditService _auditService;
    private readonly JwtOptions _jwtOptions;

    public MfaService(
        IUserMfaMethodRepository mfaMethodRepository,
        IUserRecoveryCodeRepository userRecoveryCodeRepository,
        IMfaChallengeRepository mfaChallengeRepository,
        IMfaLoginEnrollmentSessionRepository mfaLoginEnrollmentSessionRepository,
        IMfaManagementSessionRepository mfaManagementSessionRepository,
        IUserRepository userRepository,
        ITwilioOtpService twilioOtpService,
        ITokenService tokenService,
        IAccessTokenSessionRepository accessTokenSessionRepository,
        IMfaTempTokenSessionRepository mfaTempTokenSessionRepository,
        IRateLimitingService rateLimitingService,
        IDistributedLockService distributedLockService,
        ISessionFactory sessionFactory,
        IAuditService auditService,
        IOptions<JwtOptions> jwtOptions
    )
    {
        _mfaMethodRepository = mfaMethodRepository;
        _userRecoveryCodeRepository = userRecoveryCodeRepository;
        _mfaChallengeRepository = mfaChallengeRepository;
        _mfaLoginEnrollmentSessionRepository = mfaLoginEnrollmentSessionRepository;
        _mfaManagementSessionRepository = mfaManagementSessionRepository;
        _userRepository = userRepository;
        _twilioOtpService = twilioOtpService;
        _tokenService = tokenService;
        _accessTokenSessionRepository = accessTokenSessionRepository;
        _mfaTempTokenSessionRepository = mfaTempTokenSessionRepository;
        _rateLimitingService = rateLimitingService;
        _distributedLockService = distributedLockService;
        _sessionFactory = sessionFactory;
        _auditService = auditService;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<List<string>> GetAllowedMethodsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return [];
        }

        var methods = await _mfaMethodRepository.GetEnabledByUserIdAsync(userId, cancellationToken);
        var normalized = methods
            .Select(x => x.Method.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hasRecoveryCodes = await _userRecoveryCodeRepository.HasUnusedCodesAsync(userId, cancellationToken);
        if (
            hasRecoveryCodes
            && !normalized.Contains(MfaMethodTypes.RecoveryCode, StringComparer.OrdinalIgnoreCase)
        )
        {
            normalized.Add(MfaMethodTypes.RecoveryCode);
        }

        return normalized;
    }

    public async Task<List<string>> GetAvailableSetupMethodsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var enabled = await GetAllowedMethodsAsync(userId, cancellationToken);
        var allOptions = new List<string>
        {
            MfaMethodTypes.Sms,
            MfaMethodTypes.Email,
            MfaMethodTypes.Fido2,
        };

        return allOptions
            .Where(x => !enabled.Contains(x, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<(Guid SessionId, string ContinuationToken)> StartLoginEnrollmentSessionAsync(
        Guid userId,
        string tokenJti,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        var session = new MfaLoginEnrollmentSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = MfaLoginEnrollmentSessionStatuses.EnrollmentRequired,
            ContinuationToken = CreateContinuationToken(),
            StepVersion = 1,
            TokenJti = tokenJti,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(MfaChallengeOptions.ChallengeExpirationMinutes),
            CreatedAtUtc = DateTime.UtcNow,
            ModifiedAtUtc = DateTime.UtcNow,
        };

        await _mfaLoginEnrollmentSessionRepository.AddAsync(session, cancellationToken);

        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.mfa.login_enrollment.start",
            "Information",
            true,
            userId,
            null,
            null,
            new { sessionId = session.Id, ipAddress, userAgent },
            cancellationToken
        );

        return (session.Id, session.ContinuationToken);
    }

    public async Task<Result<StartMfaManagementSessionResponse>> StartManagementSessionAsync(
        Guid userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result<StartMfaManagementSessionResponse>.Failure(
                "User not found or inactive.",
                StatusCodes.Status404NotFound
            );
        }

        var availableMethods = await GetAllowedMethodsAsync(userId, cancellationToken);
        if (availableMethods.Count == 0)
        {
            return Result<StartMfaManagementSessionResponse>.Failure(
                "No MFA methods are available for step-up.",
                StatusCodes.Status400BadRequest
            );
        }

        var session = new MfaManagementSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = MfaManagementSessionStatuses.StepUpRequired,
            ContinuationToken = CreateContinuationToken(),
            StepVersion = 1,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(MfaChallengeOptions.ChallengeExpirationMinutes),
            CreatedAtUtc = DateTime.UtcNow,
            ModifiedAtUtc = DateTime.UtcNow,
        };

        await _mfaManagementSessionRepository.AddAsync(session, cancellationToken);

        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.mfa.management_session.start",
            "Information",
            true,
            userId,
            user.Username,
            null,
            new { managementSessionId = session.Id },
            cancellationToken
        );

        return Result<StartMfaManagementSessionResponse>.Success(
            new StartMfaManagementSessionResponse
            {
                Status = "StepUpRequired",
                MfaTransactionId = session.Id,
                ContinuationToken = session.ContinuationToken,
                AvailableMethods = availableMethods,
                ExpiresAtUtc = session.ExpiresAtUtc,
            },
            "Management MFA step-up required."
        );
    }

    public async Task<Result<StartMfaChallengeResponse>> StartManagementChallengeAsync(
        Guid userId,
        Guid managementSessionId,
        string continuationToken,
        string methodName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        var session = await _mfaManagementSessionRepository.GetByIdAsync(
            managementSessionId,
            cancellationToken
        );

        if (session is null || session.UserId != userId || session.ExpiresAtUtc < DateTime.UtcNow)
        {
            return Result<StartMfaChallengeResponse>.Failure(
                "Invalid or expired management session.",
                StatusCodes.Status400BadRequest
            );
        }

        if (session.Status != MfaManagementSessionStatuses.StepUpRequired)
        {
            return Result<StartMfaChallengeResponse>.Failure(
                "Management session is not in a step-up state.",
                StatusCodes.Status409Conflict
            );
        }

        if (!string.Equals(session.ContinuationToken, continuationToken, StringComparison.Ordinal))
        {
            return Result<StartMfaChallengeResponse>.Failure(
                "Management flow has already advanced.",
                StatusCodes.Status409Conflict
            );
        }

        var normalizedMethod = methodName.Trim().ToLowerInvariant();
        if (
            normalizedMethod != MfaMethodTypes.Sms
            && normalizedMethod != MfaMethodTypes.Email
            && normalizedMethod != MfaMethodTypes.RecoveryCode
        )
        {
            return Result<StartMfaChallengeResponse>.Failure(
                "Only sms, email, and recovery_code challenge start are supported in this endpoint.",
                StatusCodes.Status400BadRequest
            );
        }

        var challenge = new MfaChallenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = MfaChallengePurposes.ManageMfa,
            Method = normalizedMethod,
            Status = MfaChallengeStatuses.Pending,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(MfaChallengeOptions.ChallengeExpirationMinutes),
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAtUtc = DateTime.UtcNow,
        };

        if (normalizedMethod == MfaMethodTypes.RecoveryCode)
        {
            var hasCodes = await _userRecoveryCodeRepository.HasUnusedCodesAsync(userId, cancellationToken);
            if (!hasCodes)
            {
                return Result<StartMfaChallengeResponse>.Failure(
                    "Selected MFA method is not available for this user.",
                    StatusCodes.Status400BadRequest
                );
            }

            challenge.Provider = "internal";
            challenge.Channel = normalizedMethod;
        }
        else
        {
            var method = await _mfaMethodRepository.GetEnabledByUserIdAndMethodAsync(
                userId,
                normalizedMethod,
                cancellationToken
            );

            if (method is null || string.IsNullOrWhiteSpace(method.ContactValue))
            {
                return Result<StartMfaChallengeResponse>.Failure(
                    "Selected MFA method is not available for this user.",
                    StatusCodes.Status400BadRequest
                );
            }

            var providerSid = await _twilioOtpService.StartVerificationAsync(
                method.ContactValue,
                normalizedMethod,
                cancellationToken
            );

            challenge.Provider = "twilio";
            challenge.ProviderRequestId = providerSid;
            challenge.Channel = normalizedMethod;
            challenge.ContactValue = method.ContactValue;
        }

        await _mfaChallengeRepository.AddAsync(challenge, cancellationToken);

        var rotatedContinuationToken = CreateContinuationToken();
        session.ChallengeId = challenge.Id;
        session.ContinuationToken = rotatedContinuationToken;
        session.StepVersion += 1;
        session.ModifiedAtUtc = DateTime.UtcNow;
        await _mfaManagementSessionRepository.UpdateAsync(session, cancellationToken);

        await _auditService.TrackAuthenticationEventAsync(
            userId,
            null,
            "mfa_management_stepup_start",
            normalizedMethod,
            true,
            null,
            cancellationToken
        );

        return Result<StartMfaChallengeResponse>.Success(
            new StartMfaChallengeResponse
            {
                MfaTransactionId = managementSessionId,
                ContinuationToken = rotatedContinuationToken,
                Method = normalizedMethod,
                Status = challenge.Status,
                ExpiresAtUtc = challenge.ExpiresAtUtc,
            },
            "MFA management challenge started."
        );
    }

    public async Task<Guid> CreateSelectionChallengeAsync(
        Guid userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        var challenge = new MfaChallenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = MfaChallengePurposes.Login,
            ContinuationToken = CreateContinuationToken(),
            StepVersion = 1,
            Status = MfaChallengeStatuses.PendingSelection,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(MfaChallengeOptions.ChallengeExpirationMinutes),
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAtUtc = DateTime.UtcNow,
        };

        await _mfaChallengeRepository.AddAsync(challenge, cancellationToken);

        return challenge.Id;
    }

    public async Task<Result<StartMfaChallengeResponse>> StartChallengeAsync(
        Guid userId,
        Guid mfaTransactionId,
        string methodName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        // Rate limiting: 10 challenge starts per 15 minutes per user
        var rateLimitKey = $"mfa_start_{userId}";
        if (!_rateLimitingService.IsAllowed(rateLimitKey, maxAttempts: 10, windowSeconds: 900))
        {
            return Result<StartMfaChallengeResponse>.Failure(
                "Too many challenge requests. Please try again later.",
                StatusCodes.Status429TooManyRequests
            );
        }

        var challenge = await _mfaChallengeRepository.GetByIdAsync(
            mfaTransactionId,
            cancellationToken
        );

        if (challenge is null || challenge.UserId != userId)
        {
            return Result<StartMfaChallengeResponse>.Failure(
                "Invalid MFA transaction.",
                StatusCodes.Status400BadRequest
            );
        }

        if (challenge.ExpiresAtUtc < DateTime.UtcNow)
        {
            return Result<StartMfaChallengeResponse>.Failure(
                "MFA transaction has expired.",
                StatusCodes.Status410Gone
            );
        }

        if (challenge.Purpose != MfaChallengePurposes.Login)
        {
            return Result<StartMfaChallengeResponse>.Failure(
                "Invalid MFA transaction purpose.",
                StatusCodes.Status400BadRequest
            );
        }

        var normalizedMethod = methodName.Trim().ToLowerInvariant();
        if (
            normalizedMethod != MfaMethodTypes.Sms
            && normalizedMethod != MfaMethodTypes.Email
            && normalizedMethod != MfaMethodTypes.RecoveryCode
            && normalizedMethod != MfaMethodTypes.Fido2
        )
        {
            return Result<StartMfaChallengeResponse>.Failure(
                "Only sms, email, recovery_code, and fido2 challenge start are supported in this endpoint.",
                StatusCodes.Status400BadRequest
            );
        }

        // FIDO2 is handled separately via the Fido2Controller
        if (normalizedMethod == MfaMethodTypes.Fido2)
        {
            var hasFido2 = await _mfaMethodRepository.GetEnabledByUserIdAndMethodAsync(
                challenge.UserId,
                MfaMethodTypes.Fido2,
                cancellationToken
            );

            if (hasFido2 is null)
            {
                return Result<StartMfaChallengeResponse>.Failure(
                    "Selected MFA method is not available for this user.",
                    StatusCodes.Status400BadRequest
                );
            }

            challenge.Method = normalizedMethod;
            challenge.Status = MfaChallengeStatuses.Pending;
            challenge.ContinuationToken = CreateContinuationToken();
            challenge.StepVersion += 1;
            challenge.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(MfaChallengeOptions.ChallengeExpirationMinutes);
            challenge.IpAddress = ipAddress ?? challenge.IpAddress;
            challenge.UserAgent = userAgent ?? challenge.UserAgent;

            await _mfaChallengeRepository.UpdateAsync(challenge, cancellationToken);

            await _auditService.TrackAuthenticationEventAsync(
                challenge.UserId,
                null,
                "mfa_challenge_start",
                normalizedMethod,
                true,
                null,
                cancellationToken
            );

            return Result<StartMfaChallengeResponse>.Success(
                new StartMfaChallengeResponse
                {
                    MfaTransactionId = challenge.Id,
                    ContinuationToken = challenge.ContinuationToken,
                    Method = normalizedMethod,
                    Status = challenge.Status,
                    ExpiresAtUtc = challenge.ExpiresAtUtc,
                },
                "MFA FIDO2 challenge started. Use FIDO2 endpoint to complete."
            );
        }

        if (normalizedMethod == MfaMethodTypes.RecoveryCode)
        {
            var hasCodes = await _userRecoveryCodeRepository.HasUnusedCodesAsync(challenge.UserId, cancellationToken);
            if (!hasCodes)
            {
                return Result<StartMfaChallengeResponse>.Failure(
                    "Selected MFA method is not available for this user.",
                    StatusCodes.Status400BadRequest
                );
            }

            challenge.Method = normalizedMethod;
            challenge.Provider = "internal";
            challenge.ProviderRequestId = null;
            challenge.Channel = normalizedMethod;
            challenge.ContactValue = null;
            challenge.Status = MfaChallengeStatuses.Pending;
            challenge.ContinuationToken = CreateContinuationToken();
            challenge.StepVersion += 1;
            challenge.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(MfaChallengeOptions.ChallengeExpirationMinutes);
            challenge.IpAddress = ipAddress ?? challenge.IpAddress;
            challenge.UserAgent = userAgent ?? challenge.UserAgent;

            await _mfaChallengeRepository.UpdateAsync(challenge, cancellationToken);

            await _auditService.TrackAuthenticationEventAsync(
                challenge.UserId,
                null,
                "mfa_challenge_start",
                normalizedMethod,
                true,
                null,
                cancellationToken
            );

            return Result<StartMfaChallengeResponse>.Success(
                new StartMfaChallengeResponse
                {
                    MfaTransactionId = challenge.Id,
                    ContinuationToken = challenge.ContinuationToken,
                    Method = normalizedMethod,
                    Status = challenge.Status,
                    ExpiresAtUtc = challenge.ExpiresAtUtc,
                },
                "MFA recovery code challenge started."
            );
        }

        var method = await _mfaMethodRepository.GetEnabledByUserIdAndMethodAsync(
            challenge.UserId,
            normalizedMethod,
            cancellationToken
        );

        if (method is null || string.IsNullOrWhiteSpace(method.ContactValue))
        {
            return Result<StartMfaChallengeResponse>.Failure(
                "Selected MFA method is not available for this user.",
                StatusCodes.Status400BadRequest
            );
        }

        var providerSid = await _twilioOtpService.StartVerificationAsync(
            method.ContactValue,
            normalizedMethod,
            cancellationToken
        );

        challenge.Method = normalizedMethod;
        challenge.Provider = "twilio";
        challenge.ProviderRequestId = providerSid;
        challenge.Channel = normalizedMethod;
        challenge.ContactValue = method.ContactValue;
        challenge.Status = MfaChallengeStatuses.Pending;
        challenge.ContinuationToken = CreateContinuationToken();
        challenge.StepVersion += 1;
        challenge.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(MfaChallengeOptions.ChallengeExpirationMinutes);
        challenge.IpAddress = ipAddress ?? challenge.IpAddress;
        challenge.UserAgent = userAgent ?? challenge.UserAgent;

        await _mfaChallengeRepository.UpdateAsync(challenge, cancellationToken);

        await _auditService.TrackAuthenticationEventAsync(
            challenge.UserId,
            null,
            "mfa_challenge_start",
            normalizedMethod,
            true,
            null,
            cancellationToken
        );

        return Result<StartMfaChallengeResponse>.Success(
            new StartMfaChallengeResponse
            {
                MfaTransactionId = challenge.Id,
                ContinuationToken = challenge.ContinuationToken,
                Method = normalizedMethod,
                Status = challenge.Status,
                ExpiresAtUtc = challenge.ExpiresAtUtc,
            },
            "MFA OTP challenge started."
        );
    }

    public async Task<Result<VerifyMfaManagementChallengeResponse>> VerifyManagementChallengeAsync(
        Guid userId,
        Guid managementSessionId,
        string continuationToken,
        string code,
        CancellationToken cancellationToken
    )
    {
        var session = await _mfaManagementSessionRepository.GetByIdAsync(managementSessionId, cancellationToken);

        if (session is null || session.UserId != userId || session.ExpiresAtUtc < DateTime.UtcNow)
        {
            return Result<VerifyMfaManagementChallengeResponse>.Failure(
                "Invalid or expired management session.",
                StatusCodes.Status400BadRequest
            );
        }

        if (session.Status != MfaManagementSessionStatuses.StepUpRequired)
        {
            return Result<VerifyMfaManagementChallengeResponse>.Failure(
                "Management session is not in a verifiable state.",
                StatusCodes.Status409Conflict
            );
        }

        if (!string.Equals(session.ContinuationToken, continuationToken, StringComparison.Ordinal))
        {
            return Result<VerifyMfaManagementChallengeResponse>.Failure(
                "Management flow has already advanced.",
                StatusCodes.Status409Conflict
            );
        }

        if (session.ChallengeId is null)
        {
            return Result<VerifyMfaManagementChallengeResponse>.Failure(
                "No challenge exists for this management session.",
                StatusCodes.Status400BadRequest
            );
        }

        var challenge = await _mfaChallengeRepository.GetByIdAsync(session.ChallengeId.Value, cancellationToken);

        if (challenge is null || challenge.ExpiresAtUtc < DateTime.UtcNow || challenge.UserId != userId)
        {
            return Result<VerifyMfaManagementChallengeResponse>.Failure(
                "Invalid or expired MFA challenge.",
                StatusCodes.Status400BadRequest
            );
        }

        if (challenge.Purpose != MfaChallengePurposes.ManageMfa)
        {
            return Result<VerifyMfaManagementChallengeResponse>.Failure(
                "Invalid MFA challenge purpose.",
                StatusCodes.Status400BadRequest
            );
        }

        if (challenge.Status != MfaChallengeStatuses.Pending)
        {
            return Result<VerifyMfaManagementChallengeResponse>.Failure(
                "MFA challenge is not in a verifiable state.",
                StatusCodes.Status400BadRequest
            );
        }

        var normalizedMethod = challenge.Method?.Trim().ToLowerInvariant();
        if (
            normalizedMethod != MfaMethodTypes.Sms
            && normalizedMethod != MfaMethodTypes.Email
            && normalizedMethod != MfaMethodTypes.RecoveryCode
        )
        {
            return Result<VerifyMfaManagementChallengeResponse>.Failure(
                "Only sms, email, and recovery_code verification is supported in this endpoint.",
                StatusCodes.Status400BadRequest
            );
        }

        var isApproved = false;

        if (normalizedMethod == MfaMethodTypes.RecoveryCode)
        {
            var normalizedCode = NormalizeRecoveryCode(requestedCode: code);
            isApproved = await _userRecoveryCodeRepository.TryConsumeCodeAsync(
                challenge.UserId,
                normalizedCode,
                cancellationToken
            );
        }
        else
        {
            if (string.IsNullOrWhiteSpace(challenge.ContactValue))
            {
                return Result<VerifyMfaManagementChallengeResponse>.Failure(
                    "MFA method is unavailable.",
                    StatusCodes.Status400BadRequest
                );
            }

            isApproved = await _twilioOtpService.CheckVerificationAsync(
                challenge.ContactValue,
                code,
                cancellationToken
            );
        }

        if (!isApproved)
        {
            challenge.FailedAttempts++;
            challenge.LastFailedAttemptAtUtc = DateTime.UtcNow;

            if (challenge.FailedAttempts >= MfaChallengeOptions.MaxFailedAttempts)
            {
                challenge.Status = MfaChallengeStatuses.Locked;
                await _mfaChallengeRepository.UpdateAsync(challenge, cancellationToken);

                await _auditService.TrackSecurityEventAsync(
                    "Authentication",
                    "auth.mfa.management_session.stepup.failed",
                    "Warning",
                    false,
                    userId,
                    null,
                    $"Maximum failed attempts ({MfaChallengeOptions.MaxFailedAttempts}) exceeded",
                    null,
                    cancellationToken
                );

                return Result<VerifyMfaManagementChallengeResponse>.Failure(
                    "Too many failed verification attempts. Please try again later.",
                    StatusCodes.Status429TooManyRequests
                );
            }

            challenge.Status = MfaChallengeStatuses.Pending;
            await _mfaChallengeRepository.UpdateAsync(challenge, cancellationToken);

            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.mfa.management_session.stepup.failed",
                "Warning",
                false,
                userId,
                null,
                "Invalid step-up code",
                null,
                cancellationToken
            );

            return Result<VerifyMfaManagementChallengeResponse>.Failure(
                "Invalid or expired code.",
                StatusCodes.Status401Unauthorized
            );
        }

        challenge.Status = MfaChallengeStatuses.Verified;
        challenge.VerifiedAtUtc = DateTime.UtcNow;
        await _mfaChallengeRepository.UpdateAsync(challenge, cancellationToken);

        var validUntil = DateTime.UtcNow.AddMinutes(ManagementStepUpWindowMinutes);

        var rotatedContinuationToken = CreateContinuationToken();

        session.Status = MfaManagementSessionStatuses.StepUpCompleted;
        session.ContinuationToken = rotatedContinuationToken;
        session.StepVersion += 1;
        session.VerifiedAtUtc = DateTime.UtcNow;
        session.ExpiresAtUtc = validUntil;
        session.ModifiedAtUtc = DateTime.UtcNow;
        await _mfaManagementSessionRepository.UpdateAsync(session, cancellationToken);

        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.mfa.management_session.stepup.completed",
            "Information",
            true,
            userId,
            null,
            null,
            new { validUntil },
            cancellationToken
        );

        return Result<VerifyMfaManagementChallengeResponse>.Success(
            new VerifyMfaManagementChallengeResponse
            {
                Status = "StepUpCompleted",
                ContinuationToken = rotatedContinuationToken,
                VerifiedAtUtc = challenge.VerifiedAtUtc.Value,
                StepUpValidUntilUtc = validUntil,
            },
            "MFA management step-up completed."
        );
    }

    public async Task<Result<CompleteMfaManagementSessionResponse>> CompleteManagementSessionAsync(
        Guid userId,
        Guid managementSessionId,
        string continuationToken,
        CancellationToken cancellationToken
    )
    {
        var session = await _mfaManagementSessionRepository.GetByIdAsync(managementSessionId, cancellationToken);
        if (session is null || session.UserId != userId)
        {
            return Result<CompleteMfaManagementSessionResponse>.Failure(
                "Management session not found.",
                StatusCodes.Status404NotFound
            );
        }

        if (session.Status != MfaManagementSessionStatuses.StepUpCompleted)
        {
            return Result<CompleteMfaManagementSessionResponse>.Failure(
                "Management session is not ready to complete.",
                StatusCodes.Status409Conflict
            );
        }

        if (!string.Equals(session.ContinuationToken, continuationToken, StringComparison.Ordinal))
        {
            return Result<CompleteMfaManagementSessionResponse>.Failure(
                "Management flow has already advanced.",
                StatusCodes.Status409Conflict
            );
        }

        session.Status = MfaManagementSessionStatuses.Completed;
        session.ContinuationToken = CreateContinuationToken();
        session.StepVersion += 1;
        session.ExpiresAtUtc = DateTime.UtcNow;
        session.ModifiedAtUtc = DateTime.UtcNow;
        await _mfaManagementSessionRepository.UpdateAsync(session, cancellationToken);

        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.mfa.management_session.completed",
            "Information",
            true,
            userId,
            null,
            null,
            new { managementSessionId },
            cancellationToken
        );

        return Result<CompleteMfaManagementSessionResponse>.Success(
            new CompleteMfaManagementSessionResponse
            {
                Status = "Completed",
                CompletedAtUtc = DateTime.UtcNow,
            },
            "MFA management session completed."
        );
    }

    public async Task<Result<CancelMfaManagementSessionResponse>> CancelManagementSessionAsync(
        Guid userId,
        Guid managementSessionId,
        CancellationToken cancellationToken
    )
    {
        var session = await _mfaManagementSessionRepository.GetByIdAsync(managementSessionId, cancellationToken);
        if (session is null || session.UserId != userId)
        {
            return Result<CancelMfaManagementSessionResponse>.Failure(
                "Management session not found.",
                StatusCodes.Status404NotFound
            );
        }

        if (
            session.Status != MfaManagementSessionStatuses.StepUpRequired
            && session.Status != MfaManagementSessionStatuses.StepUpCompleted
        )
        {
            return Result<CancelMfaManagementSessionResponse>.Failure(
                "Management session cannot be cancelled in its current state.",
                StatusCodes.Status409Conflict
            );
        }

        session.Status = MfaManagementSessionStatuses.Cancelled;
        session.ExpiresAtUtc = DateTime.UtcNow;
        session.ModifiedAtUtc = DateTime.UtcNow;
        await _mfaManagementSessionRepository.UpdateAsync(session, cancellationToken);

        if (session.ChallengeId.HasValue)
        {
            var challenge = await _mfaChallengeRepository.GetByIdAsync(session.ChallengeId.Value, cancellationToken);
            if (challenge is not null && challenge.Status == MfaChallengeStatuses.Pending)
            {
                challenge.Status = MfaChallengeStatuses.Revoked;
                challenge.ExpiresAtUtc = DateTime.UtcNow;
                await _mfaChallengeRepository.UpdateAsync(challenge, cancellationToken);
            }
        }

        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.mfa.management_session.cancelled",
            "Information",
            true,
            userId,
            null,
            null,
            new { managementSessionId },
            cancellationToken
        );

        return Result<CancelMfaManagementSessionResponse>.Success(
            new CancelMfaManagementSessionResponse
            {
                Status = "Cancelled",
                CancelledAtUtc = DateTime.UtcNow,
            },
            "MFA management session cancelled."
        );
    }

    public async Task<Result<LoginResponse>> VerifyChallengeAsync(
        Guid userId,
        Guid mfaTransactionId,
        string continuationToken,
        string code,
        CancellationToken cancellationToken
    )
    {
        // Rate limiting: 10 attempts per 15 minutes per user
        var rateLimitKey = $"mfa_verify_{userId}";
        if (!_rateLimitingService.IsAllowed(rateLimitKey, maxAttempts: 10, windowSeconds: 900))
        {
            await _auditService.TrackAuthenticationEventAsync(
                userId,
                null,
                "mfa_challenge_verify",
                "unknown",
                false,
                "Rate limit exceeded",
                cancellationToken
            );

            return Result<LoginResponse>.Failure(
                "Too many verification attempts. Please try again later.",
                StatusCodes.Status429TooManyRequests
            );
        }

        // Use distributed lock to prevent concurrent verification of the same challenge
        var lockKey = $"mfa_verify_{mfaTransactionId}";
        return await _distributedLockService.ExecuteLockedAsync(
            lockKey,
            async ct => await VerifyChallengeInternalAsync(userId, mfaTransactionId, continuationToken, code, ct),
            timeoutSeconds: 5,
            cancellationToken: cancellationToken
        );
    }

    private async Task<Result<LoginResponse>> VerifyChallengeInternalAsync(
        Guid userId,
        Guid mfaTransactionId,
        string continuationToken,
        string code,
        CancellationToken cancellationToken
    )
    {
        var challenge = await _mfaChallengeRepository.GetByIdAsync(
            mfaTransactionId,
            cancellationToken
        );

        if (challenge is null || challenge.UserId != userId)
        {
            return Result<LoginResponse>.Failure(
                "Invalid MFA challenge.",
                StatusCodes.Status400BadRequest
            );
        }

        if (challenge.ExpiresAtUtc < DateTime.UtcNow)
        {
            return Result<LoginResponse>.Failure(
                "MFA challenge has expired.",
                StatusCodes.Status410Gone
            );
        }

        if (challenge.Purpose != MfaChallengePurposes.Login)
        {
            return Result<LoginResponse>.Failure(
                "Invalid MFA challenge purpose.",
                StatusCodes.Status400BadRequest
            );
        }

        if (challenge.Status != MfaChallengeStatuses.Pending)
        {
            return Result<LoginResponse>.Failure(
                "MFA_FLOW_ALREADY_ADVANCED",
                StatusCodes.Status409Conflict
            );
        }

        if (!string.Equals(challenge.ContinuationToken, continuationToken, StringComparison.Ordinal))
        {
            return Result<LoginResponse>.Failure(
                "MFA_FLOW_ALREADY_ADVANCED",
                StatusCodes.Status409Conflict
            );
        }

        if (
            challenge.Method != MfaMethodTypes.Sms
            && challenge.Method != MfaMethodTypes.Email
            && challenge.Method != MfaMethodTypes.RecoveryCode
        )
        {
            return Result<LoginResponse>.Failure(
                "Only sms, email, and recovery_code verification is supported in this endpoint.",
                StatusCodes.Status400BadRequest
            );
        }

        if (challenge.Method == MfaMethodTypes.RecoveryCode)
        {
            var normalizedCode = NormalizeRecoveryCode(requestedCode: code);
            var isConsumed = await _userRecoveryCodeRepository.TryConsumeCodeAsync(
                challenge.UserId,
                normalizedCode,
                cancellationToken
            );

            if (!isConsumed)
            {
                challenge.FailedAttempts++;
                challenge.LastFailedAttemptAtUtc = DateTime.UtcNow;

                // Check if max attempts exceeded
                if (challenge.FailedAttempts >= MfaChallengeOptions.MaxFailedAttempts)
                {
                    challenge.Status = MfaChallengeStatuses.Locked;
                    await _mfaChallengeRepository.UpdateAsync(challenge, cancellationToken);

                    await _auditService.TrackAuthenticationEventAsync(
                        challenge.UserId,
                        null,
                        "mfa_challenge_verify",
                        challenge.Method,
                        false,
                        $"Maximum failed attempts ({MfaChallengeOptions.MaxFailedAttempts}) exceeded",
                        cancellationToken
                    );

                    return Result<LoginResponse>.Failure(
                        "Too many failed verification attempts. Please try again later.",
                        StatusCodes.Status429TooManyRequests
                    );
                }

                challenge.Status = MfaChallengeStatuses.Pending;
                await _mfaChallengeRepository.UpdateAsync(challenge, cancellationToken);

                await _auditService.TrackAuthenticationEventAsync(
                    challenge.UserId,
                    null,
                    "mfa_challenge_verify",
                    challenge.Method,
                    false,
                    "Invalid recovery code",
                    cancellationToken
                );

                return Result<LoginResponse>.Failure(
                    "Invalid or already used recovery code.",
                    StatusCodes.Status401Unauthorized
                );
            }

            var userForRecovery = await _userRepository.GetByIdAsync(challenge.UserId, cancellationToken);
            if (userForRecovery is null)
            {
                return Result<LoginResponse>.Failure("User not found.", StatusCodes.Status404NotFound);
            }

            userForRecovery.LastLoginAtUtc = DateTime.UtcNow;
            await _userRepository.UpdateAsync(userForRecovery, cancellationToken);

            challenge.Status = MfaChallengeStatuses.Verified;
            challenge.ContinuationToken = CreateContinuationToken();
            challenge.StepVersion += 1;
            challenge.VerifiedAtUtc = DateTime.UtcNow;
            await _mfaChallengeRepository.UpdateAsync(challenge, cancellationToken);

            await _auditService.TrackAuthenticationEventAsync(
                challenge.UserId,
                userForRecovery.Username,
                "mfa_challenge_verify",
                challenge.Method,
                true,
                null,
                cancellationToken
            );

            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.mfa.recovery_code.consume",
                "Information",
                true,
                challenge.UserId,
                userForRecovery.Username,
                null,
                null,
                cancellationToken
            );

            await _accessTokenSessionRepository.RevokeAllActiveByUserAsync(
                userForRecovery.Id,
                "new_login",
                cancellationToken
            );
            await _mfaTempTokenSessionRepository.RevokeAllActiveByUserAsync(
                userForRecovery.Id,
                "new_login",
                cancellationToken
            );

            var recoveryAccessTokenJti = Guid.NewGuid().ToString("N");
            var recoveryAccessToken = _tokenService.CreateAccessToken(
                userForRecovery,
                recoveryAccessTokenJti
            );
            var recoveryRefreshToken = _tokenService.CreateRefreshToken();

            await _accessTokenSessionRepository.AddAsync(
                new AccessTokenSession
                {
                    Id = Guid.NewGuid(),
                    UserId = userForRecovery.Id,
                    TokenJti = recoveryAccessTokenJti,
                    IssuedAtUtc = DateTime.UtcNow,
                    ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes),
                },
                cancellationToken
            );

            return Result<LoginResponse>.Success(
                new LoginResponse
                {
                    Status = "Authenticated",
                    MfaRequired = false,
                    AccessToken = recoveryAccessToken,
                    RefreshToken = recoveryRefreshToken,
                    ExpiresIn = 15 * 60,
                    AllowedMfaMethods = await GetAllowedMethodsAsync(userForRecovery.Id, cancellationToken),
                },
                "MFA verification succeeded."
            );
        }

        var method = await _mfaMethodRepository.GetEnabledByUserIdAndMethodAsync(
            challenge.UserId,
            challenge.Method,
            cancellationToken
        );

        if (method is null || string.IsNullOrWhiteSpace(method.ContactValue))
        {
            return Result<LoginResponse>.Failure(
                "MFA method is unavailable.",
                StatusCodes.Status400BadRequest
            );
        }

        var isApproved = await _twilioOtpService.CheckVerificationAsync(
            challenge.ContactValue ?? method.ContactValue,
            code,
            cancellationToken
        );

        if (!isApproved)
        {
            challenge.FailedAttempts++;
            challenge.LastFailedAttemptAtUtc = DateTime.UtcNow;

            // Check if max attempts exceeded
            if (challenge.FailedAttempts >= MfaChallengeOptions.MaxFailedAttempts)
            {
                challenge.Status = MfaChallengeStatuses.Locked;
                await _mfaChallengeRepository.UpdateAsync(challenge, cancellationToken);

                await _auditService.TrackAuthenticationEventAsync(
                    challenge.UserId,
                    null,
                    "mfa_challenge_verify",
                    challenge.Method,
                    false,
                    $"Maximum failed attempts ({MfaChallengeOptions.MaxFailedAttempts}) exceeded",
                    cancellationToken
                );

                return Result<LoginResponse>.Failure(
                    "Too many failed verification attempts. Please try again later.",
                    StatusCodes.Status429TooManyRequests
                );
            }

            challenge.Status = MfaChallengeStatuses.Pending;
            await _mfaChallengeRepository.UpdateAsync(challenge, cancellationToken);

            await _auditService.TrackAuthenticationEventAsync(
                challenge.UserId,
                null,
                "mfa_challenge_verify",
                challenge.Method,
                false,
                "Invalid OTP",
                cancellationToken
            );

            return Result<LoginResponse>.Failure("Invalid OTP code.", StatusCodes.Status401Unauthorized);
        }

        var user = await _userRepository.GetByIdAsync(challenge.UserId, cancellationToken);
        if (user is null)
        {
            return Result<LoginResponse>.Failure("User not found.", StatusCodes.Status404NotFound);
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);

        challenge.Status = MfaChallengeStatuses.Verified;
        challenge.ContinuationToken = CreateContinuationToken();
        challenge.StepVersion += 1;
        challenge.VerifiedAtUtc = DateTime.UtcNow;

        await _mfaChallengeRepository.UpdateAsync(challenge, cancellationToken);

        await _auditService.TrackAuthenticationEventAsync(
            challenge.UserId,
            user.Username,
            "mfa_challenge_verify",
            challenge.Method,
            true,
            null,
            cancellationToken
        );

        await _accessTokenSessionRepository.RevokeAllActiveByUserAsync(
            user.Id,
            "new_login",
            cancellationToken
        );
        await _mfaTempTokenSessionRepository.RevokeAllActiveByUserAsync(
            user.Id,
            "new_login",
            cancellationToken
        );

        var (accessToken, refreshToken) = await _sessionFactory.CreateAuthenticatedSessionAsync(
            user,
            challenge.IpAddress,
            challenge.UserAgent,
            cancellationToken
        );

        return Result<LoginResponse>.Success(
            new LoginResponse
            {
                Status = "Authenticated",
                MfaRequired = false,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = 15 * 60,
                AllowedMfaMethods = await GetAllowedMethodsAsync(user.Id, cancellationToken),
            },
            "MFA verification succeeded."
        );
    }

    public async Task<Result<StartMfaEnrollmentResponse>> StartEnrollmentAsync(
        Guid userId,
        StartMfaEnrollmentRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        var hasRecentStepUp = await HasRecentManagementStepUpAsync(userId, cancellationToken);
        if (!hasRecentStepUp)
        {
            return Result<StartMfaEnrollmentResponse>.Failure(
                "Management step-up is required before enrolling additional MFA methods.",
                StatusCodes.Status403Forbidden
            );
        }

        return await StartEnrollmentCoreAsync(userId, request, ipAddress, userAgent, cancellationToken);
    }

    public async Task<Result<StartLoginEnrollmentResponse>> StartLoginEnrollmentAsync(
        Guid userId,
        Guid enrollmentSessionId,
        StartLoginEnrollmentRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        var session = await _mfaLoginEnrollmentSessionRepository.GetByIdAsync(
            enrollmentSessionId,
            cancellationToken
        );

        if (session is null || session.UserId != userId)
        {
            return Result<StartLoginEnrollmentResponse>.Failure(
                "Invalid login enrollment session.",
                StatusCodes.Status400BadRequest
            );
        }

        if (session.ExpiresAtUtc < DateTime.UtcNow)
        {
            return Result<StartLoginEnrollmentResponse>.Failure(
                "Login enrollment session has expired.",
                StatusCodes.Status410Gone
            );
        }

        if (session.Status == MfaLoginEnrollmentSessionStatuses.Completed || session.Status == MfaLoginEnrollmentSessionStatuses.Cancelled)
        {
            return Result<StartLoginEnrollmentResponse>.Failure(
                "LOGIN_ENROLLMENT_ALREADY_COMPLETED",
                StatusCodes.Status409Conflict
            );
        }

        if (!string.Equals(session.ContinuationToken, request.ContinuationToken, StringComparison.Ordinal))
        {
            return Result<StartLoginEnrollmentResponse>.Failure(
                "LOGIN_ENROLLMENT_ALREADY_ADVANCED",
                StatusCodes.Status409Conflict
            );
        }

        // Rate limiting: 3 OTP sends per 10 min per user (Twilio cost protection)
        var loginEnrollKey = $"login_enrollment_{userId}";
        if (!_rateLimitingService.IsAllowed(loginEnrollKey, maxAttempts: 3, windowSeconds: 600))
        {
            return Result<StartLoginEnrollmentResponse>.Failure(
                "Too many enrollment attempts. Please try again later.",
                StatusCodes.Status429TooManyRequests
            );
        }

        var startResult = await StartEnrollmentCoreAsync(
            userId,
            new StartMfaEnrollmentRequest { Method = request.Method, ContactValue = request.ContactValue },
            ipAddress,
            userAgent,
            cancellationToken
        );

        if (!startResult.IsSuccess || startResult.Data is null)
        {
            return Result<StartLoginEnrollmentResponse>.Failure(
                startResult.Error ?? startResult.Message ?? "Unable to start login enrollment.",
                startResult.StatusCode ?? StatusCodes.Status400BadRequest
            );
        }

        session.Status = MfaLoginEnrollmentSessionStatuses.EnrollmentInProgress;
        session.ChallengeId = startResult.Data.EnrollmentTransactionId;
        session.ContinuationToken = CreateContinuationToken();
        session.StepVersion += 1;
        session.ModifiedAtUtc = DateTime.UtcNow;
        await _mfaLoginEnrollmentSessionRepository.UpdateAsync(session, cancellationToken);

        return Result<StartLoginEnrollmentResponse>.Success(
            new StartLoginEnrollmentResponse
            {
                EnrollmentSessionId = session.Id,
                EnrollmentTransactionId = startResult.Data.EnrollmentTransactionId,
                SessionContinuationToken = session.ContinuationToken,
                ChallengeContinuationToken = startResult.Data.ContinuationToken,
                Method = startResult.Data.Method,
                Status = startResult.Data.Status,
                ExpiresAtUtc = startResult.Data.ExpiresAtUtc,
            },
            "Login enrollment challenge started."
        );
    }

    private async Task<Result<StartMfaEnrollmentResponse>> StartEnrollmentCoreAsync(
        Guid userId,
        StartMfaEnrollmentRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        var normalizedMethod = request.Method.Trim().ToLowerInvariant();
        if (normalizedMethod != MfaMethodTypes.Sms && normalizedMethod != MfaMethodTypes.Email)
        {
            return Result<StartMfaEnrollmentResponse>.Failure(
                "Only sms and email enrollment are supported in this endpoint.",
                StatusCodes.Status400BadRequest
            );
        }

        var contact = request.ContactValue.Trim();
        if (string.IsNullOrWhiteSpace(contact))
        {
            return Result<StartMfaEnrollmentResponse>.Failure(
                "Contact value is required.",
                StatusCodes.Status400BadRequest
            );
        }

        // R1/R2: Reject if method already active — user should reconfigure instead
        var existingMethod = await _mfaMethodRepository.GetByUserIdAndMethodAsync(
            userId, normalizedMethod, cancellationToken);

        if (existingMethod is not null && existingMethod.IsEnabled)
        {
            return Result<StartMfaEnrollmentResponse>.Failure(
                $"MFA method '{normalizedMethod}' is already configured. Use reconfigure to update it.",
                StatusCodes.Status409Conflict
            );
        }

        // R3: Reject if contactValue is already used by another active user
        var contactInUse = await _mfaMethodRepository.IsContactValueInUseAsync(
            contact, normalizedMethod, userId, cancellationToken);

        if (contactInUse)
        {
            return Result<StartMfaEnrollmentResponse>.Failure(
                "This contact value is already registered with another account.",
                StatusCodes.Status409Conflict
            );
        }

        // Rate limiting: 3 OTP sends per 15 min per user (Twilio cost protection)
        var enrollRateLimitKey = $"enrollment_otp_{userId}";
        if (!_rateLimitingService.IsAllowed(enrollRateLimitKey, maxAttempts: 3, windowSeconds: 900))
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.mfa.enrollment.rate_limited",
                "Warning",
                false,
                userId,
                null,
                "Enrollment OTP rate limit exceeded",
                new { method = normalizedMethod },
                cancellationToken
            );

            return Result<StartMfaEnrollmentResponse>.Failure(
                "Too many enrollment attempts. Please try again later.",
                StatusCodes.Status429TooManyRequests
            );
        }

        var providerSid = await _twilioOtpService.StartVerificationAsync(
            contact,
            normalizedMethod,
            cancellationToken
        );

        var challenge = new MfaChallenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = MfaChallengePurposes.Enrollment,
            ContinuationToken = CreateContinuationToken(),
            StepVersion = 1,
            Method = normalizedMethod,
            Provider = "twilio",
            ProviderRequestId = providerSid,
            Channel = normalizedMethod,
            ContactValue = contact,
            Status = MfaChallengeStatuses.Pending,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(MfaChallengeOptions.ChallengeExpirationMinutes),
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAtUtc = DateTime.UtcNow,
        };

        await _mfaChallengeRepository.AddAsync(challenge, cancellationToken);

        await _auditService.TrackAuthenticationEventAsync(
            userId,
            null,
            "mfa_enrollment_start",
            normalizedMethod,
            true,
            null,
            cancellationToken
        );

        return Result<StartMfaEnrollmentResponse>.Success(
            new StartMfaEnrollmentResponse
            {
                EnrollmentTransactionId = challenge.Id,
                ContinuationToken = challenge.ContinuationToken,
                Method = normalizedMethod,
                Status = challenge.Status,
                ExpiresAtUtc = challenge.ExpiresAtUtc,
            },
            "MFA enrollment challenge started."
        );
    }

    public async Task<Result<VerifyMfaEnrollmentResponse>> VerifyEnrollmentAsync(
        Guid userId,
        VerifyMfaEnrollmentRequest request,
        CancellationToken cancellationToken
    )
    {
        var hasRecentStepUp = await HasRecentManagementStepUpAsync(userId, cancellationToken);
        if (!hasRecentStepUp)
        {
            return Result<VerifyMfaEnrollmentResponse>.Failure(
                "Management step-up is required before enrolling additional MFA methods.",
                StatusCodes.Status403Forbidden
            );
        }

        return await VerifyEnrollmentCoreAsync(userId, request, cancellationToken);
    }

    public async Task<Result<VerifyLoginEnrollmentResponse>> VerifyLoginEnrollmentAsync(
        Guid userId,
        Guid enrollmentSessionId,
        VerifyLoginEnrollmentRequest request,
        CancellationToken cancellationToken
    )
    {
        var session = await _mfaLoginEnrollmentSessionRepository.GetByIdAsync(
            enrollmentSessionId,
            cancellationToken
        );

        if (session is null || session.UserId != userId)
        {
            return Result<VerifyLoginEnrollmentResponse>.Failure(
                "Invalid login enrollment session.",
                StatusCodes.Status400BadRequest
            );
        }

        if (session.ExpiresAtUtc < DateTime.UtcNow)
        {
            return Result<VerifyLoginEnrollmentResponse>.Failure(
                "Login enrollment session has expired.",
                StatusCodes.Status410Gone
            );
        }

        var verifyResult = await VerifyEnrollmentCoreAsync(
            userId,
            new VerifyMfaEnrollmentRequest
            {
                EnrollmentTransactionId = request.EnrollmentTransactionId,
                ContinuationToken = request.ContinuationToken,
                Code = request.Code,
            },
            cancellationToken
        );

        if (!verifyResult.IsSuccess || verifyResult.Data is null)
        {
            return Result<VerifyLoginEnrollmentResponse>.Failure(
                verifyResult.Error ?? verifyResult.Message ?? "Unable to verify login enrollment.",
                verifyResult.StatusCode ?? StatusCodes.Status400BadRequest
            );
        }

        session.Status = MfaLoginEnrollmentSessionStatuses.ReadyToComplete;
        session.ContinuationToken = CreateContinuationToken();
        session.StepVersion += 1;
        session.ModifiedAtUtc = DateTime.UtcNow;
        await _mfaLoginEnrollmentSessionRepository.UpdateAsync(session, cancellationToken);

        var remainingSetupOptions = (await GetAvailableSetupMethodsAsync(userId, cancellationToken))
            .Where(x => x == MfaMethodTypes.Sms || x == MfaMethodTypes.Email)
            .ToList();

        return Result<VerifyLoginEnrollmentResponse>.Success(
            new VerifyLoginEnrollmentResponse
            {
                EnrollmentSessionId = session.Id,
                SessionStatus = session.Status,
                SessionContinuationToken = session.ContinuationToken,
                Method = verifyResult.Data.Method,
                IsVerified = verifyResult.Data.IsVerified,
                RecoveryCodes = verifyResult.Data.RecoveryCodes,
                RemainingSetupOptions = remainingSetupOptions,
            },
            verifyResult.Message ?? "Login enrollment verified."
        );
    }

    public async Task<Result<LoginResponse>> CompleteLoginEnrollmentSessionAsync(
        Guid userId,
        Guid enrollmentSessionId,
        string continuationToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        var session = await _mfaLoginEnrollmentSessionRepository.GetByIdAsync(
            enrollmentSessionId,
            cancellationToken
        );

        if (session is null || session.UserId != userId)
        {
            return Result<LoginResponse>.Failure(
                "Login enrollment session not found.",
                StatusCodes.Status404NotFound
            );
        }

        if (session.ExpiresAtUtc < DateTime.UtcNow)
        {
            return Result<LoginResponse>.Failure(
                "Login enrollment session has expired.",
                StatusCodes.Status410Gone
            );
        }

        if (session.Status == MfaLoginEnrollmentSessionStatuses.Completed || session.Status == MfaLoginEnrollmentSessionStatuses.Cancelled)
        {
            return Result<LoginResponse>.Failure(
                "LOGIN_ENROLLMENT_ALREADY_COMPLETED",
                StatusCodes.Status409Conflict
            );
        }

        if (!string.Equals(session.ContinuationToken, continuationToken, StringComparison.Ordinal))
        {
            return Result<LoginResponse>.Failure(
                "LOGIN_ENROLLMENT_ALREADY_ADVANCED",
                StatusCodes.Status409Conflict
            );
        }

        var allowedMethods = await GetAllowedMethodsAsync(userId, cancellationToken);
        if (allowedMethods.Count == 0)
        {
            return Result<LoginResponse>.Failure(
                "At least one verified MFA method is required before completing authentication.",
                StatusCodes.Status409Conflict
            );
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result<LoginResponse>.Failure("User not found.", StatusCodes.Status404NotFound);
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);

        await _accessTokenSessionRepository.RevokeAllActiveByUserAsync(user.Id, "new_login", cancellationToken);
        await _mfaTempTokenSessionRepository.RevokeAllActiveByUserAsync(user.Id, "new_login", cancellationToken);
        await _mfaLoginEnrollmentSessionRepository.RevokeAllActiveByUserAsync(user.Id, cancellationToken);

        session.Status = MfaLoginEnrollmentSessionStatuses.Completed;
        session.CompletedAtUtc = DateTime.UtcNow;
        session.ExpiresAtUtc = DateTime.UtcNow;
        session.ModifiedAtUtc = DateTime.UtcNow;
        await _mfaLoginEnrollmentSessionRepository.UpdateAsync(session, cancellationToken);

        var (accessToken, refreshToken) = await _sessionFactory.CreateAuthenticatedSessionAsync(
            user,
            ipAddress,
            userAgent,
            cancellationToken
        );

        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.mfa.login_enrollment.completed",
            "Information",
            true,
            user.Id,
            user.Username,
            null,
            new { sessionId = session.Id, allowedMethods },
            cancellationToken
        );

        return Result<LoginResponse>.Success(
            new LoginResponse
            {
                Status = "Authenticated",
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = 15 * 60,
                AllowedMfaMethods = allowedMethods,
            },
            "Authentication succeeded."
        );
    }

    private async Task<Result<VerifyMfaEnrollmentResponse>> VerifyEnrollmentCoreAsync(
        Guid userId,
        VerifyMfaEnrollmentRequest request,
        CancellationToken cancellationToken
    )
    {
        var challenge = await _mfaChallengeRepository.GetByIdAsync(
            request.EnrollmentTransactionId,
            cancellationToken
        );

        if (challenge is null || challenge.UserId != userId)
        {
            return Result<VerifyMfaEnrollmentResponse>.Failure(
                "Invalid enrollment challenge.",
                StatusCodes.Status400BadRequest
            );
        }

        if (challenge.ExpiresAtUtc < DateTime.UtcNow)
        {
            return Result<VerifyMfaEnrollmentResponse>.Failure(
                "Enrollment challenge has expired.",
                StatusCodes.Status410Gone
            );
        }

        if (challenge.Purpose != MfaChallengePurposes.Enrollment)
        {
            return Result<VerifyMfaEnrollmentResponse>.Failure(
                "Invalid challenge purpose for enrollment.",
                StatusCodes.Status400BadRequest
            );
        }

        if (challenge.Status != MfaChallengeStatuses.Pending)
        {
            return Result<VerifyMfaEnrollmentResponse>.Failure(
                "MFA_FLOW_ALREADY_ADVANCED",
                StatusCodes.Status409Conflict
            );
        }

        if (!string.Equals(challenge.ContinuationToken, request.ContinuationToken, StringComparison.Ordinal))
        {
            return Result<VerifyMfaEnrollmentResponse>.Failure(
                "MFA_FLOW_ALREADY_ADVANCED",
                StatusCodes.Status409Conflict
            );
        }

        if (string.IsNullOrWhiteSpace(challenge.Method) || string.IsNullOrWhiteSpace(challenge.ContactValue))
        {
            return Result<VerifyMfaEnrollmentResponse>.Failure(
                "Enrollment challenge is missing required data.",
                StatusCodes.Status400BadRequest
            );
        }

        var isApproved = await _twilioOtpService.CheckVerificationAsync(
            challenge.ContactValue,
            request.Code,
            cancellationToken
        );

        if (!isApproved)
        {
            challenge.FailedAttempts++;
            challenge.LastFailedAttemptAtUtc = DateTime.UtcNow;

            if (challenge.FailedAttempts >= MfaChallengeOptions.MaxFailedAttempts)
            {
                challenge.Status = MfaChallengeStatuses.Locked;
                await _mfaChallengeRepository.UpdateAsync(challenge, cancellationToken);

                await _auditService.TrackAuthenticationEventAsync(
                    userId,
                    null,
                    "mfa_enrollment_verify",
                    challenge.Method,
                    false,
                    $"Maximum failed attempts ({MfaChallengeOptions.MaxFailedAttempts}) exceeded",
                    cancellationToken
                );

                return Result<VerifyMfaEnrollmentResponse>.Failure(
                    "Too many failed verification attempts. Please try again later.",
                    StatusCodes.Status429TooManyRequests
                );
            }

            challenge.Status = MfaChallengeStatuses.Pending;
            await _mfaChallengeRepository.UpdateAsync(challenge, cancellationToken);

            await _auditService.TrackAuthenticationEventAsync(
                userId,
                null,
                "mfa_enrollment_verify",
                challenge.Method,
                false,
                "Invalid OTP",
                cancellationToken
            );

            return Result<VerifyMfaEnrollmentResponse>.Failure(
                "Invalid OTP code.",
                StatusCodes.Status401Unauthorized
            );
        }

        var existing = await _mfaMethodRepository.GetByUserIdAndMethodAsync(
            userId,
            challenge.Method,
            cancellationToken
        );

        if (existing is null)
        {
            await _mfaMethodRepository.AddAsync(
                new UserMfaMethod
                {
                    UserId = userId,
                    Method = challenge.Method,
                    IsEnabled = true,
                    IsPrimary = false,
                    IsVerified = true,
                    ContactValue = challenge.ContactValue,
                    CreatedAtUtc = DateTime.UtcNow,
                    ModifiedAtUtc = DateTime.UtcNow,
                },
                cancellationToken
            );
        }
        else
        {
            existing.IsEnabled = true;
            existing.IsVerified = true;
            existing.ContactValue = challenge.ContactValue;
            existing.ModifiedAtUtc = DateTime.UtcNow;

            await _mfaMethodRepository.UpdateAsync(existing, cancellationToken);
        }

        challenge.Status = MfaChallengeStatuses.Verified;
        challenge.ContinuationToken = CreateContinuationToken();
        challenge.StepVersion += 1;
        challenge.VerifiedAtUtc = DateTime.UtcNow;
        await _mfaChallengeRepository.UpdateAsync(challenge, cancellationToken);

        await _auditService.TrackAuthenticationEventAsync(
            userId,
            null,
            "mfa_enrollment_verify",
            challenge.Method,
            true,
            null,
            cancellationToken
        );

        var recoveryCodes = await EnsureRecoveryCodesIssuedAsync(userId, cancellationToken);

        return Result<VerifyMfaEnrollmentResponse>.Success(
            new VerifyMfaEnrollmentResponse
            {
                Method = challenge.Method,
                IsVerified = true,
                RecoveryCodes = recoveryCodes,
            },
            recoveryCodes.Count > 0
                ? "MFA method enrollment verified. Recovery codes were issued once."
                : "MFA method enrollment verified. Recovery codes already exist and were not reissued."
        );
    }

    public async Task<Result<RemoveMfaMethodResponse>> RemoveMethodAsync(
        Guid userId,
        string method,
        CancellationToken cancellationToken
    )
    {
        var hasRecentManagementStepUp = await HasRecentManagementStepUpAsync(userId, cancellationToken);
        if (!hasRecentManagementStepUp)
        {
            return Result<RemoveMfaMethodResponse>.Failure(
                "Management step-up is required before modifying MFA methods.",
                StatusCodes.Status403Forbidden
            );
        }

        var normalizedMethod = method.Trim().ToLowerInvariant();
        if (normalizedMethod == MfaMethodTypes.RecoveryCode)
        {
            return Result<RemoveMfaMethodResponse>.Failure(
                "Use recovery code regeneration to rotate recovery codes.",
                StatusCodes.Status400BadRequest
            );
        }

        if (
            normalizedMethod != MfaMethodTypes.Sms
            && normalizedMethod != MfaMethodTypes.Email
            && normalizedMethod != MfaMethodTypes.Fido2
        )
        {
            return Result<RemoveMfaMethodResponse>.Failure(
                "Unsupported MFA method.",
                StatusCodes.Status400BadRequest
            );
        }

        var currentMethod = await _mfaMethodRepository.GetByUserIdAndMethodAsync(
            userId,
            normalizedMethod,
            cancellationToken
        );

        if (currentMethod is null || !currentMethod.IsEnabled)
        {
            return Result<RemoveMfaMethodResponse>.Failure(
                "MFA method is not enabled for this user.",
                StatusCodes.Status400BadRequest
            );
        }

        var enabledMethods = await _mfaMethodRepository.GetEnabledByUserIdAsync(userId, cancellationToken);
        var hasAlternativeEnabled = enabledMethods.Any(
            x => !string.Equals(x.Method, normalizedMethod, StringComparison.OrdinalIgnoreCase)
        );
        var hasRecoveryCodes = await _userRecoveryCodeRepository.HasUnusedCodesAsync(userId, cancellationToken);

        if (!hasAlternativeEnabled && !hasRecoveryCodes)
        {
            return Result<RemoveMfaMethodResponse>.Failure(
                "Cannot remove the last MFA recovery path.",
                StatusCodes.Status409Conflict
            );
        }

        currentMethod.IsEnabled = false;
        currentMethod.ModifiedAtUtc = DateTime.UtcNow;
        await _mfaMethodRepository.UpdateAsync(currentMethod, cancellationToken);

        if (normalizedMethod == MfaMethodTypes.Fido2)
        {
            await _userRepository.DisableFido2MfaAsync(userId, cancellationToken);
        }

        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.mfa.method.remove",
            "Information",
            true,
            userId,
            null,
            null,
            new { method = normalizedMethod },
            cancellationToken
        );

        return Result<RemoveMfaMethodResponse>.Success(
            new RemoveMfaMethodResponse
            {
                Method = normalizedMethod,
                IsEnabled = false,
            },
            "MFA method removed."
        );
    }

    public async Task<Result<StartMfaReconfigureResponse>> StartReconfigureMethodAsync(
        Guid userId,
        string method,
        StartMfaReconfigureRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        var hasRecentStepUp = await HasRecentManagementStepUpAsync(userId, cancellationToken);

        if (!hasRecentStepUp)
        {
            return Result<StartMfaReconfigureResponse>.Failure(
                "Management step-up is required before reconfiguring MFA methods.",
                StatusCodes.Status403Forbidden
            );
        }

        var normalizedMethod = method.Trim().ToLowerInvariant();
        if (normalizedMethod == MfaMethodTypes.Fido2)
        {
            return Result<StartMfaReconfigureResponse>.Failure(
                "Use FIDO2 enrollment flow to reconfigure passkeys.",
                StatusCodes.Status400BadRequest
            );
        }

        if (normalizedMethod != MfaMethodTypes.Sms && normalizedMethod != MfaMethodTypes.Email)
        {
            return Result<StartMfaReconfigureResponse>.Failure(
                "Only sms and email reconfiguration are supported in this endpoint.",
                StatusCodes.Status400BadRequest
            );
        }

        var existing = await _mfaMethodRepository.GetEnabledByUserIdAndMethodAsync(
            userId,
            normalizedMethod,
            cancellationToken
        );

        if (existing is null)
        {
            return Result<StartMfaReconfigureResponse>.Failure(
                "MFA method is not enabled for this user.",
                StatusCodes.Status400BadRequest
            );
        }

        var contact = request.ContactValue.Trim();
        if (string.IsNullOrWhiteSpace(contact))
        {
            return Result<StartMfaReconfigureResponse>.Failure(
                "Contact value is required.",
                StatusCodes.Status400BadRequest
            );
        }

        // R3: Reject if contactValue is already used by another active user
        var contactInUse = await _mfaMethodRepository.IsContactValueInUseAsync(
            contact, normalizedMethod, userId, cancellationToken);

        if (contactInUse)
        {
            return Result<StartMfaReconfigureResponse>.Failure(
                "This contact value is already registered with another account.",
                StatusCodes.Status409Conflict
            );
        }

        // Rate limiting: 3 OTP sends per 15 min per user (Twilio cost protection)
        var reconfigRateLimitKey = $"reconfigure_otp_{userId}";
        if (!_rateLimitingService.IsAllowed(reconfigRateLimitKey, maxAttempts: 3, windowSeconds: 900))
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.mfa.reconfigure.rate_limited",
                "Warning",
                false,
                userId,
                null,
                "Reconfigure OTP rate limit exceeded",
                new { method = normalizedMethod },
                cancellationToken
            );

            return Result<StartMfaReconfigureResponse>.Failure(
                "Too many reconfiguration attempts. Please try again later.",
                StatusCodes.Status429TooManyRequests
            );
        }

        var providerSid = await _twilioOtpService.StartVerificationAsync(
            contact,
            normalizedMethod,
            cancellationToken
        );

        var challenge = new MfaChallenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = MfaChallengePurposes.Reconfigure,
            Method = normalizedMethod,
            Provider = "twilio",
            ProviderRequestId = providerSid,
            Channel = normalizedMethod,
            ContactValue = contact,
            Status = MfaChallengeStatuses.Pending,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(MfaChallengeOptions.ChallengeExpirationMinutes),
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAtUtc = DateTime.UtcNow,
        };

        await _mfaChallengeRepository.AddAsync(challenge, cancellationToken);

        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.mfa.method.reconfigure.start",
            "Information",
            true,
            userId,
            null,
            null,
            new { method = normalizedMethod, challengeId = challenge.Id },
            cancellationToken
        );

        return Result<StartMfaReconfigureResponse>.Success(
            new StartMfaReconfigureResponse
            {
                ReconfigureTransactionId = challenge.Id,
                Method = normalizedMethod,
                Status = challenge.Status,
                ExpiresAtUtc = challenge.ExpiresAtUtc,
            },
            "MFA method reconfiguration challenge started."
        );
    }

    public async Task<Result<CompleteMfaReconfigureResponse>> CompleteReconfigureMethodAsync(
        Guid userId,
        string method,
        CompleteMfaReconfigureRequest request,
        CancellationToken cancellationToken
    )
    {
        var hasRecentStepUp = await HasRecentManagementStepUpAsync(userId, cancellationToken);

        if (!hasRecentStepUp)
        {
            return Result<CompleteMfaReconfigureResponse>.Failure(
                "Management step-up is required before reconfiguring MFA methods.",
                StatusCodes.Status403Forbidden
            );
        }

        var normalizedMethod = method.Trim().ToLowerInvariant();
        if (normalizedMethod != MfaMethodTypes.Sms && normalizedMethod != MfaMethodTypes.Email)
        {
            return Result<CompleteMfaReconfigureResponse>.Failure(
                "Only sms and email reconfiguration are supported in this endpoint.",
                StatusCodes.Status400BadRequest
            );
        }

        var challenge = await _mfaChallengeRepository.GetByIdAsync(
            request.ReconfigureTransactionId,
            cancellationToken
        );

        if (challenge is null || challenge.UserId != userId || challenge.ExpiresAtUtc < DateTime.UtcNow)
        {
            return Result<CompleteMfaReconfigureResponse>.Failure(
                "Invalid or expired reconfiguration challenge.",
                StatusCodes.Status400BadRequest
            );
        }

        if (challenge.Purpose != MfaChallengePurposes.Reconfigure)
        {
            return Result<CompleteMfaReconfigureResponse>.Failure(
                "Invalid challenge purpose for reconfiguration.",
                StatusCodes.Status400BadRequest
            );
        }

        if (!string.Equals(challenge.Method, normalizedMethod, StringComparison.OrdinalIgnoreCase))
        {
            return Result<CompleteMfaReconfigureResponse>.Failure(
                "Reconfiguration method mismatch.",
                StatusCodes.Status400BadRequest
            );
        }

        if (
            challenge.Status != MfaChallengeStatuses.Pending
            || string.IsNullOrWhiteSpace(challenge.ContactValue)
        )
        {
            return Result<CompleteMfaReconfigureResponse>.Failure(
                "MFA_FLOW_ALREADY_ADVANCED",
                StatusCodes.Status409Conflict
            );
        }

        var isApproved = await _twilioOtpService.CheckVerificationAsync(
            challenge.ContactValue,
            request.Code,
            cancellationToken
        );

        if (!isApproved)
        {
            challenge.FailedAttempts++;
            challenge.LastFailedAttemptAtUtc = DateTime.UtcNow;

            if (challenge.FailedAttempts >= MfaChallengeOptions.MaxFailedAttempts)
            {
                challenge.Status = MfaChallengeStatuses.Locked;
                await _mfaChallengeRepository.UpdateAsync(challenge, cancellationToken);

                await _auditService.TrackSecurityEventAsync(
                    "Authentication",
                    "auth.mfa.method.reconfigure.fail",
                    "Warning",
                    false,
                    userId,
                    null,
                    $"Maximum failed attempts ({MfaChallengeOptions.MaxFailedAttempts}) exceeded",
                    new { method = normalizedMethod },
                    cancellationToken
                );

                return Result<CompleteMfaReconfigureResponse>.Failure(
                    "Too many failed verification attempts. Please try again later.",
                    StatusCodes.Status429TooManyRequests
                );
            }

            challenge.Status = MfaChallengeStatuses.Pending;
            await _mfaChallengeRepository.UpdateAsync(challenge, cancellationToken);

            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.mfa.method.reconfigure.fail",
                "Warning",
                false,
                userId,
                null,
                "Invalid OTP",
                new { method = normalizedMethod },
                cancellationToken
            );

            return Result<CompleteMfaReconfigureResponse>.Failure(
                "Invalid OTP code.",
                StatusCodes.Status401Unauthorized
            );
        }

        var existing = await _mfaMethodRepository.GetByUserIdAndMethodAsync(
            userId,
            normalizedMethod,
            cancellationToken
        );

        if (existing is null)
        {
            await _mfaMethodRepository.AddAsync(
                new UserMfaMethod
                {
                    UserId = userId,
                    Method = normalizedMethod,
                    IsEnabled = true,
                    IsPrimary = false,
                    IsVerified = true,
                    ContactValue = challenge.ContactValue,
                    CreatedAtUtc = DateTime.UtcNow,
                    ModifiedAtUtc = DateTime.UtcNow,
                },
                cancellationToken
            );
        }
        else
        {
            existing.IsEnabled = true;
            existing.IsVerified = true;
            existing.ContactValue = challenge.ContactValue;
            existing.ModifiedAtUtc = DateTime.UtcNow;
            await _mfaMethodRepository.UpdateAsync(existing, cancellationToken);
        }

        challenge.Status = MfaChallengeStatuses.Verified;
        challenge.VerifiedAtUtc = DateTime.UtcNow;
        await _mfaChallengeRepository.UpdateAsync(challenge, cancellationToken);

        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.mfa.method.reconfigure.complete",
            "Information",
            true,
            userId,
            null,
            null,
            new { method = normalizedMethod },
            cancellationToken
        );

        return Result<CompleteMfaReconfigureResponse>.Success(
            new CompleteMfaReconfigureResponse
            {
                Method = normalizedMethod,
                IsReconfigured = true,
            },
            "MFA method reconfigured."
        );
    }

    private async Task<List<string>> EnsureRecoveryCodesIssuedAsync(
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        var (existingBatch, _) = await _userRecoveryCodeRepository.GetStatusAsync(userId, cancellationToken);
        if (existingBatch is not null)
        {
            return [];
        }

        var plainCodes = Enumerable
            .Range(0, 10)
            .Select(_ => GenerateRecoveryCode())
            .ToList();

        var hashes = plainCodes
            .Select(code => PasswordHasher.Hash(NormalizeRecoveryCode(code)))
            .ToList();

        await _userRecoveryCodeRepository.ReplaceBatchAsync(userId, hashes, cancellationToken);

        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.mfa.recovery_codes.issued",
            "Information",
            true,
            userId,
            null,
            null,
            new { count = plainCodes.Count },
            cancellationToken
        );

        return plainCodes;
    }

    private static string GenerateRecoveryCode() => RecoveryCodeHelper.Generate();

    private static string NormalizeRecoveryCode(string requestedCode) => RecoveryCodeHelper.Normalize(requestedCode);

    private Task<bool> HasRecentManagementStepUpAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _mfaManagementSessionRepository.HasActiveStepUpSessionAsync(
            userId,
            DateTime.UtcNow.AddMinutes(-ManagementStepUpWindowMinutes),
            cancellationToken
        );
    }

    private static string CreateContinuationToken()
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);

        return Convert
            .ToBase64String(buffer)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
