using FoodTraceability.Modules.Documents.Domain;

namespace FoodTraceability.UnitTests;

public sealed class DocumentStorageKeyTests
{
    [Fact]
    public void CreateUsesOrganizationPrefixAndRandomLowercaseHexGuid()
    {
        var organizationId = Guid.NewGuid();
        var first = DocumentStorageKey.Create(organizationId);
        var second = DocumentStorageKey.Create(organizationId);

        Assert.Matches("^[0-9a-f]{32}/[0-9a-f]{32}$", first);
        Assert.Matches("^[0-9a-f]{32}/[0-9a-f]{32}$", second);
        Assert.Equal(65, first.Length);
        Assert.StartsWith($"{organizationId:N}/", first);
        Assert.StartsWith($"{organizationId:N}/", second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void EmptyOrganizationIdIsRejected() =>
        Assert.Throws<DocumentsDomainException>(() => DocumentStorageKey.Create(Guid.Empty));
}
