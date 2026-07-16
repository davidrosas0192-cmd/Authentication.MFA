namespace Authentication.Fido2.Options;

public class TwilioOptions
{
    public string ApiKeySid { get; set; } = default!;
    public string ApiKeySecret { get; set; } = default!;
    public string VerifyServiceSid { get; set; } = default!;
}
