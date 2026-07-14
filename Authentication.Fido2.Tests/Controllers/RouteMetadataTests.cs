using System.Reflection;
using Authentication.Fido2.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Authentication.Fido2.Tests.Controllers;

public class RouteMetadataTests
{
    [Fact]
    public void Controllers_ExposeSingleRestRoutes()
    {
        AssertContainsTemplates(typeof(AuthController), "Login", "/api/sessions");
        AssertContainsTemplates(typeof(AuthController), "Logout", "/api/sessions/current");
        AssertContainsTemplates(typeof(AuthController), "CancelAuthentication", "/api/mfa/sessions/current");

        AssertContainsTemplates(typeof(MfaController), "GetMethods", "methods");
        AssertContainsTemplates(typeof(MfaController), "GetDevicesAvailable", "setup-options");
        AssertContainsTemplates(typeof(MfaController), "StartChallenge", "challenges");
        AssertContainsTemplates(typeof(MfaController), "VerifyChallenge", "challenges/current");
        AssertContainsTemplates(typeof(MfaController), "StartEnrollment", "enrollments");
        AssertContainsTemplates(typeof(MfaController), "VerifyEnrollment", "enrollments/current");
        AssertContainsTemplates(typeof(MfaController), "StartManagementSession", "management-sessions");
        AssertContainsTemplates(typeof(MfaController), "StartManagementChallenge", "management-sessions/challenges/start");
        AssertContainsTemplates(typeof(MfaController), "VerifyManagementChallenge", "management-sessions/challenges/verify");
        AssertContainsTemplates(typeof(MfaController), "CompleteManagementSession", "management-sessions/complete");
        AssertContainsTemplates(typeof(MfaController), "CancelManagementSession", "management-sessions/{mfaTransactionId}");
        AssertContainsTemplates(typeof(MfaController), "RemoveMethod", "methods/{method}");
        AssertContainsTemplates(typeof(MfaController), "StartReconfigureMethod", "methods/{method}/reconfigure");
        AssertContainsTemplates(typeof(MfaController), "CompleteReconfigureMethod", "methods/{method}/reconfigure/current");

        AssertContainsTemplates(typeof(Fido2Controller), "CreateEnrollmentOptions", "enrollments");
        AssertContainsTemplates(typeof(Fido2Controller), "CompleteEnrollment", "enrollments/current");
        AssertContainsTemplates(typeof(Fido2Controller), "CreateLoginOptions", "authentications");
        AssertContainsTemplates(typeof(Fido2Controller), "CompleteLogin", "authentications/current");
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