namespace FoodTraceability.Platform.Contracts.Traceability;

/// <summary>Resolves only the article of a lot owned by the supplied organization.</summary>
public interface ILotArticleReader
{
    /// <summary>Returns null for both missing lots and lots belonging to another organization.</summary>
    Task<Guid?> FindArticleIdAsync(Guid organizationId, Guid lotId, CancellationToken cancellationToken);
}
