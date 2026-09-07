using FoodTraceability.Modules.Traceability.Domain;

namespace FoodTraceability.Modules.Traceability.Application.Lots;

public sealed class CreateLotService(
    ILotWriter writer,
    TimeProvider timeProvider)
{
    private const decimal MaximumSupportedQuantity = 999999999999.999999m;

    public async Task<LotDetails> CreateAsync(
        CreateLotCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (decimal.Round(command.Quantity, 6) != command.Quantity)
        {
            throw new LotValidationException(
                "Quantity must not have more than 6 decimal places.");
        }

        if (command.Quantity == decimal.MinValue
            || Math.Abs(command.Quantity) > MaximumSupportedQuantity)
        {
            throw new LotValidationException("Quantity exceeds the supported range.");
        }

        Lot lot;
        try
        {
            lot = Lot.Create(
                Guid.NewGuid(),
                command.OrganizationId,
                command.ArticleId,
                command.LotNumber,
                command.Quantity,
                command.UnitId,
                timeProvider.GetUtcNow());
        }
        catch (TraceabilityDomainException exception)
        {
            throw new LotValidationException(exception.Message);
        }

        await writer.AddAsync(lot, cancellationToken);

        return new LotDetails(
            lot.Id,
            lot.OrganizationId,
            lot.ArticleId,
            lot.LotNumber,
            lot.Quantity,
            lot.UnitId,
            lot.CreatedAt);
    }
}
