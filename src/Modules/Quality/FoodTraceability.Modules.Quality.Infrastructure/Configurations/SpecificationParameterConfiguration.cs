using FoodTraceability.Modules.Quality.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodTraceability.Modules.Quality.Infrastructure.Configurations;

internal sealed class SpecificationParameterConfiguration : IEntityTypeConfiguration<SpecificationParameter>
{
    public void Configure(EntityTypeBuilder<SpecificationParameter> builder)
    {
        builder.ToTable("specification_parameter", QualityDbContext.Schema, table =>
            table.HasCheckConstraint(
                "ck_specification_parameter_bounds", "min IS NULL OR max IS NULL OR min <= max"));

        builder.HasKey(parameter => parameter.Id);

        builder.Property(parameter => parameter.Id)
            .HasColumnName("spec_parameter_id")
            .ValueGeneratedNever();

        builder.Property(parameter => parameter.SpecificationId)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(parameter => parameter.ParameterId)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(parameter => parameter.Minimum)
            .HasColumnName("min")
            .HasPrecision(SpecificationParameter.ValuePrecision, SpecificationParameter.ValueScale);

        builder.Property(parameter => parameter.Maximum)
            .HasColumnName("max")
            .HasPrecision(SpecificationParameter.ValuePrecision, SpecificationParameter.ValueScale);

        builder.Property(parameter => parameter.Target)
            .HasPrecision(SpecificationParameter.ValuePrecision, SpecificationParameter.ValueScale);

        // D-52: every caller must supply Required deliberately; no database default.
        builder.Property(parameter => parameter.Required).IsRequired();
        builder.Property(parameter => parameter.CreatedAt).IsRequired();

        builder.HasIndex(parameter => new { parameter.SpecificationId, parameter.ParameterId })
            .IsUnique()
            .HasDatabaseName("ux_specification_parameter_specification_id_parameter_id");

        builder.HasIndex(parameter => parameter.ParameterId)
            .HasDatabaseName("ix_specification_parameter_parameter_id");

        builder.HasOne<Specification>().WithMany()
            .HasForeignKey(parameter => parameter.SpecificationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_specification_parameter_specification");

        builder.HasOne<Parameter>().WithMany()
            .HasForeignKey(parameter => parameter.ParameterId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_specification_parameter_parameter");
    }
}
