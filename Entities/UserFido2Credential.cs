namespace Authentication.Fido2.Entities;

public class UserFido2Credential
{
    public long Id { get; set; }

    public Guid UserId { get; set; }

    public byte[] CredentialId { get; set; } = default!;

    public byte[] PublicKey { get; set; } = default!;

    public byte[] UserHandle { get; set; } = default!;

    public uint SignatureCounter { get; set; }

    public string? AaGuid { get; set; }

    public string? CredType { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public DateTime? LastUsedAtUtc { get; set; }

    public User User { get; set; } = default!;
}
