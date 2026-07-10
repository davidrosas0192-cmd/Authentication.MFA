namespace Authentication.Fido2.DTOs.Fido2;

public class CreateFido2LoginOptionsRequest
{
    public string UsernameOrEmail { get; set; } = default!;

}