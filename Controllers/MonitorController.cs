using Authentication.Fido2.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authentication.Fido2.Controllers;

[ApiController]
[AllowAnonymous]
public class MonitorController : ApiControllerBase
{
    private readonly IMonitorService _monitorService;

    public MonitorController(IMonitorService monitorService)
    {
        _monitorService = monitorService ?? throw new ArgumentNullException(nameof(monitorService));
    }

    /// <summary>Dashboard summary statistics for today.</summary>
    [HttpGet("/api/monitor/summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var result = await _monitorService.GetSummaryAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Paginated login history.</summary>
    [HttpGet("/api/monitor/logins")]
    public async Task<IActionResult> GetLogins(
        [FromQuery] long? userId,
        [FromQuery] string? outcome,
        [FromQuery] string? method,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _monitorService.GetLoginsAsync(
            userId, outcome, method, dateFrom, dateTo, page, pageSize, cancellationToken
        );
        return Ok(result);
    }

    /// <summary>Paginated MFA enrollment sessions filtered by status.</summary>
    [HttpGet("/api/monitor/enrollments")]
    public async Task<IActionResult> GetEnrollments(
        [FromQuery] string? status,
        [FromQuery] long? userId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _monitorService.GetEnrollmentsAsync(
            status, userId, dateFrom, dateTo, page, pageSize, cancellationToken
        );
        return Ok(result);
    }

    /// <summary>Paginated MFA challenges filtered by status, purpose and method.</summary>
    [HttpGet("/api/monitor/challenges")]
    public async Task<IActionResult> GetChallenges(
        [FromQuery] string? status,
        [FromQuery] string? purpose,
        [FromQuery] string? method,
        [FromQuery] long? userId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _monitorService.GetChallengesAsync(
            status, purpose, method, userId, dateFrom, dateTo, page, pageSize, cancellationToken
        );
        return Ok(result);
    }

    /// <summary>Paginated access and refresh token sessions.</summary>
    [HttpGet("/api/monitor/sessions")]
    public async Task<IActionResult> GetSessions(
        [FromQuery] long? userId,
        [FromQuery] string? type,
        [FromQuery] bool onlyActive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _monitorService.GetSessionsAsync(
            userId, type, onlyActive, page, pageSize, cancellationToken
        );
        return Ok(result);
    }

    /// <summary>Paginated security audit events filtered by severity, category and type.</summary>
    [HttpGet("/api/monitor/security-events")]
    public async Task<IActionResult> GetSecurityEvents(
        [FromQuery] string? severity,
        [FromQuery] string? category,
        [FromQuery] string? eventType,
        [FromQuery] string? outcome,
        [FromQuery] long? userId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _monitorService.GetSecurityEventsAsync(
            severity, category, eventType, outcome, userId, dateFrom, dateTo, page, pageSize, cancellationToken
        );
        return Ok(result);
    }

    /// <summary>Paginated users with their MFA methods summary.</summary>
    [HttpGet("/api/monitor/users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] bool? isActive,
        [FromQuery] bool? hasMfa,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _monitorService.GetUsersAsync(
            isActive, hasMfa, page, pageSize, cancellationToken
        );
        return Ok(result);
    }
}
