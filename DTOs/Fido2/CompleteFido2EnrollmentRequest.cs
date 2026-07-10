using Fido2NetLib;

namespace Authentication.Fido2.DTOs.Fido2;

public class CompleteFido2EnrollmentRequest
{
    public Guid TransactionId { get; set; }

    public AuthenticatorAttestationRawResponse AttestationResponse { get; set; } = default!;

}