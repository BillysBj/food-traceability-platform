namespace FoodTraceability.Modules.Documents.Domain;

// D-54: no display name or description while the i18n decision D-07 remains open.
public sealed class DocumentType
{
    private DocumentType(Guid id, DocumentTypeCode code)
    {
        Id = id;
        Code = code;
    }

    public Guid Id { get; }

    public DocumentTypeCode Code { get; }

    public static DocumentType Create(Guid id, DocumentTypeCode? code)
    {
        if (id == Guid.Empty)
        {
            throw new DocumentsDomainException("Document type id must not be empty.");
        }

        if (code is null)
        {
            throw new DocumentsDomainException("Document type code must be provided.");
        }

        return new DocumentType(id, code);
    }
}
