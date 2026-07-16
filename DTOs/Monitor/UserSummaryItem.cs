namespace Authentication.Fido2.DTOs.Monitor;

public class UserSummaryItem
{
    public long Id { get; set; }
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public bool IsActive { get; set; }
    public bool IsFido2Enabled { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public int MfaMethodCount { get; set; }
    public List<string> MfaMethods { get; set; } = [];
}
