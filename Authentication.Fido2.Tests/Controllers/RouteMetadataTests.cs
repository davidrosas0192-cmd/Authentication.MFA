using System.Reflection;
using Authentication.Fido2.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Authentication.Fido2.Tests.Controllers;

public class RouteMetadataTests
{
    [Fact]
    public void Controllers_ExposePreferredRestRouteAliases()
    {
        AssertContainsTemplates(typeof(AuthController), "Login", "/api/sessions", "login");
        AssertContainsTemplates(typeof(AuthController), "Logout", "/api/sessions/current", "logout");
        AssertContainsTemplates(typeof(AuthController), "CancelAuthentication", "/api/mfa/sessions/current", "cancel-authentication");

        AssertContainsTemplates(typeof(MfaController), "GetDevicesAvailable", "/api/mfa/setup-options", "devices/available");
        AssertContainsTemplates(typeof(MfaController), "StartChallenge", "/api/mfa/challenges", "challenges/start");
        AssertContainsTemplates(typeof(MfaController), "VerifyChallenge", "/api/mfa/challenges/current", "challenges/verify");
        AssertContainsTemplates(typeof(MfaController), "StartEnrollment", "/api/mfa/enrollments", "enrollment/start");
        AssertContainsTemplates(typeof(MfaController), "VerifyEnrollment", "/api/mfa/enrollments/current", "enrollment/verify");
        AssertContainsTemplates(typeof(MfaController), "RemoveMethod", "methods/{method}");
        AssertContainsTemplates(typeof(MfaController), "StartReconfigureMethod", "methods/{method}/reconfigure");
        AssertContainsTemplates(typeof(MfaController), "CompleteReconfigureMethod", "methods/{method}/reconfigure/current");

        AssertContainsTemplates(typeof(Fido2Controller), "CreateEnrollmentOptions", "/api/fido2/enrollments", "enrollment/options");
        AssertContainsTemplates(typeof(Fido2Controller), "CompleteEnrollment", "/api/fido2/enrollments/current", "enrollment/complete");
        AssertContainsTemplates(typeof(Fido2Controller), "CreateLoginOptions", "/api/fido2/authentications", "login/options");
        AssertContainsTemplates(typeof(Fido2Controller), "CompleteLogin", "/api/fido2/authentications/current", "login/complete");

        AssertContainsTemplates(typeof(UsersController), "CreateUser", "");
        AssertContainsTemplates(typeof(UsersController), "GetAdminUsers", "/api/admin/users");
        AssertContainsTemplates(typeof(UsersController), "CreateUserByAdmin", "/api/admin/users");
        AssertContainsTemplates(typeof(UsersController), "UpdateUserRole", "/api/admin/users/{userId:long}/role");
        AssertContainsTemplates(typeof(UsersController), "UpdateUserStatus", "/api/admin/users/{userId:long}/status");
    }

    private static void AssertContainsTemplates(Type controllerType, string methodName, params string[] expectedTemplates)
    {
        var method = controllerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);

        var templates = method!
            .GetCustomAttributes<HttpMethodAttribute>(inherit: true)
            .SelectMany(attribute => new[] { attribute.Template ?? string.Empty })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var expected in expectedTemplates)
        {
            Assert.Contains(expected, templates);
        }
    }
}