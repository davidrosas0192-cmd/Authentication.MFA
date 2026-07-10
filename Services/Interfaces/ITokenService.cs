using Authentication.Fido2.Entities;

namespace Authentication.Fido2.Services.Interfaces;

public interface ITokenService
{
    string CreateAccessToken(User user, string tokenJti);
    string CreateMfaToken(User user, Guid mfaTransactionId, string tokenJti);
    string CreateRefreshToken();
}