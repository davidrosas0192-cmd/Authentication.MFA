namespace Authentication.Fido2.Entities;

public class MfaSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SessionType { get; set; } = default!;
    public string TokenJti { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public string? Status { get; set; }
    public string? ContinuationToken { get; set; }
    public int? StepVersion { get; set; }
    public Guid? ChallengeId { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public Guid? MfaTransactionId { get; set; }
    public DateTime? IssuedAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}