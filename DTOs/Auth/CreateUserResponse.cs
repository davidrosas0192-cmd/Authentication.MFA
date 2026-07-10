namespace Authentication.Fido2.DTOs.Auth;

public class CreateUserResponse
{
    public long UserId { get; set; }
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
}