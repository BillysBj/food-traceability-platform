namespace FoodTraceability.Modules.Quality.Application.LabResults;

public interface IApplicableSpecificationReader
{
    // Both validity boundaries are inclusive; a missing end date is unbounded.
    // Return all matches so the Application can report ambiguous configuration.
    Task<IReadOnlyList<ApplicableSpecification>> FindAsync(
        Guid articleId, DateTimeOffset takenAt, CancellationToken cancellationToken);
}

public sealed record ApplicableSpecification(Guid Id, IReadOnlyList<Guid> RequiredParameterIds);
