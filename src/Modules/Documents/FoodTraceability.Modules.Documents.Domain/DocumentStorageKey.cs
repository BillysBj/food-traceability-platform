namespace FoodTraceability.Modules.Documents.Domain;

public static class DocumentStorageKey
{
    public static string Create(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DocumentsDomainException("Document organization id must not be empty.");
        }

        return $"{organizationId:N}/{Guid.NewGuid():N}";
    }
}
