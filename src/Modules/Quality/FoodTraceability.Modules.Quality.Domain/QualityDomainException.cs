namespace FoodTraceability.Modules.Quality.Domain;

public sealed class QualityDomainException : Exception
{
    public QualityDomainException(string message)
        : base(message)
    {
    }
}
