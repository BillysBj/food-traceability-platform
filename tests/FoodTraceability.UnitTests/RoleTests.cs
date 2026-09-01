using System.Globalization;
using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.UnitTests;

public sealed class RoleTests
{
    private static readonly Guid RoleId = Guid.Parse("88f99320-3072-4de9-baf2-82f3f1eb256a");

    public static IEnumerable<object?[]> InvalidRoleCodes()
    {
        yield return [null];
        yield return [string.Empty];
        yield return ["   "];
        yield return ["ADMIN!"];
        yield return ["QUALITY-MANAGER"];
        yield return ["QUALITY MANAGER"];
        yield return ["QUALITY.MANAGER"];
        yield return [new string('A', RoleCode.MaximumLength + 1)];
    }

    [Fact]
    public void ValidRoleIsCreatedWithExpectedValues()
    {
        var code = RoleCode.Create("QUALITY_MANAGER");

        var role = Role.Create(
            RoleId,
            code,
            RoleAssignmentScope.Organization,
            "QualityManager",
            "Quality decisions");

        Assert.Equal(RoleId, role.Id);
        Assert.Equal(code, role.Code);
        Assert.Equal(RoleAssignmentScope.Organization, role.AssignmentScope);
        Assert.Equal("QualityManager", role.Name);
        Assert.Equal("Quality decisions", role.Description);
    }

    [Fact]
    public void RoleCodeIsTrimmedAndUppercased()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            var code = RoleCode.Create("  quality_i  ");

            Assert.Equal("QUALITY_I", code.Value);
            Assert.Equal("QUALITY_I", code.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void RoleCodesWithDifferentCasingAreEqual()
    {
        var first = RoleCode.Create("Platform_Admin");
        var second = RoleCode.Create("  PLATFORM_ADMIN  ");

        Assert.Equal(first, second);
    }

    [Theory]
    [MemberData(nameof(InvalidRoleCodes))]
    public void InvalidRoleCodeIsRejected(string? value)
    {
        Assert.Throws<IdentityDomainException>(() => RoleCode.Create(value));
    }

    [Fact]
    public void RoleNameIsTrimmedAndRequired()
    {
        var code = RoleCode.Create("AUDITOR");

        var role = Role.Create(RoleId, code, RoleAssignmentScope.Organization, "  Auditor  ");

        Assert.Equal("Auditor", role.Name);
        Assert.Throws<IdentityDomainException>(() => Role.Create(
            RoleId,
            code,
            RoleAssignmentScope.Organization,
            null));
        Assert.Throws<IdentityDomainException>(() => Role.Create(
            RoleId,
            code,
            RoleAssignmentScope.Organization,
            string.Empty));
        Assert.Throws<IdentityDomainException>(() => Role.Create(
            RoleId,
            code,
            RoleAssignmentScope.Organization,
            "   "));
        Assert.Throws<IdentityDomainException>(() => Role.Create(
            RoleId,
            code,
            RoleAssignmentScope.Organization,
            new string('A', Role.MaximumNameLength + 1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankDescriptionBecomesNull(string? description)
    {
        var role = Role.Create(
            RoleId,
            RoleCode.Create("PRODUCER"),
            RoleAssignmentScope.Organization,
            "Producer",
            description);

        Assert.Null(role.Description);
    }

    [Fact]
    public void RoleDescriptionIsTrimmedAndLimited()
    {
        var role = Role.Create(
            RoleId,
            RoleCode.Create("PRODUCER"),
            RoleAssignmentScope.Organization,
            "Producer",
            "  Creates primary lots  ");

        Assert.Equal("Creates primary lots", role.Description);
        Assert.Throws<IdentityDomainException>(() => Role.Create(
            RoleId,
            RoleCode.Create("PRODUCER"),
            RoleAssignmentScope.Organization,
            "Producer",
            new string('A', Role.MaximumDescriptionLength + 1)));
    }

    [Fact]
    public void EmptyRoleIdIsRejected()
    {
        Assert.Throws<IdentityDomainException>(() => Role.Create(
            Guid.Empty,
            RoleCode.Create("PRODUCER"),
            RoleAssignmentScope.Organization,
            "Producer"));
    }

    [Fact]
    public void RoleRequiresAssignmentScope()
    {
        var code = RoleCode.Create("PRODUCER");

        Assert.Throws<IdentityDomainException>(() => Role.Create(
            RoleId,
            code,
            null,
            "Producer"));
        Assert.Throws<IdentityDomainException>(() => Role.Create(
            RoleId,
            code,
            (RoleAssignmentScope)999,
            "Producer"));
    }
}
