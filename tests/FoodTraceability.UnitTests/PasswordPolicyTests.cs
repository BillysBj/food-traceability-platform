using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.UnitTests;

public sealed class PasswordPolicyTests
{
    [Fact]
    public void FourteenCharactersAreRejected()
    {
        Assert.Throws<IdentityDomainException>(() =>
            PasswordPolicy.Validate(new string('a', PasswordPolicy.MinimumLength - 1)));
    }

    [Fact]
    public void FifteenCharactersAreAccepted()
    {
        PasswordPolicy.Validate(new string('a', PasswordPolicy.MinimumLength));
    }

    [Fact]
    public void TwoHundredFiftySixCharactersAreAccepted()
    {
        PasswordPolicy.Validate(new string('a', PasswordPolicy.MaximumLength));
    }

    [Fact]
    public void TwoHundredFiftySevenCharactersAreRejected()
    {
        Assert.Throws<IdentityDomainException>(() =>
            PasswordPolicy.Validate(new string('a', PasswordPolicy.MaximumLength + 1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingPasswordIsRejected(string? password)
    {
        Assert.Throws<IdentityDomainException>(() => PasswordPolicy.Validate(password));
    }

    [Fact]
    public void RepeatedLowercaseCharactersAreAcceptedWithoutCompositionRules()
    {
        PasswordPolicy.Validate(new string('a', PasswordPolicy.MinimumLength));
    }

    [Fact]
    public void LeadingAndTrailingSpacesAreAcceptedWithoutChangingPassword()
    {
        const string password = " leading-space ";

        PasswordPolicy.Validate(password);

        Assert.Equal(PasswordPolicy.MinimumLength, password.Length);
        Assert.StartsWith(" ", password, StringComparison.Ordinal);
        Assert.EndsWith(" ", password, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidationExceptionDoesNotContainPasswordOrSubmittedLength()
    {
        var password = new string('q', PasswordPolicy.MinimumLength - 1);

        var exception = Assert.Throws<IdentityDomainException>(() =>
            PasswordPolicy.Validate(password));

        Assert.DoesNotContain(password, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(
            password.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            exception.Message,
            StringComparison.Ordinal);
    }
}
