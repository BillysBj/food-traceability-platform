using FoodTraceability.Modules.Traceability.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodTraceability.Modules.Traceability.Infrastructure.Configurations;

internal sealed class TraceabilityEventConfiguration
    : IEntityTypeConfiguration<TraceabilityEvent>
{
    public void Configure(EntityTypeBuilder<TraceabilityEvent> builder)
    {
        builder.ToTable("traceability_event", TraceabilityDbContext.Schema);

        builder.HasKey(traceabilityEvent => traceabilityEvent.Id);

        builder.Property(traceabilityEvent => traceabilityEvent.Id)
            .HasColumnName("event_id")
            .ValueGeneratedNever();

        builder.Property(traceabilityEvent => traceabilityEvent.EventTypeId)
            .HasColumnName("event_type_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(traceabilityEvent => traceabilityEvent.OrganizationId)
            .HasColumnName("organization_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(traceabilityEvent => traceabilityEvent.LocationId)
            .HasColumnName("location_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(traceabilityEvent => traceabilityEvent.OccurredAt)
            .IsRequired();

        builder.Property(traceabilityEvent => traceabilityEvent.ExternalReference)
            .HasMaxLength(TraceabilityEvent.MaximumExternalReferenceLength);

        builder.Property(traceabilityEvent => traceabilityEvent.Description)
            .HasMaxLength(TraceabilityEvent.MaximumDescriptionLength);

        builder.Property(traceabilityEvent => traceabilityEvent.CreatedBy)
            .HasColumnName("created_by")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(traceabilityEvent => traceabilityEvent.CreatedAt)
            .IsRequired();

        // This alternate key is intentionally redundant with the primary key. It is the
        // target of composite FKs that structurally enforce each child's tenant scope.
        builder.HasAlternateKey(traceabilityEvent => new
        {
            traceabilityEvent.Id,
            traceabilityEvent.OrganizationId,
        });

        builder.HasOne<EventType>()
            .WithMany()
            .HasForeignKey(traceabilityEvent => traceabilityEvent.EventTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_traceability_event_trace_event_type");

        builder.HasMany(traceabilityEvent => traceabilityEvent.Inputs)
            .WithOne()
            .HasForeignKey("EventId", "OrganizationId")
            .HasPrincipalKey(traceabilityEvent => new
            {
                traceabilityEvent.Id,
                traceabilityEvent.OrganizationId,
            })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_event_input_trace_traceability_event");

        builder.Navigation(traceabilityEvent => traceabilityEvent.Inputs)
            .HasField("_inputs")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(traceabilityEvent => traceabilityEvent.Outputs)
            .WithOne()
            .HasForeignKey("EventId", "OrganizationId")
            .HasPrincipalKey(traceabilityEvent => new
            {
                traceabilityEvent.Id,
                traceabilityEvent.OrganizationId,
            })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_event_output_trace_traceability_event");

        builder.Navigation(traceabilityEvent => traceabilityEvent.Outputs)
            .HasField("_outputs")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Organization, location, and creator FKs cross module boundaries and are therefore
        // added explicitly in the migration rather than to this EF model (D-11).
    }
}
