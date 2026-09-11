using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace FoodTraceability.Modules.Quality.Infrastructure.Migrations;

[DbContext(typeof(QualityDbContext))]
partial class QualityDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
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
    }
}
