using FoodTraceability.Modules.Quality.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodTraceability.Modules.Quality.Infrastructure.Configurations;

internal sealed class SpecificationConfiguration : IEntityTypeConfiguration<Specification>
{
    public void Configure(EntityTypeBuilder<Specification> builder)
    {
        builder.ToTable("specification", QualityDbContext.Schema, table =>
        {
            table.HasCheckConstraint("ck_specification_version", "version > 0");
            table.HasCheckConstraint(
                "ck_specification_validity", "valid_to IS NULL OR valid_to >= valid_from");
        });

        builder.HasKey(specification => specification.Id);

        builder.Property(specification => specification.Id)
            .HasColumnName("specification_id")
            .ValueGeneratedNever();

        // D-11: the catalog.article FK is added only in the migration.
        builder.Property(specification => specification.ArticleId)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(specification => specification.Version).IsRequired();
        builder.Property(specification => specification.ValidFrom).IsRequired();
        builder.Property(specification => specification.ValidTo).IsRequired(false);
        builder.Property(specification => specification.CreatedAt).IsRequired();

        builder.HasIndex(specification => new { specification.ArticleId, specification.Version })
            .IsUnique()
            .HasDatabaseName("ux_specification_article_id_version");

        // D-52: overlapping validity periods are allowed in storage. QLT-004
        // reports ambiguity when evaluating the applicable specification.
    }
}
