using System.Globalization;
using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.UnitTests;

public sealed class PermissionTests
{
    private static readonly Guid PermissionId = Guid.Parse("86e39182-d510-4ae8-8073-487bed9a04ed");

    public static IEnumerable<object?[]> InvalidPermissionCodes()
    {
        yield return [null];
        yield return [string.Empty];
        yield return ["   "];
        yield return ["read"];
        yield return [".lot.read"];
        yield return ["lot.read."];
        yield return ["lot..read"];
        yield return ["lot-read"];
        yield return ["lot read"];
        yield return ["lot/read"];
        yield return [$"permission.{new string('a', PermissionCode.MaximumLength)}"];
    }

    [Fact]
    public void ValidPermissionIsCreatedWithExpectedValues()
    {
        var code = PermissionCode.Create("lot.read");

        var permission = Permission.Create(PermissionId, code, "  Reads lots  ");

        Assert.Equal(PermissionId, permission.Id);
        Assert.Equal(code, permission.Code);
        Assert.Equal("Reads lots", permission.Description);
    }

    [Fact]
    public void PermissionCodeIsTrimmedAndLowercased()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            var code = PermissionCode.Create("  QUALITY.I  ");

            Assert.Equal("quality.i", code.Value);
            Assert.Equal("quality.i", code.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void PermissionCodesWithDifferentCasingAreEqual()
    {
        var first = PermissionCode.Create("Trace.Event.Create");
        var second = PermissionCode.Create("  TRACE.EVENT.CREATE  ");

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("trace.event.create")]
    [InlineData("quality.sample.create")]
    public void MultiSegmentPermissionCodeIsAccepted(string value)
    {
        var code = PermissionCode.Create(value);

        Assert.Equal(value, code.Value);
    }

    [Theory]
    [MemberData(nameof(InvalidPermissionCodes))]
    public void InvalidPermissionCodeIsRejected(string? value)
    {
        Assert.Throws<IdentityDomainException>(() => PermissionCode.Create(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankPermissionDescriptionBecomesNull(string? description)
    {
        var permission = Permission.Create(
            PermissionId,
            PermissionCode.Create("permission.read"),
            description);

        Assert.Null(permission.Description);
    }

    [Fact]
    public void PermissionDescriptionIsLimited()
    {
        Assert.Throws<IdentityDomainException>(() => Permission.Create(
            PermissionId,
            PermissionCode.Create("permission.read"),
            new string('A', Permission.MaximumDescriptionLength + 1)));
    }

    [Fact]
    public void EmptyPermissionIdIsRejected()
    {
        Assert.Throws<IdentityDomainException>(() => Permission.Create(
            Guid.Empty,
            PermissionCode.Create("permission.read")));
    }
}
