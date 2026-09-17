namespace FoodTraceability.Modules.Documents.Domain;

public sealed class DocumentsDomainException : Exception
{
    public DocumentsDomainException(string message)
        : base(message)
    {
    }
}
