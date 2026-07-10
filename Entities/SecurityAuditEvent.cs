namespace Authentication.Fido2.Entities;

public class SecurityAuditEvent
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Category { get; set; } = default!;
    public string EventType { get; set; } = default!;
    public string Severity { get; set; } = default!;
    public string Outcome { get; set; } = default!;
    public long? UserId { get; set; }
    public string? UsernameOrEmail { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public string? RequestPath { get; set; }
    public string? HttpMethod { get; set; }
    public string? FailureReason { get; set; }
    public string? DetailsJson { get; set; }
}
