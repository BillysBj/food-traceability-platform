using FoodTraceability.Modules.Quality.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodTraceability.Modules.Quality.Infrastructure.Configurations;

internal sealed class LotBlockConfiguration : IEntityTypeConfiguration<LotBlock>
{
    public void Configure(EntityTypeBuilder<LotBlock> builder)
    {
        builder.ToTable("lot_block", QualityDbContext.Schema, table =>
            table.HasCheckConstraint("ck_lot_block_release_pair",
                "(released_at IS NULL) = (released_by IS NULL)"));

        builder.HasKey(block => block.Id);
        builder.Property(block => block.Id).HasColumnName("lot_block_id").ValueGeneratedNever();
        builder.Property(block => block.OrganizationId).ValueGeneratedNever().IsRequired();
        builder.Property(block => block.LotId).ValueGeneratedNever().IsRequired();
        builder.Property(block => block.Reason).HasMaxLength(LotBlock.MaximumReasonLength).IsRequired();
        builder.Property(block => block.BlockedAt).IsRequired();
        builder.Property(block => block.BlockedBy).ValueGeneratedNever().IsRequired();
        builder.Property(block => block.ReleasedAt);
        builder.Property(block => block.ReleasedBy).ValueGeneratedNever();
        builder.Property(block => block.CreatedAt).IsRequired();

        builder.HasIndex(block => block.LotId)
            .IsUnique()
            .HasFilter("released_at IS NULL")
            .HasDatabaseName("ux_lot_block_lot_id_open");

        // D-11/D-46: the tenant-safe Lot FK and both Identity FKs live only in the migration.
    }
}
