using Authentication.Fido2.Common;
using Authentication.Fido2.DTOs.Auth;

namespace Authentication.Fido2.Services.Interfaces;

public interface IUserRegistrationService
{
    Task<Result<CreateUserResponse>> CreateUserAsync(
        CreateUserRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );

    Task<Result<CreateUserResponse>> CreateUserByAdminAsync(
        long actorUserId,
        CreateUserRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );

    Task<Result<AdminUsersListResponse>> GetUsersForAdminAsync(
        CancellationToken cancellationToken
    );

    Task<Result<AdminUserSummaryResponse>> UpdateUserRoleAsync(
        long actorUserId,
        long targetUserId,
        UpdateUserRoleRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );

    Task<Result<AdminUserSummaryResponse>> UpdateUserStatusAsync(
        long actorUserId,
        long targetUserId,
        UpdateUserStatusRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken
    );
}