using FoodTraceability.Modules.Traceability.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FoodTraceability.Modules.Traceability.Infrastructure.Configurations;

internal sealed class EventTypeConfiguration : IEntityTypeConfiguration<EventType>
{
    private static readonly DateTimeOffset SeededAt =
        new(2026, 9, 9, 0, 0, 0, TimeSpan.Zero);

    private static readonly ValueConverter<EventTypeCode, string> EventTypeCodeConverter = new(
        eventTypeCode => eventTypeCode.Value,
        value => EventTypeCode.Create(value));

    private static readonly ValueConverter<EventTypeClassification, string> ClassificationConverter = new(
        classification => EventTypeClassificationCodes.ToCode(classification),
        code => EventTypeClassificationCodes.FromCode(code));

    public void Configure(EntityTypeBuilder<EventType> builder)
    {
        builder.ToTable(
            "event_type",
            TraceabilityDbContext.Schema,
            table => table.HasCheckConstraint(
                "ck_event_type_classification",
                $"classification IN ('{EventTypeClassificationCodes.Traceability}', '{EventTypeClassificationCodes.TraceabilityAwaitingLogistics}', '{EventTypeClassificationCodes.Logistics}', '{EventTypeClassificationCodes.Quality}', '{EventTypeClassificationCodes.Deferred}')"));

        builder.HasKey(eventType => eventType.Id);

        builder.Property(eventType => eventType.Id)
            .HasColumnName("event_type_id")
            .ValueGeneratedNever();

        builder.Property(eventType => eventType.Code)
            .HasConversion(EventTypeCodeConverter)
            .HasMaxLength(EventTypeCode.MaximumLength)
            .IsRequired();

        builder.HasIndex(eventType => eventType.Code)
            .IsUnique();

        builder.Property(eventType => eventType.Classification)
            .HasConversion(ClassificationConverter)
            .HasMaxLength(EventTypeClassificationCodes.MaximumLength)
            .IsRequired();

        builder.Property(eventType => eventType.CreatedAt)
            .IsRequired();

        builder.HasData(CreateCoreEventTypes());
    }

    private static EventType[] CreateCoreEventTypes()
    {
        return
        [
            EventType.Create(
                Guid.Parse("784ded8c-54a9-595e-b2b2-10acbf16e887"),
                EventTypeCode.Create("HARVEST"),
                EventTypeClassification.Traceability,
                SeededAt),
            EventType.Create(
                Guid.Parse("0195e858-b3f0-54be-ad01-581f168ecf8f"),
                EventTypeCode.Create("RECEIVE"),
                EventTypeClassification.TraceabilityAwaitingLogistics,
                SeededAt),
            EventType.Create(
                Guid.Parse("4e471a84-b9d6-5618-aee9-6ca1f3373ddd"),
                EventTypeCode.Create("TRANSFER"),
                EventTypeClassification.Logistics,
                SeededAt),
            EventType.Create(
                Guid.Parse("26614721-e8f4-5513-a9cf-0c2422fc003b"),
                EventTypeCode.Create("STORE"),
                EventTypeClassification.Deferred,
                SeededAt),
            EventType.Create(
                Guid.Parse("b2830815-b10e-5507-9ed3-13679fc08e5e"),
                EventTypeCode.Create("PRESS"),
                EventTypeClassification.Traceability,
                SeededAt),
            EventType.Create(
                Guid.Parse("99147ae3-6e5b-599d-830d-3f55d80d8242"),
                EventTypeCode.Create("PROCESS"),
                EventTypeClassification.Traceability,
                SeededAt),
            EventType.Create(
                Guid.Parse("58929c0a-8d65-5a38-a6d1-6d74cb144b44"),
                EventTypeCode.Create("MIX"),
                EventTypeClassification.Traceability,
                SeededAt),
            EventType.Create(
                Guid.Parse("5373e85b-e968-55b7-a8bc-7f44008647d2"),
                EventTypeCode.Create("SPLIT"),
                EventTypeClassification.Traceability,
                SeededAt),
            EventType.Create(
                Guid.Parse("49163869-4ef0-501f-984d-ab3adb5e1996"),
                EventTypeCode.Create("SAMPLE"),
                EventTypeClassification.Traceability,
                SeededAt),
            EventType.Create(
                Guid.Parse("dbe268c4-4290-5249-b3ad-5ffe81b3fdc8"),
                EventTypeCode.Create("QUALITY_RELEASE"),
                EventTypeClassification.Quality,
                SeededAt),
            EventType.Create(
                Guid.Parse("7a5bb899-9c04-5dbf-9341-64bc402c2ed3"),
                EventTypeCode.Create("BLOCK"),
                EventTypeClassification.Quality,
                SeededAt),
            EventType.Create(
                Guid.Parse("0283c126-a16a-516d-aeec-b246af44b88a"),
                EventTypeCode.Create("UNBLOCK"),
                EventTypeClassification.Quality,
                SeededAt),
            EventType.Create(
                Guid.Parse("76ad5624-efd3-54d8-903e-7a9fdb221adb"),
                EventTypeCode.Create("BOTTLE"),
                EventTypeClassification.Traceability,
                SeededAt),
            EventType.Create(
                Guid.Parse("b94a7503-17b1-506a-a738-b169290da379"),
                EventTypeCode.Create("PACK"),
                EventTypeClassification.Deferred,
                SeededAt),
            EventType.Create(
                Guid.Parse("7b93d110-a9e4-53af-b3fc-5f7580f7a8f6"),
                EventTypeCode.Create("SHIP"),
                EventTypeClassification.Logistics,
                SeededAt),
            EventType.Create(
                Guid.Parse("243a920d-b978-5202-896e-6428a74a2c22"),
                EventTypeCode.Create("DELIVER"),
                EventTypeClassification.Logistics,
                SeededAt),
            EventType.Create(
                Guid.Parse("3022cd5f-94b0-52d8-af19-2f1959360602"),
                EventTypeCode.Create("SELL"),
                EventTypeClassification.TraceabilityAwaitingLogistics,
                SeededAt),
            EventType.Create(
                Guid.Parse("ad863244-bf75-5f34-886a-eb400f82985d"),
                EventTypeCode.Create("RETURN"),
                EventTypeClassification.TraceabilityAwaitingLogistics,
                SeededAt),
            EventType.Create(
                Guid.Parse("36ca16f4-df60-56f3-8d30-60af75da9c92"),
                EventTypeCode.Create("DISPOSE"),
                EventTypeClassification.Traceability,
                SeededAt),
        ];
    }
}
