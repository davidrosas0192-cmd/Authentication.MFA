using Authentication.Fido2.Data;
using Authentication.Fido2.DTOs.Monitor;
using Authentication.Fido2.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authentication.Fido2.Services.Implementations;

public class MonitorService : IMonitorService
{
    private const int MaxPageSize = 100;
    private const int MaxDateRangeDays = 90;

    private readonly ApplicationDbContext _context;

    public MonitorService(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<MonitorSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var startOfDay = now.Date;

        var loginsToday = await _context.AuthenticationAuditEvents
            .CountAsync(x => x.OccurredAtUtc >= startOfDay, cancellationToken);

        var loginFailuresToday = await _context.AuthenticationAuditEvents
            .CountAsync(x => x.OccurredAtUtc >= startOfDay && x.Outcome == "failure", cancellationToken);

        var activeAccessSessions = await _context.AccessTokenSessions
            .CountAsync(x => x.RevokedAtUtc == null && x.ExpiresAtUtc > now, cancellationToken);

        var activeRefreshSessions = await _context.RefreshTokenSessions
            .CountAsync(x => x.RevokedAtUtc == null && x.ExpiresAtUtc > now, cancellationToken);

        var pendingChallenges = await _context.MfaChallenges
            .CountAsync(x => x.Status == "pending" && x.ExpiresAtUtc > now, cancellationToken);

        var lockedChallenges = await _context.MfaChallenges
            .CountAsync(x => x.Status == "locked", cancellationToken);

        var enrollmentsInProgress = await _context.MfaLoginEnrollmentSessions
            .CountAsync(x => x.Status != "completed" && x.Status != "expired" && x.Status != "cancelled" && x.ExpiresAtUtc > now, cancellationToken);

        var enrollmentsCompleted = await _context.MfaLoginEnrollmentSessions
            .CountAsync(x => x.Status == "completed", cancellationToken);

        var securityWarningsToday = await _context.SecurityAuditEvents
            .CountAsync(x => x.OccurredAtUtc >= startOfDay && x.Severity == "Warning", cancellationToken);

        var securityErrorsToday = await _context.SecurityAuditEvents
            .CountAsync(x => x.OccurredAtUtc >= startOfDay && (x.Severity == "Error" || x.Severity == "Critical"), cancellationToken);

        var usersTotal = await _context.Users.CountAsync(cancellationToken);
        var usersActive = await _context.Users.CountAsync(x => x.IsActive, cancellationToken);

        var usersWithMfa = await _context.UserMfaMethods
            .Where(x => x.IsEnabled)
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new MonitorSummaryResponse
        {
            LoginsToday = loginsToday,
            LoginFailuresToday = loginFailuresToday,
            ActiveAccessSessions = activeAccessSessions,
            ActiveRefreshSessions = activeRefreshSessions,
            PendingChallenges = pendingChallenges,
            LockedChallenges = lockedChallenges,
            EnrollmentsInProgress = enrollmentsInProgress,
            EnrollmentsCompleted = enrollmentsCompleted,
            SecurityWarningsToday = securityWarningsToday,
            SecurityErrorsToday = securityErrorsToday,
            UsersTotal = usersTotal,
            UsersActive = usersActive,
            UsersWithMfa = usersWithMfa,
            GeneratedAtUtc = now,
        };
    }

    public async Task<PagedResponse<LoginHistoryItem>> GetLoginsAsync(
        long? userId,
        string? outcome,
        string? method,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        pageSize = Math.Min(pageSize, MaxPageSize);
        page = Math.Max(page, 1);
        dateFrom = ClampDateFrom(dateFrom);

        var query = _context.AuthenticationAuditEvents.AsNoTracking();

        if (userId.HasValue)
            query = query.Where(x => x.UserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(outcome))
            query = query.Where(x => x.Outcome == outcome.Trim().ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(method))
            query = query.Where(x => x.Method == method.Trim().ToLowerInvariant());

        if (dateFrom.HasValue)
            query = query.Where(x => x.OccurredAtUtc >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(x => x.OccurredAtUtc <= dateTo.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LoginHistoryItem
            {
                Id = x.Id,
                OccurredAtUtc = x.OccurredAtUtc,
                UserId = x.UserId,
                UsernameOrEmail = x.UsernameOrEmail,
                Stage = x.Stage,
                Method = x.Method,
                Outcome = x.Outcome,
                FailureReason = x.FailureReason,
                IpAddress = x.IpAddress,
                UserAgent = x.UserAgent,
            })
            .ToListAsync(cancellationToken);

        return new PagedResponse<LoginHistoryItem>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items,
        };
    }

    public async Task<PagedResponse<EnrollmentItem>> GetEnrollmentsAsync(
        string? status,
        long? userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        pageSize = Math.Min(pageSize, MaxPageSize);
        page = Math.Max(page, 1);
        dateFrom = ClampDateFrom(dateFrom);

        var query = _context.MfaLoginEnrollmentSessions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status.Trim().ToLowerInvariant());

        if (userId.HasValue)
            query = query.Where(x => x.UserId == userId.Value);

        if (dateFrom.HasValue)
            query = query.Where(x => x.CreatedAtUtc >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(x => x.CreatedAtUtc <= dateTo.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new EnrollmentItem
            {
                Id = x.Id,
                UserId = x.UserId,
                Status = x.Status,
                StepVersion = x.StepVersion,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc,
                ExpiresAtUtc = x.ExpiresAtUtc,
                CompletedAtUtc = x.CompletedAtUtc,
            })
            .ToListAsync(cancellationToken);

        return new PagedResponse<EnrollmentItem>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items,
        };
    }

    public async Task<PagedResponse<ChallengeItem>> GetChallengesAsync(
        string? status,
        string? purpose,
        string? method,
        long? userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        pageSize = Math.Min(pageSize, MaxPageSize);
        page = Math.Max(page, 1);
        dateFrom = ClampDateFrom(dateFrom);

        var query = _context.MfaChallenges.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status.Trim().ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(purpose))
            query = query.Where(x => x.Purpose == purpose.Trim().ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(method))
            query = query.Where(x => x.Method == method.Trim().ToLowerInvariant());

        if (userId.HasValue)
            query = query.Where(x => x.UserId == userId.Value);

        if (dateFrom.HasValue)
            query = query.Where(x => x.CreatedAtUtc >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(x => x.CreatedAtUtc <= dateTo.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ChallengeItem
            {
                Id = x.Id,
                UserId = x.UserId,
                Purpose = x.Purpose,
                Method = x.Method,
                Channel = x.Channel,
                Status = x.Status,
                FailedAttempts = x.FailedAttempts,
                LastFailedAttemptAtUtc = x.LastFailedAttemptAtUtc,
                CreatedAtUtc = x.CreatedAtUtc,
                ExpiresAtUtc = x.ExpiresAtUtc,
                VerifiedAtUtc = x.VerifiedAtUtc,
                IpAddress = x.IpAddress,
            })
            .ToListAsync(cancellationToken);

        return new PagedResponse<ChallengeItem>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items,
        };
    }

    public async Task<PagedResponse<SessionItem>> GetSessionsAsync(
        long? userId,
        string? type,
        bool onlyActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        pageSize = Math.Min(pageSize, MaxPageSize);
        page = Math.Max(page, 1);

        var now = DateTime.UtcNow;
        var normalizedType = type?.Trim().ToLowerInvariant();

        var accessItems = new List<SessionItem>();
        var refreshItems = new List<SessionItem>();

        if (normalizedType is null or "access")
        {
            var accessQuery = _context.AccessTokenSessions.AsNoTracking();
            if (userId.HasValue) accessQuery = accessQuery.Where(x => x.UserId == userId.Value);
            if (onlyActive) accessQuery = accessQuery.Where(x => x.RevokedAtUtc == null && x.ExpiresAtUtc > now);

            accessItems = await accessQuery
                .Select(x => new SessionItem
                {
                    Id = x.Id,
                    Type = "access",
                    UserId = x.UserId,
                    IssuedAtUtc = x.IssuedAtUtc,
                    ExpiresAtUtc = x.ExpiresAtUtc,
                    IsRevoked = x.RevokedAtUtc != null,
                    RevokedAtUtc = x.RevokedAtUtc,
                    RevokeReason = x.RevokeReason,
                    IpAddress = x.IpAddress,
                    UserAgent = x.UserAgent,
                })
                .ToListAsync(cancellationToken);
        }

        if (normalizedType is null or "refresh")
        {
            var refreshQuery = _context.RefreshTokenSessions.AsNoTracking();
            if (userId.HasValue) refreshQuery = refreshQuery.Where(x => x.UserId == userId.Value);
            if (onlyActive) refreshQuery = refreshQuery.Where(x => x.RevokedAtUtc == null && x.ExpiresAtUtc > now);

            refreshItems = await refreshQuery
                .Select(x => new SessionItem
                {
                    Id = x.Id,
                    Type = "refresh",
                    UserId = x.UserId,
                    IssuedAtUtc = x.IssuedAtUtc,
                    ExpiresAtUtc = x.ExpiresAtUtc,
                    IsRevoked = x.RevokedAtUtc != null,
                    RevokedAtUtc = x.RevokedAtUtc,
                    RevokeReason = x.RevokeReason,
                    LastRotatedAtUtc = x.LastRotatedAtUtc,
                    IpAddress = x.IpAddress,
                    UserAgent = x.UserAgent,
                })
                .ToListAsync(cancellationToken);
        }

        var allItems = accessItems
            .Concat(refreshItems)
            .OrderByDescending(x => x.IssuedAtUtc)
            .ToList();

        var totalCount = allItems.Count;
        var pagedItems = allItems.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResponse<SessionItem>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = pagedItems,
        };
    }

    public async Task<PagedResponse<SecurityEventItem>> GetSecurityEventsAsync(
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
    )
    {
        pageSize = Math.Min(pageSize, MaxPageSize);
        page = Math.Max(page, 1);
        dateFrom = ClampDateFrom(dateFrom);

        var query = _context.SecurityAuditEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(x => x.Severity == severity.Trim());

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category == category.Trim());

        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(x => x.EventType == eventType.Trim());

        if (!string.IsNullOrWhiteSpace(outcome))
            query = query.Where(x => x.Outcome == outcome.Trim().ToLowerInvariant());

        if (userId.HasValue)
            query = query.Where(x => x.UserId == userId.Value);

        if (dateFrom.HasValue)
            query = query.Where(x => x.OccurredAtUtc >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(x => x.OccurredAtUtc <= dateTo.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SecurityEventItem
            {
                Id = x.Id,
                OccurredAtUtc = x.OccurredAtUtc,
                Category = x.Category,
                EventType = x.EventType,
                Severity = x.Severity,
                Outcome = x.Outcome,
                UserId = x.UserId,
                UsernameOrEmail = x.UsernameOrEmail,
                IpAddress = x.IpAddress,
                FailureReason = x.FailureReason,
                RequestPath = x.RequestPath,
                HttpMethod = x.HttpMethod,
            })
            .ToListAsync(cancellationToken);

        return new PagedResponse<SecurityEventItem>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items,
        };
    }

    public async Task<PagedResponse<UserSummaryItem>> GetUsersAsync(
        bool? isActive,
        bool? hasMfa,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        pageSize = Math.Min(pageSize, MaxPageSize);
        page = Math.Max(page, 1);

        var userQuery = _context.Users.AsNoTracking();

        if (isActive.HasValue)
            userQuery = userQuery.Where(x => x.IsActive == isActive.Value);

        var userIds = await userQuery.Select(x => x.Id).ToListAsync(cancellationToken);

        var mfaMethods = await _context.UserMfaMethods
            .AsNoTracking()
            .Where(x => x.IsEnabled && userIds.Contains(x.UserId))
            .GroupBy(x => x.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Methods = g.Select(m => m.Method).ToList(),
            })
            .ToListAsync(cancellationToken);

        var mfaLookup = mfaMethods.ToDictionary(x => x.UserId, x => x.Methods);

        if (hasMfa.HasValue)
        {
            var usersWithMfaIds = mfaLookup
                .Where(kv => kv.Value.Count > 0)
                .Select(kv => kv.Key)
                .ToHashSet();

            userQuery = hasMfa.Value
                ? userQuery.Where(x => usersWithMfaIds.Contains(x.Id))
                : userQuery.Where(x => !usersWithMfaIds.Contains(x.Id));
        }

        var totalCount = await userQuery.CountAsync(cancellationToken);

        var users = await userQuery
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = users.Select(u =>
        {
            var methods = mfaLookup.TryGetValue(u.Id, out var m) ? m : [];
            return new UserSummaryItem
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                IsActive = u.IsActive,
                IsFido2Enabled = u.IsFido2MfaEnabled,
                CreatedAtUtc = u.CreatedAtUtc,
                LastLoginAtUtc = u.LastLoginAtUtc,
                MfaMethodCount = methods.Count,
                MfaMethods = methods,
            };
        }).ToList();

        return new PagedResponse<UserSummaryItem>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items,
        };
    }

    private static DateTime ClampDateFrom(DateTime? dateFrom)
    {
        var minDate = DateTime.UtcNow.AddDays(-MaxDateRangeDays);
        if (!dateFrom.HasValue || dateFrom.Value < minDate)
            return minDate;
        return dateFrom.Value;
    }
}
