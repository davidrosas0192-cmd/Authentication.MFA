namespace Authentication.Fido2.Entities;

public class MfaChallenge
{
    public Guid Id { get; set; }
    public long UserId { get; set; }
    public string? Method { get; set; }
    public string? Provider { get; set; }
    public string? ProviderRequestId { get; set; }
    public string? Channel { get; set; }
    public string Status { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
