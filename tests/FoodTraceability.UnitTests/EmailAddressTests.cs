using System.Globalization;
using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.UnitTests;

public sealed class EmailAddressTests
{
    public static IEnumerable<object?[]> InvalidEmailAddresses()
    {
        yield return [null];
        yield return [string.Empty];
        yield return ["   "];
        yield return ["user.example.com"];
        yield return ["user@@example.com"];
        yield return ["@example.com"];
        yield return ["user@"];
        yield return ["first last@example.com"];
        yield return [$"{new string('a', 253)}@b"];
    }

    [Fact]
    public void EmailIsTrimmedAndLowercased()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            var email = EmailAddress.Create("  USERI@EXAMPLE.COM  ");

            Assert.Equal("useri@example.com", email.Value);
            Assert.Equal("useri@example.com", email.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void EmailAddressesWithDifferentCasingAreEqual()
    {
        var first = EmailAddress.Create("User@Example.com");
        var second = EmailAddress.Create("  user@example.COM  ");

        Assert.Equal(first, second);
    }

    [Theory]
    [MemberData(nameof(InvalidEmailAddresses))]
    public void InvalidEmailIsRejected(string? value)
    {
        Assert.Throws<IdentityDomainException>(() => EmailAddress.Create(value));
    }
}
