using Authentication.Fido2.Common;
using Authentication.Fido2.DTOs.Auth;
using Authentication.Fido2.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Authentication.Fido2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ApiControllerBase
{
    private readonly IUserRegistrationService _userRegistrationService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserRegistrationService userRegistrationService,
        ILogger<UsersController> logger
    )
    {
        _userRegistrationService = userRegistrationService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken
    )
    {
        var response = await _userRegistrationService.CreateUserAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken
        );

        return ToActionResult(response);
    }
}