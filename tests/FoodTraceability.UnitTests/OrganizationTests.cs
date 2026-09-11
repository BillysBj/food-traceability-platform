using FoodTraceability.Modules.Organizations.Domain;

namespace FoodTraceability.UnitTests;

public sealed class OrganizationTests
{
    private static readonly Guid OrganizationId =
        Guid.Parse("aaf1df5a-994f-4bdf-971f-765e7f3473fb");
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 1, 10, 15, 0, TimeSpan.Zero);

    [Fact]
    public void ValidOrganizationIsCreatedWithExpectedValues()
    {
        var organization = Organization.Create(
            OrganizationId,
            "  Aegean Foods  ",
            "  EL123456789  ",
            "  TAX-42  ",
            "  contact@example.com  ",
            "  +30 210 123 4567  ",
            CreatedAt);

        Assert.Equal(OrganizationId, organization.Id);
        Assert.Equal("Aegean Foods", organization.Name);
        Assert.Equal("EL123456789", organization.VatId);
        Assert.Equal("TAX-42", organization.TaxNumber);
        Assert.Equal("contact@example.com", organization.Email);
        Assert.Equal("+30 210 123 4567", organization.Phone);
        Assert.Equal(CreatedAt, organization.CreatedAt);
        Assert.Equal(CreatedAt, organization.UpdatedAt);
    }

    [Fact]
    public void OrganizationNameIsTrimmedAndRequired()
    {
        var organization = Organization.Create(
            OrganizationId,
            "  Aegean Foods  ",
            null,
            null,
            null,
            null,
            CreatedAt);

        Assert.Equal("Aegean Foods", organization.Name);
        Assert.Throws<OrganizationsDomainException>(() => Organization.Create(
            OrganizationId,
            "   ",
            null,
            null,
            null,
            null,
            CreatedAt));
    }

    [Fact]
    public void BlankOptionalOrganizationFieldsBecomeNull()
    {
        var organization = Organization.Create(
            OrganizationId,
            "Aegean Foods",
            null,
            string.Empty,
            "   ",
            "\t",
            CreatedAt);

        Assert.Null(organization.VatId);
        Assert.Null(organization.TaxNumber);
        Assert.Null(organization.Email);
        Assert.Null(organization.Phone);
    }

    [Theory]
    [InlineData("  de 123 456 789  ")]
    [InlineData("DE-123.456.789")]
    public void VatIdIsUppercaseWithOnlyLettersAndDigits(string vatId)
    {
        var organization = Organization.Create(
            OrganizationId, "Aegean Foods", vatId, null, null, null, CreatedAt);

        Assert.Equal("DE123456789", organization.VatId);
    }

    [Fact]
    public void VatIdContainingOnlySeparatorsBecomesNull()
    {
        var organization = Organization.Create(
            OrganizationId, "Aegean Foods", " - . / _ \t ", null, null, null, CreatedAt);

        Assert.Null(organization.VatId);
    }

    [Fact]
    public void VatIdLengthIsCheckedAfterNormalization()
    {
        var vatId = string.Join(" ", Enumerable.Repeat("a", Organization.MaximumVatIdLength));
        var organization = Organization.Create(
            OrganizationId, "Aegean Foods", vatId, null, null, null, CreatedAt);

        Assert.Equal(new string('A', Organization.MaximumVatIdLength), organization.VatId);
        Assert.Throws<OrganizationsDomainException>(() => Organization.Create(
            OrganizationId, "Aegean Foods", vatId + "b", null, null, null, CreatedAt));
    }

    [Fact]
    public void TaxNumberRetainsCaseAndSeparators()
    {
        var organization = Organization.Create(
            OrganizationId, "  Aegean-Foods  ", "de-123", "  tax-12.3 / 4  ",
            "  Contact@example.com  ", "  +30 210-123  ", CreatedAt);

        Assert.Equal("DE123", organization.VatId);
        Assert.Equal("tax-12.3 / 4", organization.TaxNumber);
        Assert.Equal("Aegean-Foods", organization.Name);
        Assert.Equal("Contact@example.com", organization.Email);
        Assert.Equal("+30 210-123", organization.Phone);
    }
}
