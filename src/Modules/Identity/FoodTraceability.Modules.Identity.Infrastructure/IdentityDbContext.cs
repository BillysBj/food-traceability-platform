using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Identity.Infrastructure;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public const string Schema = "identity";

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();

    public DbSet<OrganizationRoleAssignment> OrganizationRoleAssignments =>
        Set<OrganizationRoleAssignment>();

    public DbSet<PlatformRoleAssignment> PlatformRoleAssignments => Set<PlatformRoleAssignment>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.UseFoodTraceabilityModelConventions();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
