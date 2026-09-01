using FoodTraceability.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodTraceability.Modules.Identity.Infrastructure.Configurations;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permission", IdentityDbContext.Schema);

        builder.HasKey(rolePermission => new
        {
            rolePermission.RoleId,
            rolePermission.PermissionId,
        });

        builder.Property(rolePermission => rolePermission.RoleId)
            .HasColumnName("role_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(rolePermission => rolePermission.PermissionId)
            .HasColumnName("permission_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(rolePermission => rolePermission.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(rolePermission => rolePermission.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(ApprovedRolePermissionMatrix.CreateAssignments());
    }
}
