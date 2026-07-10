using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Authentication.Fido2.Tests.TestSupport;

internal static class ControllerTestHelpers
{
    public static DefaultHttpContext CreateHttpContext(ClaimsPrincipal? user = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["User-Agent"] = "Test-Agent/1.0";
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        context.User = user ?? new ClaimsPrincipal(new ClaimsIdentity());
        return context;
    }

    public static ClaimsPrincipal CreateUserPrincipal(
        long userId,
        Guid? mfaTransactionId = null,
        string? tokenType = null,
        string? tokenJti = null
    )
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, tokenJti ?? Guid.NewGuid().ToString()),
        };

        if (!string.IsNullOrWhiteSpace(tokenType))
        {
            claims.Add(new Claim("token_type", tokenType));
        }

        if (mfaTransactionId.HasValue)
        {
            claims.Add(new Claim("mfa_tx", mfaTransactionId.Value.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    public static JsonElement ToJson(object? value)
    {
        return JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement.Clone();
    }

    public static ObjectResult AssertObjectResult(IActionResult result, int expectedStatusCode)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(expectedStatusCode, objectResult.StatusCode);
        return objectResult;
    }
}