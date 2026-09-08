using FoodTraceability.Api.Contracts.Authentication;

namespace FoodTraceability.IntegrationTests;

public sealed class ApiLoginRequestTests
{
    [Fact]
    public void RequestToStringRedactsPasswordAndRetainsDiagnosticFields()
    {
        const string password = "uniquely-recognizable-api-login-password";
        var request = new LoginRequest("diagnostic.login@example.com", password);

        var result = request.ToString();

        Assert.DoesNotContain(password, result, StringComparison.Ordinal);
        Assert.DoesNotContain(
            password.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            result,
            StringComparison.Ordinal);
        Assert.Contains("diagnostic.login@example.com", result, StringComparison.Ordinal);
        Assert.Contains("Password = ***", result, StringComparison.Ordinal);
    }
}
