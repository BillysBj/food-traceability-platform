using System.Globalization;
using FoodTraceability.Modules.Documents.Domain;

namespace FoodTraceability.UnitTests;

public sealed class DocumentTypeCodeTests
{
    [Fact]
    public void CodeIsTrimmedAndUppercasedInvariantly()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            var code = DocumentTypeCode.Create("  indicator_01  ");

            Assert.Equal("INDICATOR_01", code.Value);
            Assert.Equal(code.Value, code.ToString());
            Assert.Equal(DocumentTypeCode.Create("INDICATOR_01"), code);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingCodeIsRejected(string? code)
    {
        Assert.Throws<DocumentsDomainException>(() => DocumentTypeCode.Create(code));
    }

    [Theory]
    [InlineData("INDICATOR-01")]
    [InlineData("INDICATOR 01")]
    [InlineData("INDICATOR.01")]
    [InlineData("ΔΕΙΚΤΗΣ")]
    [InlineData("INDICATOR\t01")]
    public void DisallowedCharacterIsRejected(string code)
    {
        Assert.Throws<DocumentsDomainException>(() => DocumentTypeCode.Create(code));
    }

    [Fact]
    public void CodeAtMaximumLengthIsAcceptedAfterTrimming()
    {
        var value = new string('A', DocumentTypeCode.MaximumLength);

        Assert.Equal(value, DocumentTypeCode.Create($"  {value}  ").Value);
    }

    [Fact]
    public void CodeOverMaximumLengthIsRejectedAfterTrimming()
    {
        var value = new string('A', DocumentTypeCode.MaximumLength + 1);

        Assert.Throws<DocumentsDomainException>(() => DocumentTypeCode.Create($"  {value}  "));
    }
}
