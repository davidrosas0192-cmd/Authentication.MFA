namespace Authentication.Fido2.Entities;

public class UserRecoveryCode
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public long UserId { get; set; }
    public string CodeHash { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? UsedAtUtc { get; set; }

    public UserRecoveryCodeBatch Batch { get; set; } = default!;
}
