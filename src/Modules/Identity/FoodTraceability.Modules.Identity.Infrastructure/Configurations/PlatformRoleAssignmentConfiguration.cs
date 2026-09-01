using FoodTraceability.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FoodTraceability.Modules.Identity.Infrastructure.Configurations;

internal sealed class PlatformRoleAssignmentConfiguration
    : IEntityTypeConfiguration<PlatformRoleAssignment>
{
    private static readonly ValueConverter<RoleAssignmentScope, string> AssignmentScopeConverter =
        new(
            scope => RoleAssignmentScopeCodes.ToCode(scope),
            code => RoleAssignmentScopeCodes.FromCode(code));

    public void Configure(EntityTypeBuilder<PlatformRoleAssignment> builder)
    {
        builder.ToTable(
            "platform_role_assignment",
            IdentityDbContext.Schema,
            table => table.HasCheckConstraint(
                "ck_platform_role_assignment_assignment_scope",
                $"assignment_scope = '{RoleAssignmentScopeCodes.Platform}'"));

        builder.HasKey(assignment => new
        {
            assignment.UserId,
            assignment.RoleId,
        });

        builder.Property(assignment => assignment.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(assignment => assignment.RoleId)
            .HasColumnName("role_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(assignment => assignment.AssignmentScope)
            .HasColumnName("assignment_scope")
            .HasConversion(AssignmentScopeConverter)
            .HasMaxLength(RoleAssignmentScopeCodes.MaximumLength)
            .IsRequired();

        builder.Property(assignment => assignment.CreatedAt)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(assignment => assignment.UserId)
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
    }
}
