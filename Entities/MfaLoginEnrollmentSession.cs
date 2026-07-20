namespace Authentication.Fido2.Entities;

public class MfaLoginEnrollmentSession
{
    public Guid Id { get; set; }
    public long UserId { get; set; }
    public string Status { get; set; } = default!;
    public string ContinuationToken { get; set; } = default!;
    public int StepVersion { get; set; }
    public string TokenJti { get; set; } = default!;
    public Guid? ChallengeId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}