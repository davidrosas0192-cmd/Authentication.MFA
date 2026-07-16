using Authentication.Fido2.Options;
using Authentication.Fido2.Services.Interfaces;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Verify.V2.Service;

namespace Authentication.Fido2.Services.Implementations;

public class TwilioOtpService : ITwilioOtpService
{
    private readonly TwilioOptions _options;

    public TwilioOtpService(IOptions<TwilioOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> StartVerificationAsync(
        string destination,
        string channel,
        CancellationToken cancellationToken
    )
    {
        TwilioClient.Init(_options.AccountSid, _options.AuthToken);

        var verification = await VerificationResource.CreateAsync(
            pathServiceSid: _options.VerifyServiceSid,
            to: destination,
            channel: channel
        );

        return verification.Sid;
        // return Guid.NewGuid().ToString(); // Return a new GUID as a placeholder for the verification SID

    }

    public async Task<bool> CheckVerificationAsync(
        string destination,
        string code,
        CancellationToken cancellationToken
    )
    {
        TwilioClient.Init(_options.AccountSid, _options.AuthToken);

        var check = await VerificationCheckResource.CreateAsync(
            pathServiceSid: _options.VerifyServiceSid,
            to: destination,
            code: code
        );

        return string.Equals(check.Status, "approved", StringComparison.OrdinalIgnoreCase);

        // return true; // Always return true for testing purposes
    }
}
