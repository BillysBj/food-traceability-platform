using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.Modules.Identity.Infrastructure.Configurations;

/// <summary>
/// The approved Pilot 1 role-permission matrix from D-20.
/// </summary>
/// <remarks>
/// These assignments define capabilities only. Tenant and organization scope, entity access,
/// and business state rules must be enforced separately. A permission alone must never enable
/// access across organizations.
/// </remarks>
internal static class ApprovedRolePermissionMatrix
{
    public static RolePermission[] CreateAssignments()
    {
        return
        [
            // PLATFORM_ADMIN
            Assign(StandardRoleIds.PlatformAdmin, StandardPermissionIds.OrganizationRead),
            Assign(StandardRoleIds.PlatformAdmin, StandardPermissionIds.OrganizationManage),
            Assign(StandardRoleIds.PlatformAdmin, StandardPermissionIds.UserRead),
            Assign(StandardRoleIds.PlatformAdmin, StandardPermissionIds.UserManage),
            Assign(StandardRoleIds.PlatformAdmin, StandardPermissionIds.RoleRead),
            Assign(StandardRoleIds.PlatformAdmin, StandardPermissionIds.PermissionRead),
            Assign(StandardRoleIds.PlatformAdmin, StandardPermissionIds.ProductRead),
            Assign(StandardRoleIds.PlatformAdmin, StandardPermissionIds.ProductCreate),
            Assign(StandardRoleIds.PlatformAdmin, StandardPermissionIds.ProductUpdate),

            // ORGANIZATION_ADMIN
            Assign(StandardRoleIds.OrganizationAdmin, StandardPermissionIds.OrganizationRead),
            Assign(StandardRoleIds.OrganizationAdmin, StandardPermissionIds.OrganizationManage),
            Assign(StandardRoleIds.OrganizationAdmin, StandardPermissionIds.UserRead),
            Assign(StandardRoleIds.OrganizationAdmin, StandardPermissionIds.UserManage),
            Assign(StandardRoleIds.OrganizationAdmin, StandardPermissionIds.RoleRead),
            Assign(StandardRoleIds.OrganizationAdmin, StandardPermissionIds.ArticleRead),

            // PRODUCER
            Assign(StandardRoleIds.Producer, StandardPermissionIds.ProductRead),
            Assign(StandardRoleIds.Producer, StandardPermissionIds.ArticleRead),
            Assign(StandardRoleIds.Producer, StandardPermissionIds.ArticleCreate),
            Assign(StandardRoleIds.Producer, StandardPermissionIds.ArticleUpdate),
            Assign(StandardRoleIds.Producer, StandardPermissionIds.LotRead),
            Assign(StandardRoleIds.Producer, StandardPermissionIds.LotCreate),
            Assign(StandardRoleIds.Producer, StandardPermissionIds.LotUpdate),
            Assign(StandardRoleIds.Producer, StandardPermissionIds.TraceRead),
            Assign(StandardRoleIds.Producer, StandardPermissionIds.TraceEventCreate),
            Assign(StandardRoleIds.Producer, StandardPermissionIds.DocumentRead),
            Assign(StandardRoleIds.Producer, StandardPermissionIds.DocumentUpload),

            // PROCESSOR
            Assign(StandardRoleIds.Processor, StandardPermissionIds.ProductRead),
            Assign(StandardRoleIds.Processor, StandardPermissionIds.ArticleRead),
            Assign(StandardRoleIds.Processor, StandardPermissionIds.ArticleCreate),
            Assign(StandardRoleIds.Processor, StandardPermissionIds.ArticleUpdate),
            Assign(StandardRoleIds.Processor, StandardPermissionIds.LotRead),
            Assign(StandardRoleIds.Processor, StandardPermissionIds.LotCreate),
            Assign(StandardRoleIds.Processor, StandardPermissionIds.LotUpdate),
            Assign(StandardRoleIds.Processor, StandardPermissionIds.TraceRead),
            Assign(StandardRoleIds.Processor, StandardPermissionIds.TraceEventCreate),
            Assign(StandardRoleIds.Processor, StandardPermissionIds.DocumentRead),
            Assign(StandardRoleIds.Processor, StandardPermissionIds.DocumentUpload),

            // BOTTLER
            Assign(StandardRoleIds.Bottler, StandardPermissionIds.ProductRead),
            Assign(StandardRoleIds.Bottler, StandardPermissionIds.ArticleRead),
            Assign(StandardRoleIds.Bottler, StandardPermissionIds.ArticleCreate),
            Assign(StandardRoleIds.Bottler, StandardPermissionIds.ArticleUpdate),
            Assign(StandardRoleIds.Bottler, StandardPermissionIds.LotRead),
            Assign(StandardRoleIds.Bottler, StandardPermissionIds.LotCreate),
            Assign(StandardRoleIds.Bottler, StandardPermissionIds.LotUpdate),
            Assign(StandardRoleIds.Bottler, StandardPermissionIds.TraceRead),
            Assign(StandardRoleIds.Bottler, StandardPermissionIds.TraceEventCreate),
            Assign(StandardRoleIds.Bottler, StandardPermissionIds.DocumentRead),
            Assign(StandardRoleIds.Bottler, StandardPermissionIds.DocumentUpload),

            // QUALITY_MANAGER
            Assign(StandardRoleIds.QualityManager, StandardPermissionIds.LotRead),
            Assign(StandardRoleIds.QualityManager, StandardPermissionIds.TraceRead),
            Assign(StandardRoleIds.QualityManager, StandardPermissionIds.QualityRead),
            Assign(StandardRoleIds.QualityManager, StandardPermissionIds.QualitySampleCreate),
            Assign(StandardRoleIds.QualityManager, StandardPermissionIds.QualityRelease),
            Assign(StandardRoleIds.QualityManager, StandardPermissionIds.QualityBlock),
            Assign(StandardRoleIds.QualityManager, StandardPermissionIds.DocumentRead),
            Assign(StandardRoleIds.QualityManager, StandardPermissionIds.DocumentUpload),

            // LABORATORY
            Assign(StandardRoleIds.Laboratory, StandardPermissionIds.LotRead),
            Assign(StandardRoleIds.Laboratory, StandardPermissionIds.QualityRead),
            Assign(StandardRoleIds.Laboratory, StandardPermissionIds.QualityResultCreate),
            Assign(StandardRoleIds.Laboratory, StandardPermissionIds.DocumentRead),
            Assign(StandardRoleIds.Laboratory, StandardPermissionIds.DocumentUpload),

            // LOGISTICS
            Assign(StandardRoleIds.Logistics, StandardPermissionIds.LotRead),
            Assign(StandardRoleIds.Logistics, StandardPermissionIds.TraceRead),
            Assign(StandardRoleIds.Logistics, StandardPermissionIds.TransportRead),
            Assign(StandardRoleIds.Logistics, StandardPermissionIds.TransportCreate),
            Assign(StandardRoleIds.Logistics, StandardPermissionIds.DeliveryRead),
            Assign(StandardRoleIds.Logistics, StandardPermissionIds.DeliveryCreate),
            Assign(StandardRoleIds.Logistics, StandardPermissionIds.DocumentRead),
            Assign(StandardRoleIds.Logistics, StandardPermissionIds.DocumentUpload),

            // RETAILER
            Assign(StandardRoleIds.Retailer, StandardPermissionIds.LotRead),
            Assign(StandardRoleIds.Retailer, StandardPermissionIds.TraceRead),
            Assign(StandardRoleIds.Retailer, StandardPermissionIds.DeliveryRead),
            Assign(StandardRoleIds.Retailer, StandardPermissionIds.DocumentRead),

            // AUDITOR
            Assign(StandardRoleIds.Auditor, StandardPermissionIds.LotRead),
            Assign(StandardRoleIds.Auditor, StandardPermissionIds.TraceRead),
            Assign(StandardRoleIds.Auditor, StandardPermissionIds.QualityRead),
            Assign(StandardRoleIds.Auditor, StandardPermissionIds.DocumentRead),
            Assign(StandardRoleIds.Auditor, StandardPermissionIds.AuditRead),
        ];
    }

    private static RolePermission Assign(Guid roleId, Guid permissionId)
    {
        return RolePermission.Create(roleId, permissionId);
    }
}
