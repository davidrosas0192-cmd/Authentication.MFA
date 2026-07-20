namespace Authentication.Fido2.Services.Interfaces;

public interface IAuditService
{
    Task TrackAuthenticationEventAsync(
        Guid? userId,
        string? usernameOrEmail,
        string stage,
        string method,
        bool isSuccess,
        string? failureReason,
        CancellationToken cancellationToken
    );

    Task TrackSecurityEventAsync(
        string category,
        string eventType,
        string severity,
        bool isSuccess,
        Guid? userId,
        string? usernameOrEmail,
        string? failureReason,
        object? details,
        CancellationToken cancellationToken
    );
}
