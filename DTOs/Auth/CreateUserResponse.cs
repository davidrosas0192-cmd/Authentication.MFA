namespace Authentication.Fido2.DTOs.Auth;

public class CreateUserResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
}