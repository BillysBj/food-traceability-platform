using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Quality.Infrastructure.Migrations;

[DbContext(typeof(QualityDbContext))]
[Migration("20260912121000_AddSample")]
partial class AddSample
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.11")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

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
    }
}
