using System.Globalization;
using FoodTraceability.Modules.Organizations.Domain;

namespace FoodTraceability.UnitTests;

public sealed class CountryCodeTests
{
    public static IEnumerable<object?[]> InvalidCountryCodes()
    {
        yield return [null];
        yield return [string.Empty];
        yield return ["   "];
        yield return ["A"];
        yield return ["ABC"];
        yield return ["A1"];
        yield return ["A-"];
        yield return ["ÄT"];
    }

    [Fact]
    public void CountryCodeIsTrimmedAndUppercased()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            var countryCode = CountryCode.Create("  it  ");

            Assert.Equal("IT", countryCode.Value);
            Assert.Equal("IT", countryCode.ToString());
            Assert.Equal(CountryCode.Create("IT"), countryCode);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [MemberData(nameof(InvalidCountryCodes))]
    public void InvalidCountryCodeIsRejected(string? value)
    {
        Assert.Throws<OrganizationsDomainException>(() => CountryCode.Create(value));
    }
}
