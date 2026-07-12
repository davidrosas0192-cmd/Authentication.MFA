using Authentication.Fido2.Common;
using Authentication.Fido2.Constants;
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
        return await CreateUserInternalAsync(
            request,
            ipAddress,
            userAgent,
            allowPrivilegedRoleAssignment: false,
            actorUserId: null,
            cancellationToken
        );
    }

    public async Task<Result<CreateUserResponse>> CreateUserByAdminAsync(
        long actorUserId,
        CreateUserRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        return await CreateUserInternalAsync(
            request,
            ipAddress,
            userAgent,
            allowPrivilegedRoleAssignment: true,
            actorUserId,
            cancellationToken
        );
    }

    public async Task<Result<AdminUsersListResponse>> GetUsersForAdminAsync(
        CancellationToken cancellationToken
    )
    {
        var users = await _userRepository.ListAllAsync(cancellationToken);

        return Result<AdminUsersListResponse>.Success(
            new AdminUsersListResponse
            {
                Users = users
                    .Select(ToAdminSummary)
                    .ToList(),
            }
        );
    }

    public async Task<Result<AdminUserSummaryResponse>> UpdateUserRoleAsync(
        long actorUserId,
        long targetUserId,
        UpdateUserRoleRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.Role))
        {
            return Result<AdminUserSummaryResponse>.Failure(
                "Role is required.",
                StatusCodes.Status400BadRequest
            );
        }

        var normalizedRole = UserRoles.Normalize(request.Role);
        if (!UserRoles.IsValid(normalizedRole))
        {
            return Result<AdminUserSummaryResponse>.Failure(
                "Role is invalid. Allowed values: admin, user, support.",
                StatusCodes.Status400BadRequest
            );
        }

        var targetUser = await _userRepository.GetByIdAsync(targetUserId, cancellationToken);
        if (targetUser is null)
        {
            return Result<AdminUserSummaryResponse>.Failure(
                "User was not found.",
                StatusCodes.Status404NotFound
            );
        }

        var currentRole = UserRoles.Normalize(targetUser.Role);
        if (currentRole == UserRoles.Admin && normalizedRole != UserRoles.Admin && targetUser.IsActive)
        {
            var activeAdminCount = await _userRepository.CountActiveByRoleAsync(
                UserRoles.Admin,
                cancellationToken
            );

            if (activeAdminCount <= 1)
            {
                return Result<AdminUserSummaryResponse>.Failure(
                    "Cannot remove role from the last active admin.",
                    StatusCodes.Status409Conflict
                );
            }
        }

        targetUser.Role = normalizedRole;
        await _userRepository.UpdateAsync(targetUser, cancellationToken);

        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.user.role.update",
            "Information",
            true,
            actorUserId,
            null,
            null,
            new
            {
                targetUserId,
                role = normalizedRole,
                ipAddress,
                userAgent,
            },
            cancellationToken
        );

        return Result<AdminUserSummaryResponse>.Success(ToAdminSummary(targetUser), "User role updated.");
    }

    public async Task<Result<AdminUserSummaryResponse>> UpdateUserStatusAsync(
        long actorUserId,
        long targetUserId,
        UpdateUserStatusRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        var targetUser = await _userRepository.GetByIdAsync(targetUserId, cancellationToken);
        if (targetUser is null)
        {
            return Result<AdminUserSummaryResponse>.Failure(
                "User was not found.",
                StatusCodes.Status404NotFound
            );
        }

        if (targetUser.IsActive && !request.IsActive && UserRoles.Normalize(targetUser.Role) == UserRoles.Admin)
        {
            var activeAdminCount = await _userRepository.CountActiveByRoleAsync(
                UserRoles.Admin,
                cancellationToken
            );

            if (activeAdminCount <= 1)
            {
                return Result<AdminUserSummaryResponse>.Failure(
                    "Cannot deactivate the last active admin.",
                    StatusCodes.Status409Conflict
                );
            }
        }

        targetUser.IsActive = request.IsActive;
        await _userRepository.UpdateAsync(targetUser, cancellationToken);

        await _auditService.TrackSecurityEventAsync(
            "Authentication",
            "auth.user.status.update",
            "Information",
            true,
            actorUserId,
            null,
            null,
            new
            {
                targetUserId,
                isActive = request.IsActive,
                ipAddress,
                userAgent,
            },
            cancellationToken
        );

        return Result<AdminUserSummaryResponse>.Success(ToAdminSummary(targetUser), "User status updated.");
    }

    private async Task<Result<CreateUserResponse>> CreateUserInternalAsync(
        CreateUserRequest request,
        string? ipAddress,
        string? userAgent,
        bool allowPrivilegedRoleAssignment,
        long? actorUserId,
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

        var requestedRole = string.IsNullOrWhiteSpace(request.Role)
            ? UserRoles.User
            : UserRoles.Normalize(request.Role);

        if (!UserRoles.IsValid(requestedRole))
        {
            return Result<CreateUserResponse>.Failure("Role is invalid. Allowed values: admin, user, support.", StatusCodes.Status400BadRequest);
        }

        if (!allowPrivilegedRoleAssignment && requestedRole != UserRoles.User)
        {
            await _auditService.TrackSecurityEventAsync(
                "Authentication",
                "auth.user.create.role_assignment_denied",
                "Warning",
                false,
                actorUserId,
                request.Username,
                "Privileged role assignment requires admin access.",
                new { requestedRole, ipAddress, userAgent },
                cancellationToken
            );

            return Result<CreateUserResponse>.Failure(
                "Only admins can assign admin or support roles.",
                StatusCodes.Status403Forbidden
            );
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
            Role = requestedRole,
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
            new { ipAddress, userAgent, role = requestedRole, actorUserId },
            cancellationToken
        );

        return Result<CreateUserResponse>.Success(
            new CreateUserResponse
            {
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = UserRoles.ToDisplayName(requestedRole),
            },
            "User created successfully."
        );
    }

    private static AdminUserSummaryResponse ToAdminSummary(User user)
    {
        return new AdminUserSummaryResponse
        {
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = UserRoles.ToDisplayName(user.Role),
            IsActive = user.IsActive,
            CreatedAtUtc = user.CreatedAtUtc,
            LastLoginAtUtc = user.LastLoginAtUtc,
        };
    }
}