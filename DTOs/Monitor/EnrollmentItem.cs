namespace Authentication.Fido2.DTOs.Monitor;

public class EnrollmentItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Status { get; set; } = default!;
    public int StepVersion { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ModifiedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
