namespace Authentication.Fido2.DTOs.Auth;

public class CreateUserRequest
{
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
}