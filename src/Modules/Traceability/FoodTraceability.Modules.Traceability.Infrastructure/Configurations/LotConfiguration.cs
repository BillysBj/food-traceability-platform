using FoodTraceability.Modules.Traceability.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodTraceability.Modules.Traceability.Infrastructure.Configurations;

internal sealed class LotConfiguration : IEntityTypeConfiguration<Lot>
{
    public void Configure(EntityTypeBuilder<Lot> builder)
    {
        builder.ToTable("lot", TraceabilityDbContext.Schema);

        builder.HasKey(lot => lot.Id);

        builder.Property(lot => lot.Id)
            .HasColumnName("lot_id")
            .ValueGeneratedNever();

        builder.Property(lot => lot.OrganizationId)
            .HasColumnName("organization_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(lot => lot.LotNumber)
            .HasMaxLength(Lot.MaximumLotNumberLength)
            .IsRequired();

        builder.Property(lot => lot.CreatedAt)
            .IsRequired();
    }
}
