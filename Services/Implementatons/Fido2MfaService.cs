using System.Security.Cryptography;
using System.Text;
using Authentication.Fido2.Common;
using Authentication.Fido2.Constants;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.DTOs.Fido2;
using Authentication.Fido2.Entities;
using Authentication.Fido2.Options;
using Authentication.Fido2.Services.Interfaces;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.Extensions.Options;

namespace Authentication.Fido2.Services.Implementations;

public class Fido2MfaService : IFido2MfaService
{
    private readonly IFido2 _fido2;
    private readonly IUserRepository _userRepository;
    private readonly IUserMfaMethodRepository _userMfaMethodRepository;
    private readonly IUserRecoveryCodeRepository _userRecoveryCodeRepository;
    private readonly IFido2CredentialRepository _credentialRepository;
    private readonly IFido2TransactionRepository _transactionRepository;
    private readonly ITokenService _tokenService;
    private readonly IMfaService _mfaService;
    private readonly IAccessTokenSessionRepository _accessTokenSessionRepository;
    private readonly IMfaTempTokenSessionRepository _mfaTempTokenSessionRepository;
    private readonly IAuditService _auditService;
    private readonly JwtOptions _jwtOptions;

    public Fido2MfaService(
        IFido2 fido2,
        IUserRepository userRepository,
        IUserMfaMethodRepository userMfaMethodRepository,
        IUserRecoveryCodeRepository userRecoveryCodeRepository,
        IFido2CredentialRepository credentialRepository,
        IFido2TransactionRepository transactionRepository,
        ITokenService tokenService,
        IMfaService mfaService,
        IAccessTokenSessionRepository accessTokenSessionRepository,
        IMfaTempTokenSessionRepository mfaTempTokenSessionRepository,
        IAuditService auditService,
        IOptions<JwtOptions> jwtOptions
    )
    {
        _fido2 = fido2 ?? throw new ArgumentNullException(nameof(fido2));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _userMfaMethodRepository =
            userMfaMethodRepository
            ?? throw new ArgumentNullException(nameof(userMfaMethodRepository));
        _userRecoveryCodeRepository =
            userRecoveryCodeRepository
            ?? throw new ArgumentNullException(nameof(userRecoveryCodeRepository));
        _credentialRepository =
            credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        _transactionRepository =
            transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _mfaService = mfaService ?? throw new ArgumentNullException(nameof(mfaService));
        _accessTokenSessionRepository =
            accessTokenSessionRepository
            ?? throw new ArgumentNullException(nameof(accessTokenSessionRepository));
        _mfaTempTokenSessionRepository =
            mfaTempTokenSessionRepository
            ?? throw new ArgumentNullException(nameof(mfaTempTokenSessionRepository));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<Result<Fido2OptionsResponse>> CreateEnrollmentOptionsAsync(
        long userId,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken
    )
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.fido2.enrollment.options",
                "Warning",
                false,
                userId,
                null,
                "User not found",
                null,
                cancellationToken
            );

            return Result<Fido2OptionsResponse>.Failure("User not found.", StatusCodes.Status404NotFound);
        }

        if (!user.IsActive)
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.fido2.enrollment.options",
                "Warning",
                false,
                user.Id,
                user.Username,
                "User inactive",
                null,
                cancellationToken
            );

            return Result<Fido2OptionsResponse>.Failure("User is inactive.", StatusCodes.Status403Forbidden);
        }

        var existingCredentials = await _credentialRepository.GetByUserIdAsync(
            user.Id,
            cancellationToken
        );

        var excludeCredentials = existingCredentials
            .Select(x => new PublicKeyCredentialDescriptor(x.CredentialId))
            .ToList();

        var fidoUser = new Fido2User
        {
            DisplayName = user.Username,
            Name = user.Email,
            Id = Encoding.UTF8.GetBytes(user.Id.ToString()),
        };

        var options = _fido2.RequestNewCredential(
            new RequestNewCredentialParams
            {
                User = fidoUser,
                ExcludeCredentials = excludeCredentials,
                AuthenticatorSelection = new AuthenticatorSelection
                {
                    ResidentKey = ResidentKeyRequirement.Preferred,
                    UserVerification = UserVerificationRequirement.Required,
                },
                AttestationPreference = AttestationConveyancePreference.None,
                Extensions = new AuthenticationExtensionsClientInputs { CredProps = true },
            }
        );

        var transaction = new Fido2Transaction
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Type = Fido2TransactionTypes.Registration,
            OptionsJson = options.ToJson(),
            IsUsed = false,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
        };

        await _transactionRepository.AddAsync(transaction, cancellationToken);

        await _auditService.TrackAuthenticationEventAsync(
            user.Id,
            user.Username,
            "fido2_enrollment_options",
            "fido2",
            true,
            null,
            cancellationToken
        );
        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.fido2.enrollment.options",
            "Information",
            true,
            user.Id,
            user.Username,
            null,
            new { transactionId = transaction.Id },
            cancellationToken
        );

        return Result<Fido2OptionsResponse>.Success(
            new Fido2OptionsResponse { TransactionId = transaction.Id, Options = options },
            "Enrollment options created."
        );
    }

    public async Task<Result<CompleteFido2EnrollmentResponse>> CompleteEnrollmentAsync(
        CompleteFido2EnrollmentRequest request,
        long userId,
        CancellationToken cancellationToken
    )
    {
        var transaction = await _transactionRepository.GetByIdAsync(
            request.TransactionId,
            cancellationToken
        );

        var validationResult = ValidateTransaction<CompleteFido2EnrollmentResponse>(
            transaction,
            Fido2TransactionTypes.Registration
        );
        if (validationResult is not null)
        {
            await _auditService.TrackAuthenticationEventAsync(
                transaction?.UserId,
                null,
                "fido2_enrollment_complete",
                "fido2",
                false,
                validationResult.Error ?? validationResult.Message,
                cancellationToken
            );

            return validationResult;
        }

        if (transaction!.UserId != userId)
        {
            await _auditService.TrackAuthenticationEventAsync(
                transaction.UserId,
                null,
                "fido2_enrollment_complete",
                "fido2",
                false,
                "Transaction user does not match token user",
                cancellationToken
            );

            return Result<CompleteFido2EnrollmentResponse>.Failure(
                "Invalid FIDO2 transaction context.",
                StatusCodes.Status401Unauthorized
            );
        }

        var options = CredentialCreateOptions.FromJson(transaction!.OptionsJson);

        IsCredentialIdUniqueToUserAsyncDelegate callback = async (args, ct) =>
        {
            var exists = await _credentialRepository.CredentialIdExistsAsync(args.CredentialId, ct);

            return !exists;
        };

        var credential = await _fido2.MakeNewCredentialAsync(
            new MakeNewCredentialParams
            {
                AttestationResponse = request.AttestationResponse,
                OriginalOptions = options,
                IsCredentialIdUniqueToUserCallback = callback,
            },
            cancellationToken: cancellationToken
        );

        var userCredential = new UserFido2Credential
        {
            UserId = transaction.UserId,
            CredentialId = credential.Id,
            PublicKey = credential.PublicKey,
            UserHandle = credential.User.Id,
            SignatureCounter = credential.SignCount,
            AaGuid = credential.AaGuid.ToString(),
            CredType = credential.Type.ToString(),
            CreatedAtUtc = DateTime.UtcNow,
        };

        await _credentialRepository.AddAsync(userCredential, cancellationToken);

        await _userRepository.EnableFido2MfaAsync(transaction.UserId, cancellationToken);

        var existingMethod = await _userMfaMethodRepository.GetByUserIdAndMethodAsync(
            transaction.UserId,
            MfaMethodTypes.Fido2,
            cancellationToken
        );

        if (existingMethod is null)
        {
            await _userMfaMethodRepository.AddAsync(
                new UserMfaMethod
                {
                    UserId = transaction.UserId,
                    Method = MfaMethodTypes.Fido2,
                    IsEnabled = true,
                    IsPrimary = false,
                    IsVerified = true,
                    ContactValue = null,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                },
                cancellationToken
            );
        }
        else
        {
            existingMethod.IsEnabled = true;
            existingMethod.IsVerified = true;
            existingMethod.UpdatedAtUtc = DateTime.UtcNow;

            await _userMfaMethodRepository.UpdateAsync(existingMethod, cancellationToken);
        }

        transaction.IsUsed = true;

        await _transactionRepository.UpdateAsync(transaction, cancellationToken);

        await _auditService.TrackAuthenticationEventAsync(
            transaction.UserId,
            null,
            "fido2_enrollment_complete",
            "fido2",
            true,
            null,
            cancellationToken
        );
        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.fido2.enrollment.complete",
            "Information",
            true,
            transaction.UserId,
            null,
            null,
            new { transactionId = transaction.Id },
            cancellationToken
        );

        var recoveryCodes = await EnsureRecoveryCodesIssuedAsync(transaction.UserId, cancellationToken);

        return Result<CompleteFido2EnrollmentResponse>.Success(
            new CompleteFido2EnrollmentResponse { RecoveryCodes = recoveryCodes },
            recoveryCodes.Count > 0
                ? "FIDO2 credential registered successfully. Recovery codes were issued once."
                : "FIDO2 credential registered successfully. Recovery codes already exist and were not reissued."
        );
    }

    public async Task<Result<Fido2OptionsResponse>> CreateLoginOptionsAsync(
        long userId,
        Guid mfaTransactionId,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken
    )
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            await _auditService.TrackAuthenticationEventAsync(
                userId,
                null,
                "fido2_login_options",
                "fido2",
                false,
                "User not found",
                cancellationToken
            );

            return Result<Fido2OptionsResponse>.Failure("User not found.", StatusCodes.Status404NotFound);
        }

        if (!user.IsActive)
        {
            await _auditService.TrackAuthenticationEventAsync(
                user.Id,
                user.Username,
                "fido2_login_options",
                "fido2",
                false,
                "User inactive",
                cancellationToken
            );

            return Result<Fido2OptionsResponse>.Failure("User is inactive.", StatusCodes.Status403Forbidden);
        }

        var enabledFido2Method = await _userMfaMethodRepository.GetEnabledByUserIdAndMethodAsync(
            user.Id,
            MfaMethodTypes.Fido2,
            cancellationToken
        );

        if (enabledFido2Method is null)
        {
            await _auditService.TrackAuthenticationEventAsync(
                user.Id,
                user.Username,
                "fido2_login_options",
                "fido2",
                false,
                "FIDO2 MFA is not enabled",
                cancellationToken
            );

            return Result<Fido2OptionsResponse>.Failure("FIDO2 MFA is not enabled.", StatusCodes.Status400BadRequest);
        }

        var credentials = await _credentialRepository.GetByUserIdAsync(user.Id, cancellationToken);

        if (credentials.Count == 0)
        {
            await _auditService.TrackAuthenticationEventAsync(
                user.Id,
                user.Username,
                "fido2_login_options",
                "fido2",
                false,
                "No FIDO2 credentials found",
                cancellationToken
            );

            return Result<Fido2OptionsResponse>.Failure("No FIDO2 credentials found.", StatusCodes.Status404NotFound);
        }

        var allowedCredentials = credentials
            .Select(x => new PublicKeyCredentialDescriptor(x.CredentialId))
            .ToList();

        var options = _fido2.GetAssertionOptions(
            new GetAssertionOptionsParams
            {
                AllowedCredentials = allowedCredentials,
                UserVerification = UserVerificationRequirement.Required,
                Extensions = new AuthenticationExtensionsClientInputs(),
            }
        );

        var transaction = new Fido2Transaction
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Type = Fido2TransactionTypes.Assertion,
            OptionsJson = options.ToJson(),
            IsUsed = false,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            ParentMfaTransactionId = mfaTransactionId,
        };

        await _transactionRepository.AddAsync(transaction, cancellationToken);

        await _auditService.TrackAuthenticationEventAsync(
            user.Id,
            user.Username,
            "fido2_login_options",
            "fido2",
            true,
            null,
            cancellationToken
        );
        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.fido2.login.options",
            "Information",
            true,
            user.Id,
            user.Username,
            null,
            new { transactionId = transaction.Id, mfaTransactionId },
            cancellationToken
        );

        return Result<Fido2OptionsResponse>.Success(
            new Fido2OptionsResponse { TransactionId = transaction.Id, Options = options },
            "Login options created."
        );
    }

    public async Task<Result<LoginResponse>> CompleteLoginAsync(
        CompleteFido2LoginRequest request,
        long userId,
        Guid mfaTransactionId,
        CancellationToken cancellationToken
    )
    {
        var transaction = await _transactionRepository.GetByIdAsync(
            request.TransactionId,
            cancellationToken
        );

        var validationResult = ValidateTransaction<LoginResponse>(
            transaction,
            Fido2TransactionTypes.Assertion
        );
        if (validationResult is not null)
        {
            await _auditService.TrackAuthenticationEventAsync(
                transaction?.UserId,
                null,
                "fido2_login_complete",
                "fido2",
                false,
                validationResult.Error ?? validationResult.Message,
                cancellationToken
            );

            return validationResult;
        }

        if (transaction!.UserId != userId)
        {
            await _auditService.TrackAuthenticationEventAsync(
                transaction.UserId,
                null,
                "fido2_login_complete",
                "fido2",
                false,
                "Transaction user does not match token user",
                cancellationToken
            );

            return Result<LoginResponse>.Failure("Invalid MFA token context.", StatusCodes.Status401Unauthorized);
        }

        if (transaction.ParentMfaTransactionId != mfaTransactionId)
        {
            await _auditService.TrackAuthenticationEventAsync(
                transaction.UserId,
                null,
                "fido2_login_complete",
                "fido2",
                false,
                "MFA transaction mismatch",
                cancellationToken
            );

            return Result<LoginResponse>.Failure("Invalid MFA transaction.", StatusCodes.Status401Unauthorized);
        }

        var options = AssertionOptions.FromJson(transaction!.OptionsJson);

        var credential = await _credentialRepository.GetByCredentialIdAsync(
            request.AssertionResponse.RawId,
            cancellationToken
        );

        if (credential is null)
        {
            await _auditService.TrackAuthenticationEventAsync(
                transaction.UserId,
                null,
                "fido2_login_complete",
                "fido2",
                false,
                "Unknown FIDO2 credential",
                cancellationToken
            );

            return Result<LoginResponse>.Failure("Unknown FIDO2 credential.", StatusCodes.Status401Unauthorized);
        }

        if (credential.UserId != transaction.UserId)
        {
            await _auditService.TrackAuthenticationEventAsync(
                transaction.UserId,
                null,
                "fido2_login_complete",
                "fido2",
                false,
                "Credential does not belong to transaction user",
                cancellationToken
            );

            return Result<LoginResponse>.Failure(
                "Credential does not belong to transaction user.",
                StatusCodes.Status401Unauthorized
            );
        }

        IsUserHandleOwnerOfCredentialIdAsync callback = async (args, ct) =>
        {
            var credentials = await _credentialRepository.GetByUserHandleAsync(args.UserHandle, ct);

            return credentials.Any(x => x.CredentialId.SequenceEqual(args.CredentialId));
        };

        var assertionResult = await _fido2.MakeAssertionAsync(
            new MakeAssertionParams
            {
                AssertionResponse = request.AssertionResponse,
                OriginalOptions = options,
                StoredPublicKey = credential.PublicKey,
                StoredSignatureCounter = credential.SignatureCounter,
                IsUserHandleOwnerOfCredentialIdCallback = callback,
            },
            cancellationToken: cancellationToken
        );

        credential.SignatureCounter = assertionResult.SignCount;
        credential.LastUsedAtUtc = DateTime.UtcNow;

        await _credentialRepository.UpdateAsync(credential, cancellationToken);

        transaction.IsUsed = true;

        await _transactionRepository.UpdateAsync(transaction, cancellationToken);

        var user = await _userRepository.GetByIdAsync(transaction.UserId, cancellationToken);

        if (user is null)
        {
            await _auditService.TrackAuthenticationEventAsync(
                transaction.UserId,
                null,
                "fido2_login_complete",
                "fido2",
                false,
                "User not found",
                cancellationToken
            );

            return Result<LoginResponse>.Failure("User not found.", StatusCodes.Status404NotFound);
        }

        user.LastLoginAtUtc = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user, cancellationToken);

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

        await _auditService.TrackAuthenticationEventAsync(
            user.Id,
            user.Username,
            "fido2_login_complete",
            "fido2",
            true,
            null,
            cancellationToken
        );
        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.fido2.login.complete",
            "Information",
            true,
            user.Id,
            user.Username,
            null,
            null,
            cancellationToken
        );

        return Result<LoginResponse>.Success(
            new LoginResponse
            {
                MfaRequired = false,
                Status = "Authenticated",
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = 15 * 60,
                AllowedMfaMethods = await _mfaService.GetAllowedMethodsAsync(user.Id, cancellationToken),
            },
            "Login completed successfully."
        );
    }

    private static Result<T>? ValidateTransaction<T>(Fido2Transaction? transaction, string expectedType)
    {
        if (transaction is null)
        {
            return Result<T>.Failure("Invalid FIDO2 transaction.", StatusCodes.Status400BadRequest);
        }

        if (transaction.IsUsed)
        {
            return Result<T>.Failure("FIDO2 transaction already used.", StatusCodes.Status400BadRequest);
        }

        if (transaction.ExpiresAtUtc < DateTime.UtcNow)
        {
            return Result<T>.Failure("FIDO2 transaction expired.", StatusCodes.Status400BadRequest);
        }

        if (transaction.Type != expectedType)
        {
            return Result<T>.Failure("Invalid FIDO2 transaction type.", StatusCodes.Status400BadRequest);
        }

        return null;
    }

    private async Task<List<string>> EnsureRecoveryCodesIssuedAsync(long userId, CancellationToken cancellationToken)
    {
        var (existingBatch, _) = await _userRecoveryCodeRepository.GetStatusAsync(userId, cancellationToken);
        if (existingBatch is not null)
        {
            return [];
        }

        const int recoveryCodeCount = 10;
        const string recoveryCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        var recoveryCodes = Enumerable
            .Range(0, recoveryCodeCount)
            .Select(_ => GenerateRecoveryCode(recoveryCodeAlphabet))
            .ToList();

        var hashes = recoveryCodes
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
            new { count = recoveryCodes.Count },
            cancellationToken
        );

        return recoveryCodes;
    }

    private static string GenerateRecoveryCode(string alphabet)
    {
        const int codeLength = 12;
        var chars = new char[codeLength];

        for (var index = 0; index < codeLength; index++)
        {
            chars[index] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        var value = new string(chars);
        return $"{value[..4]}-{value.Substring(4, 4)}-{value.Substring(8, 4)}";
    }

    private static string NormalizeRecoveryCode(string requestedCode)
    {
        return requestedCode.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
    }
}
