namespace Authentication.Fido2.DTOs.Fido2;

public class Fido2OptionsResponse
{
    public Guid TransactionId { get; set; }
    public object Options { get; set; } = default!;
}