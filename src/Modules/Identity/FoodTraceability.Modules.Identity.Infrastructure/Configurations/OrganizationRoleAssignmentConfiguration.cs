using FoodTraceability.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FoodTraceability.Modules.Identity.Infrastructure.Configurations;

internal sealed class OrganizationRoleAssignmentConfiguration
    : IEntityTypeConfiguration<OrganizationRoleAssignment>
{
    private static readonly ValueConverter<RoleAssignmentScope, string> AssignmentScopeConverter =
        new(
            scope => RoleAssignmentScopeCodes.ToCode(scope),
            code => RoleAssignmentScopeCodes.FromCode(code));

    public void Configure(EntityTypeBuilder<OrganizationRoleAssignment> builder)
    {
        builder.ToTable(
            "organization_role_assignment",
            IdentityDbContext.Schema,
            table => table.HasCheckConstraint(
                "ck_organization_role_assignment_assignment_scope",
                $"assignment_scope = '{RoleAssignmentScopeCodes.Organization}'"));

        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.Id)
            .HasColumnName("organization_role_assignment_id")
            .ValueGeneratedNever();

        builder.Property(assignment => assignment.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(assignment => assignment.OrganizationId)
            .HasColumnName("organization_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(assignment => assignment.RoleId)
            .HasColumnName("role_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(assignment => assignment.LocationId)
            .HasColumnName("location_id")
            .ValueGeneratedNever();

        builder.Property(assignment => assignment.AssignmentScope)
            .HasColumnName("assignment_scope")
            .HasConversion(AssignmentScopeConverter)
            .HasMaxLength(RoleAssignmentScopeCodes.MaximumLength)
            .IsRequired();

        builder.Property(assignment => assignment.CreatedAt)
            .IsRequired();

        builder.HasOne<OrganizationMembership>()
            .WithMany()
            .HasForeignKey(assignment => new
            {
                assignment.UserId,
                assignment.OrganizationId,
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(assignment => new
            {
                assignment.RoleId,
                assignment.AssignmentScope,
            })
            .HasPrincipalKey(role => new
            {
                role.Id,
                role.AssignmentScope,
            })
            .OnDelete(DeleteBehavior.Restrict);

        // PostgreSQL NULLS NOT DISTINCT makes an organization-wide (NULL location)
        // assignment unique in the same way as a location-specific assignment.
        builder.HasIndex(assignment => new
            {
                assignment.UserId,
                assignment.OrganizationId,
                assignment.RoleId,
                assignment.LocationId,
            })
            .IsUnique()
            .AreNullsDistinct(false);

        // The location/organization FK is added explicitly in the migration.
    }
}
