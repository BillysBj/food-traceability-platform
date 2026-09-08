using FoodTraceability.Api.Contracts.Users;

namespace FoodTraceability.IntegrationTests;

public sealed class CreateUserRequestTests
{
    [Fact]
    public void RequestToStringRedactsPasswordAndRetainsDiagnosticFields()
    {
        const string password = "uniquely-recognizable-create-user-request-password";
        var request = new CreateUserRequest(
            "diagnostic.user@example.com",
            "DiagnosticFirstName",
            "DiagnosticLastName",
            password);

        var result = request.ToString();

        Assert.DoesNotContain(password, result, StringComparison.Ordinal);
        Assert.DoesNotContain(
            password.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            result,
            StringComparison.Ordinal);
        Assert.Contains("diagnostic.user@example.com", result, StringComparison.Ordinal);
        Assert.Contains("DiagnosticFirstName", result, StringComparison.Ordinal);
        Assert.Contains("DiagnosticLastName", result, StringComparison.Ordinal);
        Assert.Contains("Password = ***", result, StringComparison.Ordinal);
    }
}
