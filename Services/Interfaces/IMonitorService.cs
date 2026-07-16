using Authentication.Fido2.DTOs.Monitor;

namespace Authentication.Fido2.Services.Interfaces;

public interface IMonitorService
{
    Task<MonitorSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken);

    Task<PagedResponse<LoginHistoryItem>> GetLoginsAsync(
        long? userId,
        string? outcome,
        string? method,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    );

    Task<PagedResponse<EnrollmentItem>> GetEnrollmentsAsync(
        string? status,
        long? userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    );

    Task<PagedResponse<ChallengeItem>> GetChallengesAsync(
        string? status,
        string? purpose,
        string? method,
        long? userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    );

    Task<PagedResponse<SessionItem>> GetSessionsAsync(
        long? userId,
        string? type,
        bool onlyActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    );

    Task<PagedResponse<SecurityEventItem>> GetSecurityEventsAsync(
        string? severity,
        string? category,
        string? eventType,
        string? outcome,
        long? userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    );

    Task<PagedResponse<UserSummaryItem>> GetUsersAsync(
        bool? isActive,
        bool? hasMfa,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    );
}
