using Fido2NetLib;

namespace Authentication.Fido2.DTOs.Fido2;

public class CompleteFido2LoginRequest
{
    public Guid TransactionId { get; set; }

    public AuthenticatorAssertionRawResponse AssertionResponse { get; set; } = default!;

}