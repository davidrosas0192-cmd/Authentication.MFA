namespace Authentication.Fido2.DTOs.Mfa;

public class RecoveryCodeStatusResponse
{
    public bool HasRecoveryCodes { get; set; }
    public int RemainingCodes { get; set; }
    public DateTime? IssuedAtUtc { get; set; }
}
