namespace FoodTraceability.Modules.Traceability.Application.EventTypes;

public interface IEventTypeReader
{
    Task<Guid?> FindIdByCodeAsync(string code, CancellationToken cancellationToken);

    Task<string?> FindCodeByIdAsync(Guid eventTypeId, CancellationToken cancellationToken);
}
