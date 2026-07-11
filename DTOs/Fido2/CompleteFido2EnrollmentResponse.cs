namespace Authentication.Fido2.DTOs.Fido2;

public class CompleteFido2EnrollmentResponse
{
    public List<string> RecoveryCodes { get; set; } = [];
}