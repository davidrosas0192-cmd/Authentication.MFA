namespace Authentication.Fido2.DTOs.Mfa;

public class MfaMethodsResponse
{
    public List<string> AllowedMfaMethods { get; set; } = [];
}
