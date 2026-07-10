namespace Authentication.Fido2.Services.Interfaces;

public interface ITwilioOtpService
{
    Task<string> StartVerificationAsync(string destination, string channel, CancellationToken cancellationToken);
    Task<bool> CheckVerificationAsync(string destination, string code, CancellationToken cancellationToken);
}
