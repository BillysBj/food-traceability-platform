using FoodTraceability.Modules.Traceability.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodTraceability.Modules.Traceability.Infrastructure.Configurations;

internal sealed class EventInputConfiguration : IEntityTypeConfiguration<EventInput>
{
    private const int QuantityPrecision = 18;
    private const int QuantityScale = 6;

    public void Configure(EntityTypeBuilder<EventInput> builder)
    {
        builder.ToTable(
            "event_input",
            TraceabilityDbContext.Schema,
            tableBuilder => tableBuilder.HasCheckConstraint(
                "ck_event_input_quantity_positive",
                "quantity > 0"));

        builder.HasKey(input => input.Id);

        builder.Property(input => input.Id)
            .HasColumnName("event_input_id")
            .HasColumnOrder(0)
            .ValueGeneratedNever();

        builder.Property<Guid>("EventId")
            .HasColumnName("event_id")
            .HasColumnOrder(1)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property<Guid>("OrganizationId")
            .HasColumnName("organization_id")
            .HasColumnOrder(2)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(input => input.LotId)
            .HasColumnName("lot_id")
            .HasColumnOrder(3)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(input => input.Quantity)
            .HasPrecision(QuantityPrecision, QuantityScale)
            .HasColumnOrder(4)
            .IsRequired();

        builder.Property(input => input.UnitId)
            .HasColumnName("unit_id")
            .HasColumnOrder(5)
            .ValueGeneratedNever()
            .IsRequired();

        builder.HasIndex("EventId", nameof(EventInput.LotId))
            .IsUnique()
            .HasDatabaseName("ux_event_input_event_id_lot_id");

        builder.HasIndex(input => input.LotId);

        // Together with the event FK, this composite FK prevents cross-organization lot
        // references and requires the input unit to equal the referenced lot's unit exactly.
        builder.HasOne<Lot>()
            .WithMany()
            .HasForeignKey(nameof(EventInput.LotId), "OrganizationId", nameof(EventInput.UnitId))
            .HasPrincipalKey(lot => new
            {
                lot.Id,
                lot.OrganizationId,
                lot.UnitId,
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_event_input_trace_lot");
    }
}
