namespace Authentication.Fido2.DTOs.Monitor;

public class SessionItem
{
    public Guid Id { get; set; }
    public string Type { get; set; } = default!; // "access" or "refresh"
    public Guid UserId { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokeReason { get; set; }
    public DateTime? LastRotatedAtUtc { get; set; } // refresh only
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
