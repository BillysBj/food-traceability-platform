using FoodTraceability.Modules.Traceability.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodTraceability.Modules.Traceability.Infrastructure.Configurations;

internal sealed class EventOutputConfiguration : IEntityTypeConfiguration<EventOutput>
{
    private const int QuantityPrecision = 18;
    private const int QuantityScale = 6;

    public void Configure(EntityTypeBuilder<EventOutput> builder)
    {
        builder.ToTable(
            "event_output",
            TraceabilityDbContext.Schema,
            tableBuilder => tableBuilder.HasCheckConstraint(
                "ck_event_output_quantity_positive",
                "quantity > 0"));

        builder.HasKey(output => output.Id);

        builder.Property(output => output.Id)
            .HasColumnName("event_output_id")
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

        builder.Property(output => output.LotId)
            .HasColumnName("lot_id")
            .HasColumnOrder(3)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(output => output.Quantity)
            .HasPrecision(QuantityPrecision, QuantityScale)
            .HasColumnOrder(4)
            .IsRequired();

        builder.Property(output => output.UnitId)
            .HasColumnName("unit_id")
            .HasColumnOrder(5)
            .ValueGeneratedNever()
            .IsRequired();

        builder.HasIndex("EventId", nameof(EventOutput.LotId))
            .IsUnique()
            .HasDatabaseName("ux_event_output_event_id_lot_id");

        builder.HasIndex(output => output.LotId);

        // Together with the event FK, this composite FK prevents cross-organization lot
        // references and requires the output unit to equal the referenced lot's unit exactly.
        builder.HasOne<Lot>()
            .WithMany()
            .HasForeignKey(nameof(EventOutput.LotId), "OrganizationId", nameof(EventOutput.UnitId))
            .HasPrincipalKey(lot => new
            {
                lot.Id,
                lot.OrganizationId,
                lot.UnitId,
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_event_output_trace_lot");
    }
}
