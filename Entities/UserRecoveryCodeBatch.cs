namespace Authentication.Fido2.Entities;

public class UserRecoveryCodeBatch
{
    public Guid Id { get; set; }
    public long UserId { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? ReplacedAtUtc { get; set; }

    public List<UserRecoveryCode> Codes { get; set; } = [];
}
