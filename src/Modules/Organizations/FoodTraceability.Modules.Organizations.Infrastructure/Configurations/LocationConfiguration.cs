using FoodTraceability.Modules.Organizations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FoodTraceability.Modules.Organizations.Infrastructure.Configurations;

internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public const int CoordinatePrecision = 9;
    public const int CoordinateScale = 6;

    // Persisted values pass through the domain factory again; invalid database data fails materialization.
    private static readonly ValueConverter<CountryCode?, string?> CountryCodeConverter = new(
        countryCode => countryCode == null ? null : countryCode.Value,
        value => value == null ? null : CountryCode.Create(value));

    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("location", OrganizationsDbContext.Schema, tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_location_coordinates_complete",
                "(latitude IS NULL AND longitude IS NULL) OR (latitude IS NOT NULL AND longitude IS NOT NULL)");
            tableBuilder.HasCheckConstraint(
                "ck_location_latitude_range",
                "latitude IS NULL OR latitude BETWEEN -90 AND 90");
            tableBuilder.HasCheckConstraint(
                "ck_location_longitude_range",
                "longitude IS NULL OR longitude BETWEEN -180 AND 180");
        });

        builder.HasKey(location => location.Id);

        builder.Property(location => location.Id)
            .HasColumnName("location_id")
            .ValueGeneratedNever();

        builder.Property(location => location.OrganizationId)
            .HasColumnName("organization_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(location => location.Name)
            .HasMaxLength(Location.MaximumNameLength)
            .IsRequired();

        builder.Property(location => location.City)
            .HasMaxLength(Location.MaximumCityLength);

        builder.Property(location => location.Region)
            .HasMaxLength(Location.MaximumRegionLength);

        builder.Property(location => location.CountryCode)
            .HasConversion(CountryCodeConverter)
            .HasColumnType("character(2)")
            .HasMaxLength(CountryCode.Length);

        builder.Property(location => location.Latitude)
            .HasPrecision(CoordinatePrecision, CoordinateScale);

        builder.Property(location => location.Longitude)
            .HasPrecision(CoordinatePrecision, CoordinateScale);

        builder.Property(location => location.CreatedAt)
            .IsRequired();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(location => location.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Required for ID-004: PostgreSQL needs a unique principal column set for the future
        // composite (location_id, organization_id) foreign key that enforces same-tenant scope.
        builder.HasAlternateKey(location => new { location.Id, location.OrganizationId });
    }
}
