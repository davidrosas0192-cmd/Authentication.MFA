using Authentication.Fido2.Common;
using Authentication.Fido2.Constants;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.DTOs.Mfa;
using Authentication.Fido2.Entities;
using Authentication.Fido2.Options;
using Authentication.Fido2.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Authentication.Fido2.Services.Implementations;

public class MfaService : IMfaService
{
    private readonly IUserMfaMethodRepository _mfaMethodRepository;
    private readonly IMfaChallengeRepository _mfaChallengeRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITwilioOtpService _twilioOtpService;
    private readonly ITokenService _tokenService;
    private readonly IAccessTokenSessionRepository _accessTokenSessionRepository;
    private readonly IMfaTempTokenSessionRepository _mfaTempTokenSessionRepository;
    private readonly IAuditService _auditService;
    private readonly JwtOptions _jwtOptions;

    public MfaService(
        IUserMfaMethodRepository mfaMethodRepository,
        IMfaChallengeRepository mfaChallengeRepository,
        IUserRepository userRepository,
        ITwilioOtpService twilioOtpService,
        ITokenService tokenService,
        IAccessTokenSessionRepository accessTokenSessionRepository,
        IMfaTempTokenSessionRepository mfaTempTokenSessionRepository,
        IAuditService auditService,
        IOptions<JwtOptions> jwtOptions
    )
    {
        _mfaMethodRepository = mfaMethodRepository;
        _mfaChallengeRepository = mfaChallengeRepository;
        _userRepository = userRepository;
        _twilioOtpService = twilioOtpService;
        _tokenService = tokenService;
        _accessTokenSessionRepository = accessTokenSessionRepository;
        _mfaTempTokenSessionRepository = mfaTempTokenSessionRepository;
        _auditService = auditService;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<List<string>> GetAllowedMethodsAsync(long userId, CancellationToken cancellationToken)
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

        if (user.IsFido2MfaEnabled && !normalized.Contains(MfaMethodTypes.Fido2, StringComparer.OrdinalIgnoreCase))
        {
            normalized.Add(MfaMethodTypes.Fido2);
        }

        return normalized;
    }

    public async Task<List<string>> GetAvailableSetupMethodsAsync(long userId, CancellationToken cancellationToken)
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

    public async Task<Guid> CreateSelectionChallengeAsync(
        long userId,
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
            Status = MfaChallengeStatuses.PendingSelection,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAtUtc = DateTime.UtcNow,
        };

        await _mfaChallengeRepository.AddAsync(challenge, cancellationToken);

        return challenge.Id;
    }

    public async Task<Result<StartMfaChallengeResponse>> StartChallengeAsync(
        long userId,
        StartMfaChallengeRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        var challenge = await _mfaChallengeRepository.GetByIdAsync(
            request.MfaTransactionId,
            cancellationToken
        );

        if (challenge is null || challenge.ExpiresAtUtc < DateTime.UtcNow || challenge.UserId != userId)
        {
            return Result<StartMfaChallengeResponse>.Failure(
                "Invalid or expired MFA transaction.",
                StatusCodes.Status400BadRequest
            );
        }

        if (challenge.Purpose != MfaChallengePurposes.Login)
        {
            return Result<StartMfaChallengeResponse>.Failure(
                "Invalid MFA transaction purpose.",
                StatusCodes.Status400BadRequest
            );
        }

        var normalizedMethod = request.Method.Trim().ToLowerInvariant();
        if (normalizedMethod != MfaMethodTypes.Sms && normalizedMethod != MfaMethodTypes.Email)
        {
            return Result<StartMfaChallengeResponse>.Failure(
                "Only sms and email OTP challenge start are supported in this endpoint.",
                StatusCodes.Status400BadRequest
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
        challenge.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5);
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
                Method = normalizedMethod,
                Status = challenge.Status,
                ExpiresAtUtc = challenge.ExpiresAtUtc,
            },
            "MFA OTP challenge started."
        );
    }

    public async Task<Result<LoginResponse>> VerifyChallengeAsync(
        long userId,
        VerifyMfaChallengeRequest request,
        CancellationToken cancellationToken
    )
    {
        var challenge = await _mfaChallengeRepository.GetByIdAsync(
            request.MfaTransactionId,
            cancellationToken
        );

        if (challenge is null || challenge.ExpiresAtUtc < DateTime.UtcNow || challenge.UserId != userId)
        {
            return Result<LoginResponse>.Failure(
                "Invalid or expired MFA challenge.",
                StatusCodes.Status400BadRequest
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
                "MFA challenge is not in a verifiable state.",
                StatusCodes.Status400BadRequest
            );
        }

        if (challenge.Method != MfaMethodTypes.Sms && challenge.Method != MfaMethodTypes.Email)
        {
            return Result<LoginResponse>.Failure(
                "Only sms and email OTP verification is supported in this endpoint.",
                StatusCodes.Status400BadRequest
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
            request.Code,
            cancellationToken
        );

        if (!isApproved)
        {
            challenge.Status = MfaChallengeStatuses.Failed;
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

        var accessTokenJti = Guid.NewGuid().ToString("N");
        var accessToken = _tokenService.CreateAccessToken(user, accessTokenJti);
        var refreshToken = _tokenService.CreateRefreshToken();

        await _accessTokenSessionRepository.AddAsync(
            new AccessTokenSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenJti = accessTokenJti,
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
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = 15 * 60,
                AllowedMfaMethods = await GetAllowedMethodsAsync(user.Id, cancellationToken),
            },
            "MFA verification succeeded."
        );
    }

    public async Task<Result<StartMfaEnrollmentResponse>> StartEnrollmentAsync(
        long userId,
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
            Method = normalizedMethod,
            Provider = "twilio",
            ProviderRequestId = providerSid,
            Channel = normalizedMethod,
            ContactValue = contact,
            Status = MfaChallengeStatuses.Pending,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
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
                Method = normalizedMethod,
                Status = challenge.Status,
                ExpiresAtUtc = challenge.ExpiresAtUtc,
            },
            "MFA enrollment challenge started."
        );
    }

    public async Task<Result<VerifyMfaEnrollmentResponse>> VerifyEnrollmentAsync(
        long userId,
        VerifyMfaEnrollmentRequest request,
        CancellationToken cancellationToken
    )
    {
        var challenge = await _mfaChallengeRepository.GetByIdAsync(
            request.EnrollmentTransactionId,
            cancellationToken
        );

        if (challenge is null || challenge.UserId != userId || challenge.ExpiresAtUtc < DateTime.UtcNow)
        {
            return Result<VerifyMfaEnrollmentResponse>.Failure(
                "Invalid or expired enrollment challenge.",
                StatusCodes.Status400BadRequest
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
                "Enrollment challenge is not in a verifiable state.",
                StatusCodes.Status400BadRequest
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
            challenge.Status = MfaChallengeStatuses.Failed;
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
                    UpdatedAtUtc = DateTime.UtcNow,
                },
                cancellationToken
            );
        }
        else
        {
            existing.IsEnabled = true;
            existing.IsVerified = true;
            existing.ContactValue = challenge.ContactValue;
            existing.UpdatedAtUtc = DateTime.UtcNow;

            await _mfaMethodRepository.UpdateAsync(existing, cancellationToken);
        }

        challenge.Status = MfaChallengeStatuses.Verified;
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

        return Result<VerifyMfaEnrollmentResponse>.Success(
            new VerifyMfaEnrollmentResponse { Method = challenge.Method, IsVerified = true },
            "MFA method enrollment verified."
        );
    }
}
