using FoodTraceability.Modules.Documents.Domain;

namespace FoodTraceability.UnitTests;

public sealed class DocumentTests
{
    private const string ValidSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateOnly DocumentDate = new(2026, 9, 17);
    private static readonly DateTimeOffset CreatedAt = new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero)
        .AddTicks(1234567);

    [Fact]
    public void CreatePreservesIdsDateAndSubMicrosecondTimestampAndNormalizesText()
    {
        var id = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var document = Create(id: id, organizationId: organizationId,
            name: "  Report  ", fileName: "  report.pdf  ",
            storageKey: "  opaque/key  ", mimeType: "  APPLICATION/PDF  ",
            sha256: ValidSha256.ToUpperInvariant());

        Assert.Equal(id, document.Id);
        Assert.Equal(StandardDocumentTypeIds.LabReport, document.DocumentTypeId);
        Assert.Equal(organizationId, document.OrganizationId);
        Assert.Equal("Report", document.Name);
        Assert.Equal("report.pdf", document.FileName);
        Assert.Equal("  opaque/key  ", document.StorageKey);
        Assert.Equal("application/pdf", document.MimeType);
        Assert.Equal(ValidSha256, document.Sha256);
        Assert.Equal(DocumentDate, document.DocumentDate);
        Assert.Equal(CreatedAt, document.CreatedAt);
        Assert.Equal(7, document.CreatedAt.Ticks % 10);
    }

    [Fact]
    public void EmptyIdIsRejected() =>
        Assert.Throws<DocumentsDomainException>(() => Create(id: Guid.Empty));

    [Fact]
    public void EmptyDocumentTypeIdIsRejected() =>
        Assert.Throws<DocumentsDomainException>(() => Create(documentTypeId: Guid.Empty));

    [Fact]
    public void EmptyOrganizationIdIsRejected() =>
        Assert.Throws<DocumentsDomainException>(() => Create(organizationId: Guid.Empty));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingNameIsRejected(string? value) =>
        Assert.Throws<DocumentsDomainException>(() => Create(name: value));

    [Fact]
    public void NameLengthBoundaryIsEnforcedAfterTrimming()
    {
        var value = new string('a', Document.MaximumNameLength);
        Assert.Equal(value, Create(name: $"  {value}  ").Name);
        Assert.Throws<DocumentsDomainException>(() => Create(name: value + "a"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingFileNameIsRejected(string? value) =>
        Assert.Throws<DocumentsDomainException>(() => Create(fileName: value));

    [Fact]
    public void FileNameLengthBoundaryIsEnforcedAfterTrimming()
    {
        var value = new string('a', Document.MaximumFileNameLength);
        Assert.Equal(value, Create(fileName: $"  {value}  ").FileName);
        Assert.Throws<DocumentsDomainException>(() => Create(fileName: value + "a"));
    }

    [Theory]
    [InlineData("path/report.pdf")]
    [InlineData(@"path\report.pdf")]
    public void FileNameWithPathSeparatorIsRejected(string value) =>
        Assert.Throws<DocumentsDomainException>(() => Create(fileName: value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingStorageKeyIsRejected(string? value) =>
        Assert.Throws<DocumentsDomainException>(() => Create(storageKey: value));

    [Fact]
    public void StorageKeyLengthBoundaryIsEnforcedWithoutTrimming()
    {
        var value = new string('a', Document.MaximumStorageKeyLength);
        Assert.Equal(value, Create(storageKey: value).StorageKey);
        Assert.Throws<DocumentsDomainException>(() => Create(storageKey: value + "a"));
        Assert.Throws<DocumentsDomainException>(() => Create(storageKey: " " + value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("application")]
    [InlineData("/pdf")]
    [InlineData("application/")]
    [InlineData("application/pdf/extra")]
    [InlineData(" /pdf")]
    [InlineData("application/ ")]
    public void InvalidMimeTypeIsRejected(string? value) =>
        Assert.Throws<DocumentsDomainException>(() => Create(mimeType: value));

    [Fact]
    public void MimeTypeLengthBoundaryIsEnforcedAfterTrimming()
    {
        var value = "a/" + new string('b', Document.MaximumMimeTypeLength - 2);
        Assert.Equal(value, Create(mimeType: $"  {value}  ").MimeType);
        Assert.Throws<DocumentsDomainException>(() => Create(mimeType: value + "b"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingSha256IsRejected(string? value) =>
        Assert.Throws<DocumentsDomainException>(() => Create(sha256: value));

    [Theory]
    [InlineData(63)]
    [InlineData(65)]
    public void Sha256MustHaveExactly64Characters(int length) =>
        Assert.Throws<DocumentsDomainException>(() => Create(sha256: new string('a', length)));

    [Theory]
    [InlineData('g')]
    [InlineData('G')]
    [InlineData('-')]
    [InlineData(' ')]
    [InlineData('é')]
    public void NonHexadecimalSha256IsRejected(char character) =>
        Assert.Throws<DocumentsDomainException>(() => Create(sha256: new string('a', 63) + character));

    [Fact]
    public void Sha256IsNotTrimmed() =>
        Assert.Throws<DocumentsDomainException>(() => Create(sha256: " " + ValidSha256));

    [Fact]
    public void DocumentTypePreservesIdAndCode()
    {
        var id = Guid.NewGuid();
        var code = DocumentTypeCode.Create("lab_report");
        var type = DocumentType.Create(id, code);
        Assert.Equal(id, type.Id);
        Assert.Equal(code, type.Code);
    }

    [Fact]
    public void DocumentTypeRequiresIdAndCode()
    {
        Assert.Throws<DocumentsDomainException>(() =>
            DocumentType.Create(Guid.Empty, DocumentTypeCode.Create("LAB_REPORT")));
        Assert.Throws<DocumentsDomainException>(() => DocumentType.Create(Guid.NewGuid(), null));
    }

    private static Document Create(
        Guid? id = null,
        Guid? documentTypeId = null,
        Guid? organizationId = null,
        string? name = "Report",
        string? fileName = "report.pdf",
        string? storageKey = "documents/report",
        string? mimeType = "application/pdf",
        string? sha256 = ValidSha256) =>
        Document.Create(id ?? Guid.NewGuid(), documentTypeId ?? StandardDocumentTypeIds.LabReport,
            organizationId ?? Guid.NewGuid(), name, fileName, storageKey, mimeType,
            DocumentDate, sha256, CreatedAt);
}
