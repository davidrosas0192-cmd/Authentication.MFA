namespace Authentication.Fido2.Entities;

public class UserRecoveryCodeBatch
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? ReplacedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public List<UserRecoveryCode> Codes { get; set; } = [];
}
