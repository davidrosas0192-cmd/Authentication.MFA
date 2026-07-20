namespace Authentication.Fido2.DTOs.Monitor;

public class ChallengeItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Purpose { get; set; } = default!;
    public string? Method { get; set; }
    public string? Channel { get; set; }
    public string Status { get; set; } = default!;
    public int FailedAttempts { get; set; }
    public DateTime? LastFailedAttemptAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public string? IpAddress { get; set; }
}
