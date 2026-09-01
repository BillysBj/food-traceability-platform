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

    private static readonly ValueConverter<RoleAssignmentScope, string> AssignmentScopeConverter =
        new(
            scope => RoleAssignmentScopeCodes.ToCode(scope),
            code => RoleAssignmentScopeCodes.FromCode(code));

    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable(
            "role",
            IdentityDbContext.Schema,
            table => table.HasCheckConstraint(
                "ck_role_assignment_scope",
                $"assignment_scope IN ('{RoleAssignmentScopeCodes.Platform}', '{RoleAssignmentScopeCodes.Organization}')"));

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

        builder.Property(role => role.AssignmentScope)
            .HasColumnName("assignment_scope")
            .HasConversion(AssignmentScopeConverter)
            .HasMaxLength(RoleAssignmentScopeCodes.MaximumLength)
            .IsRequired();

        // This alternate key is intentionally redundant with the primary key. It is the
        // target of composite FKs that structurally enforce each role's assignment scope.
        builder.HasAlternateKey(role => new
        {
            role.Id,
            role.AssignmentScope,
        });

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
            Role.Create(StandardRoleIds.PlatformAdmin, RoleCode.Create("PLATFORM_ADMIN"), RoleAssignmentScope.Platform, "PlatformAdmin"),
            Role.Create(StandardRoleIds.OrganizationAdmin, RoleCode.Create("ORGANIZATION_ADMIN"), RoleAssignmentScope.Organization, "OrganizationAdmin"),
            Role.Create(StandardRoleIds.Producer, RoleCode.Create("PRODUCER"), RoleAssignmentScope.Organization, "Producer"),
            Role.Create(StandardRoleIds.Processor, RoleCode.Create("PROCESSOR"), RoleAssignmentScope.Organization, "Processor"),
            Role.Create(StandardRoleIds.QualityManager, RoleCode.Create("QUALITY_MANAGER"), RoleAssignmentScope.Organization, "QualityManager"),
            Role.Create(StandardRoleIds.Laboratory, RoleCode.Create("LABORATORY"), RoleAssignmentScope.Organization, "Laboratory"),
            Role.Create(StandardRoleIds.Bottler, RoleCode.Create("BOTTLER"), RoleAssignmentScope.Organization, "Bottler"),
            Role.Create(StandardRoleIds.Logistics, RoleCode.Create("LOGISTICS"), RoleAssignmentScope.Organization, "Logistics"),
            Role.Create(StandardRoleIds.Retailer, RoleCode.Create("RETAILER"), RoleAssignmentScope.Organization, "Retailer"),
            Role.Create(StandardRoleIds.Auditor, RoleCode.Create("AUDITOR"), RoleAssignmentScope.Organization, "Auditor"),
        ];
    }
}
