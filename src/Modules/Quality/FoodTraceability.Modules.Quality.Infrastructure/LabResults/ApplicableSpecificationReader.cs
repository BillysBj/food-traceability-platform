using FoodTraceability.Modules.Quality.Application.LabResults;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Quality.Infrastructure.LabResults;

internal sealed class ApplicableSpecificationReader(QualityDbContext dbContext, ScopedTransaction transaction)
    : IApplicableSpecificationReader
{
    public async Task<IReadOnlyList<ApplicableSpecification>> FindAsync(
        Guid articleId, DateTimeOffset takenAt, CancellationToken cancellationToken)
    {
        await transaction.EnlistAsync(dbContext, cancellationToken);
        var specificationIds = await dbContext.Specifications.AsNoTracking()
            .Where(specification => specification.ArticleId == articleId
                && specification.ValidFrom <= takenAt
                && (specification.ValidTo == null || takenAt <= specification.ValidTo))
            .Select(specification => specification.Id)
            .ToListAsync(cancellationToken);
        var requiredParameters = await dbContext.SpecificationParameters.AsNoTracking()
            .Where(parameter => specificationIds.Contains(parameter.SpecificationId) && parameter.Required)
            .Select(parameter => new { parameter.SpecificationId, parameter.ParameterId })
            .ToListAsync(cancellationToken);
        var requiredBySpecification = requiredParameters.ToLookup(parameter => parameter.SpecificationId);
        return specificationIds.Select(id => new ApplicableSpecification(
            id, requiredBySpecification[id].Select(parameter => parameter.ParameterId).ToArray())).ToArray();
    }
}
