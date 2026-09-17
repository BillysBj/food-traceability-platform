using FoodTraceability.Modules.Documents.Domain;

namespace FoodTraceability.UnitTests;

public sealed class DocumentContentTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingStorageKeyIsRejected(string? storageKey) =>
        Assert.Throws<DocumentsDomainException>(() => DocumentContent.Create(storageKey, [0x00]));

    [Fact]
    public void StorageKeyLengthBoundaryIsEnforcedWithoutTrimming()
    {
        var storageKey = new string('a', 1024);
        Assert.Equal(storageKey, DocumentContent.Create(storageKey, [0x00]).StorageKey);
        Assert.Throws<DocumentsDomainException>(() => DocumentContent.Create(storageKey + "a", [0x00]));
        Assert.Throws<DocumentsDomainException>(() => DocumentContent.Create(" " + storageKey, [0x00]));
        Assert.Equal("  opaque/key  ", DocumentContent.Create("  opaque/key  ", [0x00]).StorageKey);
    }

    [Fact]
    public void NullAndEmptyContentAreRejected()
    {
        Assert.Throws<DocumentsDomainException>(() => DocumentContent.Create("key", null));
        Assert.Throws<DocumentsDomainException>(() => DocumentContent.Create("key", []));
    }

    [Fact]
    public void CreateCopiesTheSuppliedArray()
    {
        byte[] supplied = [0x00, 0x01, 0xff];
        var content = DocumentContent.Create("key", supplied);
        supplied[0] = 0xff;

        Assert.NotSame(supplied, content.Content);
        Assert.Equal(new byte[] { 0x00, 0x01, 0xff }, content.Content);
    }
}
