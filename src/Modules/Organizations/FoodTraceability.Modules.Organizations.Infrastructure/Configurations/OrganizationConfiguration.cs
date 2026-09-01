using FoodTraceability.Modules.Organizations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodTraceability.Modules.Organizations.Infrastructure.Configurations;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organization", OrganizationsDbContext.Schema);

        builder.HasKey(organization => organization.Id);

        builder.Property(organization => organization.Id)
            .HasColumnName("organization_id")
            .ValueGeneratedNever();

        builder.Property(organization => organization.Name)
            .HasMaxLength(Organization.MaximumNameLength)
            .IsRequired();

        builder.Property(organization => organization.VatId)
            .HasMaxLength(Organization.MaximumVatIdLength);

        builder.Property(organization => organization.TaxNumber)
            .HasMaxLength(Organization.MaximumTaxNumberLength);

        builder.Property(organization => organization.Email)
            .HasMaxLength(Organization.MaximumEmailLength);

        builder.Property(organization => organization.Phone)
            .HasMaxLength(Organization.MaximumPhoneLength);

        builder.Property(organization => organization.CreatedAt)
            .IsRequired();

        builder.Property(organization => organization.UpdatedAt)
            .IsRequired();
    }
}
