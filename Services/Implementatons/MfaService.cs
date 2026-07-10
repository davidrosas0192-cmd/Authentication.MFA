using Authentication.Fido2.Common;
using Authentication.Fido2.Constants;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.DTOs.Mfa;
using Authentication.Fido2.Entities;
using Authentication.Fido2.Services.Interfaces;

namespace Authentication.Fido2.Services.Implementations;

public class MfaService : IMfaService
{
    private readonly IUserMfaMethodRepository _mfaMethodRepository;
    private readonly IMfaChallengeRepository _mfaChallengeRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITwilioOtpService _twilioOtpService;
    private readonly ITokenService _tokenService;
    private readonly IAuditService _auditService;

    public MfaService(
        IUserMfaMethodRepository mfaMethodRepository,
        IMfaChallengeRepository mfaChallengeRepository,
        IUserRepository userRepository,
        ITwilioOtpService twilioOtpService,
        ITokenService tokenService,
        IAuditService auditService
    )
    {
        _mfaMethodRepository = mfaMethodRepository;
        _mfaChallengeRepository = mfaChallengeRepository;
        _userRepository = userRepository;
        _twilioOtpService = twilioOtpService;
        _tokenService = tokenService;
        _auditService = auditService;
    }

    public async Task<List<string>> GetAllowedMethodsAsync(long userId, CancellationToken cancellationToken)
    {
        var methods = await _mfaMethodRepository.GetEnabledByUserIdAsync(userId, cancellationToken);

        return methods.Select(x => x.Method).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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

        if (challenge is null || challenge.ExpiresAtUtc < DateTime.UtcNow)
        {
            return Result<StartMfaChallengeResponse>.Failure(
                "Invalid or expired MFA transaction.",
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
        VerifyMfaChallengeRequest request,
        CancellationToken cancellationToken
    )
    {
        var challenge = await _mfaChallengeRepository.GetByIdAsync(
            request.MfaTransactionId,
            cancellationToken
        );

        if (challenge is null || challenge.ExpiresAtUtc < DateTime.UtcNow)
        {
            return Result<LoginResponse>.Failure(
                "Invalid or expired MFA challenge.",
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
            method.ContactValue,
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

        return Result<LoginResponse>.Success(
            new LoginResponse
            {
                Status = "Authenticated",
                MfaRequired = false,
                AccessToken = _tokenService.CreateAccessToken(user),
                RefreshToken = _tokenService.CreateRefreshToken(),
                ExpiresIn = 15 * 60,
            },
            "MFA verification succeeded."
        );
    }
}
