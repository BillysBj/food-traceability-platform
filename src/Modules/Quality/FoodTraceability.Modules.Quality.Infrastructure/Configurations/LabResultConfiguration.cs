using FoodTraceability.Modules.Quality.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FoodTraceability.Modules.Quality.Infrastructure.Configurations;

internal sealed class LabResultConfiguration : IEntityTypeConfiguration<LabResult>
{
    private static readonly ValueConverter<LabResultAssessment, string> AssessmentConverter = new(
        assessment => ToAssessmentCode(assessment),
        code => FromAssessmentCode(code));

    public void Configure(EntityTypeBuilder<LabResult> builder)
    {
        builder.ToTable(
            "lab_result",
            QualityDbContext.Schema,
            table => table.HasCheckConstraint(
                "ck_lab_result_assessment",
                "assessment IN ('PASS', 'FAIL')"));

        builder.HasKey(result => result.Id);

        builder.Property(result => result.Id)
            .HasColumnName("lab_result_id")
            .ValueGeneratedNever();

        builder.Property(result => result.SampleId)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(result => result.ParameterId)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(result => result.Value)
            .HasPrecision(LabResult.ValuePrecision, LabResult.ValueScale)
            .IsRequired();

        builder.Property(result => result.Assessment)
            .HasConversion(AssessmentConverter)
            .HasMaxLength(4)
            .IsRequired();

        builder.Property(result => result.Method)
            .HasMaxLength(LabResult.MaximumMethodLength)
            .IsRequired();

        builder.Property(result => result.MeasuredAt).IsRequired();
        builder.Property(result => result.CreatedAt).IsRequired();

        // D-51: reassessment requires a new sample, never another result for the same pair.
        builder.HasIndex(result => new { result.SampleId, result.ParameterId })
            .IsUnique()
            .HasDatabaseName("ux_lab_result_sample_id_parameter_id");

        builder.HasIndex(result => result.ParameterId)
            .HasDatabaseName("ix_lab_result_parameter_id");

        // Both principals belong to Quality. No navigation or cross-module EF reference.
        builder.HasOne<Sample>().WithMany()
            .HasForeignKey(result => result.SampleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_lab_result_sample");

        builder.HasOne<Parameter>().WithMany()
            .HasForeignKey(result => result.ParameterId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_lab_result_parameter");
    }

    private static string ToAssessmentCode(LabResultAssessment assessment) => assessment switch
    {
        LabResultAssessment.Pass => "PASS",
        LabResultAssessment.Fail => "FAIL",
        _ => throw new InvalidOperationException($"Unknown lab result assessment '{assessment}'."),
    };

    private static LabResultAssessment FromAssessmentCode(string code) => code switch
    {
        "PASS" => LabResultAssessment.Pass,
        "FAIL" => LabResultAssessment.Fail,
        _ => throw new InvalidOperationException($"Unknown lab result assessment code '{code}'."),
    };
}
