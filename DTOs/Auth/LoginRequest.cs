using System.ComponentModel.DataAnnotations;

namespace Authentication.Fido2.DTOs.Auth;

public class LoginRequest
{
    [Required]
    public string Username { get; set; } = default!;

    [Required]  
    public string Password { get; set; } = default!;
}
