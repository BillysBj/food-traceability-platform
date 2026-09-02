using FoodTraceability.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FoodTraceability.Modules.Catalog.Infrastructure.Configurations;

internal sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    private static readonly DateTimeOffset SeededAt =
        new(2026, 9, 2, 0, 0, 0, TimeSpan.Zero);

    private static readonly ValueConverter<UnitCode, string> UnitCodeConverter = new(
        unitCode => unitCode.Value,
        value => UnitCode.Create(value));

    private static readonly ValueConverter<UnitDimension, string> DimensionConverter = new(
        dimension => UnitDimensionCodes.ToCode(dimension),
        code => UnitDimensionCodes.FromCode(code));

    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable(
            "unit",
            CatalogDbContext.Schema,
            table => table.HasCheckConstraint(
                "ck_unit_dimension",
                $"dimension IN ('{UnitDimensionCodes.Mass}', '{UnitDimensionCodes.Volume}', '{UnitDimensionCodes.Count}')"));

        builder.HasKey(unit => unit.Id);

        builder.Property(unit => unit.Id)
            .HasColumnName("unit_id")
            .ValueGeneratedNever();

        builder.Property(unit => unit.Code)
            .HasConversion(UnitCodeConverter)
            .HasMaxLength(UnitCode.MaximumLength)
            .IsRequired();

        builder.HasIndex(unit => unit.Code)
            .IsUnique();

        builder.Property(unit => unit.Symbol)
            .HasMaxLength(Unit.MaximumSymbolLength)
            .IsRequired();

        builder.Property(unit => unit.Dimension)
            .HasConversion(DimensionConverter)
            .HasMaxLength(UnitDimensionCodes.MaximumLength)
            .IsRequired();

        builder.Property(unit => unit.CreatedAt)
            .IsRequired();

        builder.HasData(CreateStandardUnits());
    }

    private static Unit[] CreateStandardUnits()
    {
        return
        [
            Unit.Create(
                Guid.Parse("4ba563a7-f314-57d8-b3d7-ee5c12ff1085"),
                UnitCode.Create("KG"),
                "kg",
                UnitDimension.Mass,
                SeededAt),
            Unit.Create(
                Guid.Parse("5e726b86-c672-5ed0-9601-904328038341"),
                UnitCode.Create("G"),
                "g",
                UnitDimension.Mass,
                SeededAt),
            Unit.Create(
                Guid.Parse("8d8ed466-8384-5e44-8430-eee76f15a180"),
                UnitCode.Create("L"),
                "l",
                UnitDimension.Volume,
                SeededAt),
            Unit.Create(
                Guid.Parse("dd541026-8821-53a3-97de-f0a974327970"),
                UnitCode.Create("ML"),
                "ml",
                UnitDimension.Volume,
                SeededAt),
            Unit.Create(
                Guid.Parse("d227d884-ef6c-5667-9587-1d9fdee6836e"),
                UnitCode.Create("PCS"),
                "pcs",
                UnitDimension.Count,
                SeededAt),
        ];
    }
}
