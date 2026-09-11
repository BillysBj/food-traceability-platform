using FoodTraceability.Modules.Quality.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FoodTraceability.Modules.Quality.Infrastructure.Configurations;

internal sealed class ParameterConfiguration : IEntityTypeConfiguration<Parameter>
{
    private static readonly ValueConverter<ParameterCode, string> ParameterCodeConverter = new(
        code => code.Value,
        value => ParameterCode.Create(value));

    public void Configure(EntityTypeBuilder<Parameter> builder)
    {
        builder.ToTable("parameter", QualityDbContext.Schema);

        builder.HasKey(parameter => parameter.Id);

        builder.Property(parameter => parameter.Id)
            .HasColumnName("parameter_id")
            .ValueGeneratedNever();

        builder.Property(parameter => parameter.Code)
            .HasConversion(ParameterCodeConverter)
            .HasMaxLength(ParameterCode.MaximumLength)
            .IsRequired();

        builder.HasIndex(parameter => parameter.Code)
            .IsUnique();

        // The cross-schema FK to catalog.unit is declared only in the migration (D-11).
        builder.Property(parameter => parameter.UnitId)
            .IsRequired(false);

        builder.Property(parameter => parameter.StandardMethod)
            .HasMaxLength(Parameter.MaximumStandardMethodLength)
            .IsRequired();
    }
}
