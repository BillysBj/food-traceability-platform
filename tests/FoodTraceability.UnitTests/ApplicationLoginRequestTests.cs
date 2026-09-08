using FoodTraceability.Modules.Identity.Application.Authentication;

namespace FoodTraceability.UnitTests;

public sealed class ApplicationLoginRequestTests
{
    [Fact]
    public void RequestToStringRedactsPasswordAndRetainsDiagnosticFields()
    {
        const string password = "uniquely-recognizable-application-login-password";
        var request = new LoginRequest("application.login@example.com", password);

        var result = request.ToString();

        Assert.DoesNotContain(password, result, StringComparison.Ordinal);
        Assert.DoesNotContain(
            password.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            result,
            StringComparison.Ordinal);
        Assert.Contains("application.login@example.com", result, StringComparison.Ordinal);
        Assert.Contains("Password = ***", result, StringComparison.Ordinal);
    }
}
