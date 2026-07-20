namespace Authentication.Fido2.Entities;

public class MfaChallenge
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Purpose { get; set; } = default!;
    public string ContinuationToken { get; set; } = default!;
    public int StepVersion { get; set; }
    public string? Method { get; set; }
    public string? Provider { get; set; }
    public string? ProviderRequestId { get; set; }
    public string? Channel { get; set; }
    public string? ContactValue { get; set; }
    public string Status { get; set; } = default!;
    public int FailedAttempts { get; set; } = 0;
    public DateTime? LastFailedAttemptAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
