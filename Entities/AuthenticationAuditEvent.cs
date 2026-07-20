namespace Authentication.Fido2.Entities;

public class AuthenticationAuditEvent
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public Guid? UserId { get; set; }
    public string? UsernameOrEmail { get; set; }
    public string Stage { get; set; } = default!;
    public string Method { get; set; } = default!;
    public string Outcome { get; set; } = default!;
    public string? FailureReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
}
