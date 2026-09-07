using FoodTraceability.Modules.Traceability.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodTraceability.Modules.Traceability.Infrastructure.Configurations;

internal sealed class LotConfiguration : IEntityTypeConfiguration<Lot>
{
    private const int QuantityPrecision = 18;
    private const int QuantityScale = 6;

    public void Configure(EntityTypeBuilder<Lot> builder)
    {
        builder.ToTable(
            "lot",
            TraceabilityDbContext.Schema,
            tableBuilder => tableBuilder.HasCheckConstraint(
                "ck_lot_quantity_positive",
                "quantity > 0"));

        builder.HasKey(lot => lot.Id);

        builder.Property(lot => lot.Id)
            .HasColumnName("lot_id")
            .ValueGeneratedNever();

        builder.Property(lot => lot.OrganizationId)
            .HasColumnName("organization_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(lot => lot.ArticleId)
            .HasColumnName("article_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(lot => lot.LotNumber)
            .HasMaxLength(Lot.MaximumLotNumberLength)
            .IsRequired();

        builder.Property(lot => lot.Quantity)
            .HasPrecision(QuantityPrecision, QuantityScale)
            .IsRequired();

        builder.Property(lot => lot.UnitId)
            .HasColumnName("unit_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(lot => lot.CreatedAt)
            .IsRequired();

        // The foreign keys to catalog.article and catalog.unit are added explicitly in the
        // migration. Configuring them here would require a project reference to the Catalog
        // module and would break module isolation (D-11).
    }
}
