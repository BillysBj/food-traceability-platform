using FoodTraceability.Modules.Quality.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FoodTraceability.Modules.Quality.Infrastructure.Configurations;

internal sealed class SampleConfiguration : IEntityTypeConfiguration<Sample>
{
    private static readonly ValueConverter<SampleStatus, string> StatusConverter = new(
        status => ToStatusCode(status),
        code => FromStatusCode(code));

    public void Configure(EntityTypeBuilder<Sample> builder)
    {
        builder.ToTable(
            "sample",
            QualityDbContext.Schema,
            table => table.HasCheckConstraint(
                "ck_sample_status",
                "status IN ('PENDING', 'PASS', 'FAIL')"));

        builder.HasKey(sample => sample.Id);

        builder.Property(sample => sample.Id)
            .HasColumnName("sample_id")
            .ValueGeneratedNever();

        builder.Property(sample => sample.OrganizationId)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(sample => sample.LotId)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(sample => sample.LocationId)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(sample => sample.TraceabilityEventId)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(sample => sample.SampleNumber)
            .HasMaxLength(Sample.MaximumSampleNumberLength)
            .IsRequired();

        builder.Property(sample => sample.TakenAt)
            .IsRequired();

        builder.Property(sample => sample.Status)
            .HasConversion(StatusConverter)
            .HasMaxLength(7)
            .IsRequired();

        builder.Property(sample => sample.CreatedAt)
            .IsRequired();

        // The case-insensitive unique index (D-45) and the three cross-module
        // composite foreign keys (D-11, D-46) are declared only in the migration.
    }

    private static string ToStatusCode(SampleStatus status) => status switch
    {
        SampleStatus.Pending => "PENDING",
        SampleStatus.Pass => "PASS",
        SampleStatus.Fail => "FAIL",
        _ => throw new InvalidOperationException($"Unknown sample status '{status}'."),
    };

    private static SampleStatus FromStatusCode(string code) => code switch
    {
        "PENDING" => SampleStatus.Pending,
        "PASS" => SampleStatus.Pass,
        "FAIL" => SampleStatus.Fail,
        _ => throw new InvalidOperationException($"Unknown sample status code '{code}'."),
    };
}
