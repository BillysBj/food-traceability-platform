using FoodTraceability.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FoodTraceability.Modules.Identity.Infrastructure.Configurations;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    // Persisted values pass through the domain factory again; invalid database data fails materialization.
    private static readonly ValueConverter<PermissionCode, string> PermissionCodeConverter = new(
        permissionCode => permissionCode.Value,
        value => PermissionCode.Create(value));

    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permission", IdentityDbContext.Schema);

        builder.HasKey(permission => permission.Id);

        builder.Property(permission => permission.Id)
            .HasColumnName("permission_id")
            .ValueGeneratedNever();

        builder.Property(permission => permission.Code)
            .HasConversion(PermissionCodeConverter)
            .HasMaxLength(PermissionCode.MaximumLength)
            .IsRequired();

        builder.HasIndex(permission => permission.Code)
            .IsUnique();

        builder.Property(permission => permission.Description)
            .HasMaxLength(Permission.MaximumDescriptionLength);

        builder.HasData(CreateStandardPermissions());
    }

    private static Permission[] CreateStandardPermissions()
    {
        return
        [
            Permission.Create(StandardPermissionIds.OrganizationRead, PermissionCode.Create("organization.read")),
            Permission.Create(StandardPermissionIds.OrganizationManage, PermissionCode.Create("organization.manage")),
            Permission.Create(StandardPermissionIds.UserRead, PermissionCode.Create("user.read")),
            Permission.Create(StandardPermissionIds.UserManage, PermissionCode.Create("user.manage")),
            Permission.Create(StandardPermissionIds.RoleRead, PermissionCode.Create("role.read")),
            Permission.Create(StandardPermissionIds.PermissionRead, PermissionCode.Create("permission.read")),
            Permission.Create(StandardPermissionIds.ProductRead, PermissionCode.Create("product.read")),
            Permission.Create(StandardPermissionIds.ProductCreate, PermissionCode.Create("product.create")),
            Permission.Create(StandardPermissionIds.ProductUpdate, PermissionCode.Create("product.update")),
            Permission.Create(StandardPermissionIds.ArticleRead, PermissionCode.Create("article.read")),
            Permission.Create(StandardPermissionIds.ArticleCreate, PermissionCode.Create("article.create")),
            Permission.Create(StandardPermissionIds.ArticleUpdate, PermissionCode.Create("article.update")),
            Permission.Create(StandardPermissionIds.LotRead, PermissionCode.Create("lot.read")),
            Permission.Create(StandardPermissionIds.LotCreate, PermissionCode.Create("lot.create")),
            Permission.Create(StandardPermissionIds.LotUpdate, PermissionCode.Create("lot.update")),
            Permission.Create(StandardPermissionIds.TraceRead, PermissionCode.Create("trace.read")),
            Permission.Create(StandardPermissionIds.TraceEventCreate, PermissionCode.Create("trace.event.create")),
            Permission.Create(StandardPermissionIds.QualityRead, PermissionCode.Create("quality.read")),
            Permission.Create(StandardPermissionIds.QualitySampleCreate, PermissionCode.Create("quality.sample.create")),
            Permission.Create(StandardPermissionIds.QualityResultCreate, PermissionCode.Create("quality.result.create")),
            Permission.Create(StandardPermissionIds.QualityRelease, PermissionCode.Create("quality.release")),
            Permission.Create(StandardPermissionIds.QualityBlock, PermissionCode.Create("quality.block")),
            Permission.Create(StandardPermissionIds.DocumentRead, PermissionCode.Create("document.read")),
            Permission.Create(StandardPermissionIds.DocumentUpload, PermissionCode.Create("document.upload")),
            Permission.Create(StandardPermissionIds.TransportRead, PermissionCode.Create("transport.read")),
            Permission.Create(StandardPermissionIds.TransportCreate, PermissionCode.Create("transport.create")),
            Permission.Create(StandardPermissionIds.DeliveryRead, PermissionCode.Create("delivery.read")),
            Permission.Create(StandardPermissionIds.DeliveryCreate, PermissionCode.Create("delivery.create")),
            Permission.Create(StandardPermissionIds.AuditRead, PermissionCode.Create("audit.read")),
        ];
    }
}
