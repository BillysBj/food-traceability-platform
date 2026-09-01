using FoodTraceability.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FoodTraceability.Modules.Identity.Infrastructure.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    // Persisted values pass through the domain factory again; invalid database data fails materialization.
    private static readonly ValueConverter<RoleCode, string> RoleCodeConverter = new(
        roleCode => roleCode.Value,
        value => RoleCode.Create(value));

    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("role", IdentityDbContext.Schema);

        builder.HasKey(role => role.Id);

        builder.Property(role => role.Id)
            .HasColumnName("role_id")
            .ValueGeneratedNever();

        builder.Property(role => role.Code)
            .HasConversion(RoleCodeConverter)
            .HasMaxLength(RoleCode.MaximumLength)
            .IsRequired();

        builder.HasIndex(role => role.Code)
            .IsUnique();

        builder.Property(role => role.Name)
            .HasMaxLength(Role.MaximumNameLength)
            .IsRequired();

        builder.HasIndex(role => role.Name)
            .IsUnique();

        builder.Property(role => role.Description)
            .HasMaxLength(Role.MaximumDescriptionLength);

        builder.HasData(CreateStandardRoles());
    }

    private static Role[] CreateStandardRoles()
    {
        return
        [
            Role.Create(StandardRoleIds.PlatformAdmin, RoleCode.Create("PLATFORM_ADMIN"), "PlatformAdmin"),
            Role.Create(StandardRoleIds.OrganizationAdmin, RoleCode.Create("ORGANIZATION_ADMIN"), "OrganizationAdmin"),
            Role.Create(StandardRoleIds.Producer, RoleCode.Create("PRODUCER"), "Producer"),
            Role.Create(StandardRoleIds.Processor, RoleCode.Create("PROCESSOR"), "Processor"),
            Role.Create(StandardRoleIds.QualityManager, RoleCode.Create("QUALITY_MANAGER"), "QualityManager"),
            Role.Create(StandardRoleIds.Laboratory, RoleCode.Create("LABORATORY"), "Laboratory"),
            Role.Create(StandardRoleIds.Bottler, RoleCode.Create("BOTTLER"), "Bottler"),
            Role.Create(StandardRoleIds.Logistics, RoleCode.Create("LOGISTICS"), "Logistics"),
            Role.Create(StandardRoleIds.Retailer, RoleCode.Create("RETAILER"), "Retailer"),
            Role.Create(StandardRoleIds.Auditor, RoleCode.Create("AUDITOR"), "Auditor"),
        ];
    }
}
