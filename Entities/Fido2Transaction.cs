namespace Authentication.Fido2.Entities;

public class Fido2Transaction
{
    public Guid Id { get; set; }

    public long UserId { get; set; }

    public string Type { get; set; } = default!; 

    // "registration" or "assertion"

    public string OptionsJson { get; set; } = default!;

    public bool IsUsed { get; set; }

    public string IpAddress { get; set; } = default!;

    public string UserAgent { get; set; } = default!;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public Guid? ParentMfaTransactionId { get; set; }

}