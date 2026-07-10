namespace Authentication.Fido2.Entities;

public class UserMfaMethod
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Method { get; set; } = default!;
    public bool IsEnabled { get; set; } = true;
    public bool IsPrimary { get; set; }
    public bool IsVerified { get; set; }
    public string? ContactValue { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
