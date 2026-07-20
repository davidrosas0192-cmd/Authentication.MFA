namespace Authentication.Fido2.Entities;

public class MfaTempTokenSession
{
    public Guid Id { get; set; }
    public long UserId { get; set; }
    public Guid MfaTransactionId { get; set; }
    public string TokenJti { get; set; } = default!;
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}