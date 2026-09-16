namespace FoodTraceability.Platform.Contracts.Traceability;

/// <summary>Writes the lot state decided by Quality; does not authorize or decide transitions.</summary>
public interface ILotQualityStatusWriter
{
    /// <summary>
    /// Returns false if the lot does not exist in the given organization, including a lot
    /// belonging to another organization. Returns true even if the status is unchanged.
    /// Participates in an active scope transaction without committing or rolling it back.
    /// </summary>
    Task<bool> SetAsync(
        Guid organizationId,
        Guid lotId,
        LotQualityStatus qualityStatus,
        CancellationToken cancellationToken);
}
