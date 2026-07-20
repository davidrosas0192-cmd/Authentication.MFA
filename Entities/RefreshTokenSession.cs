namespace Authentication.Fido2.Entities;

public class RefreshTokenSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = default!;
    public Guid AccessTokenSessionId { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokeReason { get; set; }
    public DateTime? LastRotatedAtUtc { get; set; }
    public Guid? PreviousTokenSessionId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
