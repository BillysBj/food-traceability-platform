using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Quality.Infrastructure.Migrations;

[DbContext(typeof(QualityDbContext))]
[Migration("20260914140000_AddSpecification")]
partial class AddSpecification
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.11")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("FoodTraceability.Modules.Quality.Domain.LabResult", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid")
                .HasColumnName("lab_result_id");

            b.Property<string>("Assessment")
                .IsRequired()
                .HasMaxLength(4)
                .HasColumnType("character varying(4)")
                .HasColumnName("assessment");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");

            b.Property<DateTimeOffset>("MeasuredAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("measured_at");

            b.Property<string>("Method")
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnType("character varying(128)")
                .HasColumnName("method");

            b.Property<Guid>("ParameterId")
                .HasColumnType("uuid")
                .HasColumnName("parameter_id");

            b.Property<Guid>("SampleId")
                .HasColumnType("uuid")
                .HasColumnName("sample_id");

            b.Property<decimal>("Value")
                .HasPrecision(18, 6)
                .HasColumnType("numeric(18,6)")
                .HasColumnName("value");

            b.HasKey("Id")
                .HasName("pk_lab_result");

            b.HasIndex("ParameterId")
                .HasDatabaseName("ix_lab_result_parameter_id");

            b.HasIndex("SampleId", "ParameterId")
                .IsUnique()
                .HasDatabaseName("ux_lab_result_sample_id_parameter_id");

            b.ToTable("lab_result", "quality", t =>
            {
                t.HasCheckConstraint("ck_lab_result_assessment", "assessment IN ('PASS', 'FAIL')");
            });
        });

        modelBuilder.Entity("FoodTraceability.Modules.Quality.Domain.Parameter", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid")
                .HasColumnName("parameter_id");

            b.Property<string>("Code")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("character varying(64)")
                .HasColumnName("code");

            b.Property<string>("StandardMethod")
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnType("character varying(128)")
                .HasColumnName("standard_method");

            b.Property<Guid?>("UnitId")
                .HasColumnType("uuid")
                .HasColumnName("unit_id");

            b.HasKey("Id")
                .HasName("pk_parameter");

            b.HasIndex("Code")
                .IsUnique()
                .HasDatabaseName("ix_parameter_code");

            b.ToTable("parameter", "quality");
        });

        modelBuilder.Entity("FoodTraceability.Modules.Quality.Domain.Sample", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid")
                .HasColumnName("sample_id");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");

            b.Property<Guid>("LocationId")
                .HasColumnType("uuid")
                .HasColumnName("location_id");

            b.Property<Guid>("LotId")
                .HasColumnType("uuid")
                .HasColumnName("lot_id");

            b.Property<Guid>("OrganizationId")
                .HasColumnType("uuid")
                .HasColumnName("organization_id");

            b.Property<string>("SampleNumber")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("character varying(100)")
                .HasColumnName("sample_number");

            b.Property<string>("Status")
                .IsRequired()
                .HasMaxLength(7)
                .HasColumnType("character varying(7)")
                .HasColumnName("status");

            b.Property<DateTimeOffset>("TakenAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("taken_at");

            b.Property<Guid>("TraceabilityEventId")
                .HasColumnType("uuid")
                .HasColumnName("traceability_event_id");

            b.HasKey("Id")
                .HasName("pk_sample");

            b.ToTable("sample", "quality", t =>
            {
                t.HasCheckConstraint("ck_sample_status", "status IN ('PENDING', 'PASS', 'FAIL')");
            });
        });

        modelBuilder.Entity("FoodTraceability.Modules.Quality.Domain.Specification", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid")
                .HasColumnName("specification_id");

            b.Property<Guid>("ArticleId")
                .HasColumnType("uuid")
                .HasColumnName("article_id");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");

            b.Property<DateTimeOffset>("ValidFrom")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("valid_from");

            b.Property<DateTimeOffset?>("ValidTo")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("valid_to");

            b.Property<int>("Version")
                .HasColumnType("integer")
                .HasColumnName("version");

            b.HasKey("Id")
                .HasName("pk_specification");

            b.HasIndex("ArticleId", "Version")
                .IsUnique()
                .HasDatabaseName("ux_specification_article_id_version");

            b.ToTable("specification", "quality", t =>
            {
                t.HasCheckConstraint("ck_specification_validity", "valid_to IS NULL OR valid_to >= valid_from");
                t.HasCheckConstraint("ck_specification_version", "version > 0");
            });
        });

        modelBuilder.Entity("FoodTraceability.Modules.Quality.Domain.SpecificationParameter", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid")
                .HasColumnName("spec_parameter_id");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");

            b.Property<decimal?>("Maximum")
                .HasPrecision(18, 6)
                .HasColumnType("numeric(18,6)")
                .HasColumnName("max");

            b.Property<decimal?>("Minimum")
                .HasPrecision(18, 6)
                .HasColumnType("numeric(18,6)")
                .HasColumnName("min");

            b.Property<Guid>("ParameterId")
                .HasColumnType("uuid")
                .HasColumnName("parameter_id");

            b.Property<bool>("Required")
                .HasColumnType("boolean")
                .HasColumnName("required");

            b.Property<Guid>("SpecificationId")
                .HasColumnType("uuid")
                .HasColumnName("specification_id");

            b.Property<decimal?>("Target")
                .HasPrecision(18, 6)
                .HasColumnType("numeric(18,6)")
                .HasColumnName("target");

            b.HasKey("Id")
                .HasName("pk_specification_parameter");

            b.HasIndex("ParameterId")
                .HasDatabaseName("ix_specification_parameter_parameter_id");

            b.HasIndex("SpecificationId", "ParameterId")
                .IsUnique()
                .HasDatabaseName("ux_specification_parameter_specification_id_parameter_id");

            b.ToTable("specification_parameter", "quality", t =>
            {
                t.HasCheckConstraint("ck_specification_parameter_bounds", "min IS NULL OR max IS NULL OR min <= max");
            });
        });

        modelBuilder.Entity("FoodTraceability.Modules.Quality.Domain.LabResult", b =>
        {
            b.HasOne("FoodTraceability.Modules.Quality.Domain.Parameter", null)
                .WithMany()
                .HasForeignKey("ParameterId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("fk_lab_result_parameter");

            b.HasOne("FoodTraceability.Modules.Quality.Domain.Sample", null)
                .WithMany()
                .HasForeignKey("SampleId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("fk_lab_result_sample");
        });

        modelBuilder.Entity("FoodTraceability.Modules.Quality.Domain.SpecificationParameter", b =>
        {
            b.HasOne("FoodTraceability.Modules.Quality.Domain.Parameter", null)
                .WithMany()
                .HasForeignKey("ParameterId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("fk_specification_parameter_parameter");

            b.HasOne("FoodTraceability.Modules.Quality.Domain.Specification", null)
                .WithMany()
                .HasForeignKey("SpecificationId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("fk_specification_parameter_specification");
        });
    }
}
