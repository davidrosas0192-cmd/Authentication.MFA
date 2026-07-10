using Authentication.Fido2.Common;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.Entities;
using Authentication.Fido2.Services.Interfaces;

namespace Authentication.Fido2.Services.Implementations;

public class UserRegistrationService : IUserRegistrationService
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditService _auditService;

    public UserRegistrationService(IUserRepository userRepository, IAuditService auditService)
    {
        _userRepository = userRepository;
        _auditService = auditService;
    }

    public async Task<Result<CreateUserResponse>> CreateUserAsync(
        CreateUserRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.user.create",
                "Warning",
                false,
                null,
                request.Username,
                "Missing required fields",
                new { ipAddress, userAgent },
                cancellationToken
            );

            return Result<CreateUserResponse>.Failure("Username, email and password are required.", StatusCodes.Status400BadRequest);
        }

        var normalizedUsername = request.Username.Trim();
        var normalizedEmail = request.Email.Trim();

        var existingByUsername = await _userRepository.GetByUsernameAsync(normalizedUsername, cancellationToken);
        if (existingByUsername is not null)
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.user.create",
                "Warning",
                false,
                existingByUsername.Id,
                normalizedUsername,
                "Username already exists",
                new { ipAddress, userAgent },
                cancellationToken
            );

            return Result<CreateUserResponse>.Failure("Username already exists.", StatusCodes.Status409Conflict);
        }

        var existingByEmail = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existingByEmail is not null)
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.user.create",
                "Warning",
                false,
                existingByEmail.Id,
                normalizedEmail,
                "Email already exists",
                new { ipAddress, userAgent },
                cancellationToken
            );

            return Result<CreateUserResponse>.Failure("Email already exists.", StatusCodes.Status409Conflict);
        }

        var user = new User
        {
            Username = normalizedUsername,
            Email = normalizedEmail,
            PasswordHash = PasswordHasher.Hash(request.Password),
            IsActive = true,
            IsFido2MfaEnabled = false,
            CreatedAtUtc = DateTime.UtcNow,
        };

        await _userRepository.AddAsync(user, cancellationToken);

        await _auditService.TrackAuthenticationEventAsync(
            user.Id,
            user.Username,
            "user_create",
            "public_signup",
            true,
            null,
            cancellationToken
        );

        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.user.create",
            "Information",
            true,
            user.Id,
            user.Username,
            null,
            new { ipAddress, userAgent },
            cancellationToken
        );

        return Result<CreateUserResponse>.Success(
            new CreateUserResponse
            {
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
            },
            "User created successfully."
        );
    }
}