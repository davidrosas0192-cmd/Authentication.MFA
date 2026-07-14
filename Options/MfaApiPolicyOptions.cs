namespace Authentication.Fido2.Options;

public class MfaApiPolicyOptions
{
    public int RetryAfterSecondsOnTooManyRequests { get; set; } = 45;
}
