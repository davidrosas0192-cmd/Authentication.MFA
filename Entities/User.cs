namespace Authentication.Fido2.Entities;

public class User
{
    public long Id { get; set; }
    public string Role { get; set; } = "user";
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public bool IsFido2MfaEnabled { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; set; }
}