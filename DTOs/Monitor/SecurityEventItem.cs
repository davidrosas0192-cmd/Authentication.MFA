namespace Authentication.Fido2.DTOs.Monitor;

public class SecurityEventItem
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Category { get; set; } = default!;
    public string EventType { get; set; } = default!;
    public string Severity { get; set; } = default!;
    public string Outcome { get; set; } = default!;
    public Guid? UserId { get; set; }
    public string? UsernameOrEmail { get; set; }
    public string? IpAddress { get; set; }
    public string? FailureReason { get; set; }
    public string? RequestPath { get; set; }
    public string? HttpMethod { get; set; }
}
