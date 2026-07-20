using System.Text.Json;
using Authentication.Fido2.Data;
using Authentication.Fido2.Entities;
using Authentication.Fido2.Services.Interfaces;

namespace Authentication.Fido2.Services.Implementations;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        ApplicationDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger
    )
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _httpContextAccessor =
            httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task TrackAuthenticationEventAsync(
        Guid? userId,
        string? usernameOrEmail,
        string stage,
        string method,
        bool isSuccess,
        string? failureReason,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var (ipAddress, userAgent, correlationId, _, _) = ReadRequestMetadata();

            _dbContext.AuthenticationAuditEvents.Add(
                new AuthenticationAuditEvent
                {
                    OccurredAtUtc = DateTime.UtcNow,
                    UserId = userId,
                    UsernameOrEmail = usernameOrEmail,
                    Stage = stage,
                    Method = method,
                    Outcome = isSuccess ? "Success" : "Failure",
                    FailureReason = failureReason,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    CorrelationId = correlationId,
                }
            );

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit logging failed for authentication event.");
        }
    }

    public async Task TrackSecurityEventAsync(
        string category,
        string eventType,
        string severity,
        bool isSuccess,
        Guid? userId,
        string? usernameOrEmail,
        string? failureReason,
        object? details,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var (ipAddress, userAgent, correlationId, requestPath, httpMethod) =
                ReadRequestMetadata();

            _dbContext.SecurityAuditEvents.Add(
                new SecurityAuditEvent
                {
                    OccurredAtUtc = DateTime.UtcNow,
                    Category = category,
                    EventType = eventType,
                    Severity = severity,
                    Outcome = isSuccess ? "Success" : "Failure",
                    UserId = userId,
                    UsernameOrEmail = usernameOrEmail,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    CorrelationId = correlationId,
                    RequestPath = requestPath,
                    HttpMethod = httpMethod,
                    FailureReason = failureReason,
                    DetailsJson = details is null ? null : JsonSerializer.Serialize(details),
                }
            );

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit logging failed for security event.");
        }
    }

    private (string? IpAddress, string? UserAgent, string? CorrelationId, string? Path, string? Method) ReadRequestMetadata()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        return (
            httpContext?.Connection.RemoteIpAddress?.ToString(),
            httpContext?.Request.Headers.UserAgent.ToString(),
            httpContext?.TraceIdentifier,
            httpContext?.Request.Path.ToString(),
            httpContext?.Request.Method
        );
    }
}
