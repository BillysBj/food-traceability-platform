using FoodTraceability.Modules.Quality.Application.Specifications;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Quality.Infrastructure.Specifications;

internal sealed class SpecificationReader(QualityDbContext dbContext) : ISpecificationReader
{
    public Task<SampleSpecificationContext?> FindSampleAsync(
        Guid organizationId, Guid sampleId, CancellationToken cancellationToken) =>
        dbContext.Samples.AsNoTracking()
            .Where(sample => sample.OrganizationId == organizationId && sample.Id == sampleId)
            .Select(sample => new SampleSpecificationContext(sample.LotId, sample.TakenAt))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<SpecificationDetails> GetAsync(Guid specificationId, CancellationToken cancellationToken)
    {
        var specification = await dbContext.Specifications.AsNoTracking()
            .Where(item => item.Id == specificationId)
            .Select(item => new { item.Id, item.ArticleId, item.Version, item.ValidFrom, item.ValidTo })
            .SingleAsync(cancellationToken);
        var parameters = await (
            from configured in dbContext.SpecificationParameters.AsNoTracking()
            join parameter in dbContext.Parameters.AsNoTracking() on configured.ParameterId equals parameter.Id
            where configured.SpecificationId == specificationId
            select new
            {
                configured.ParameterId, parameter.Code, parameter.StandardMethod, parameter.UnitId,
                configured.Minimum, configured.Maximum, configured.Target, configured.Required,
            }).ToListAsync(cancellationToken);

        // ParameterCode is an EF value-converted domain value. Sort the returned
        // technical codes ordinally after materialization, without a collation dependency.
        var details = parameters.Select(parameter => new SpecificationParameterDetails(
            parameter.ParameterId, parameter.Code.Value, parameter.StandardMethod, parameter.UnitId,
            parameter.Minimum, parameter.Maximum, parameter.Target, parameter.Required))
            .OrderBy(parameter => parameter.ParameterCode, StringComparer.Ordinal).ToArray();
        return new SpecificationDetails(specification.Id, specification.ArticleId, specification.Version,
            specification.ValidFrom, specification.ValidTo, details);
    }
}
