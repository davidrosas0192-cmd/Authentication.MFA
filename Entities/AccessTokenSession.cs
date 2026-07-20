namespace Authentication.Fido2.Entities;

public class AccessTokenSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenJti { get; set; } = default!;
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokeReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
