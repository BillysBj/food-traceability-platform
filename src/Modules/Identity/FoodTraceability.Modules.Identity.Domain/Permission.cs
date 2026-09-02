namespace FoodTraceability.Modules.Identity.Domain;

public sealed class Permission
{
    public const int MaximumDescriptionLength = 500;

    private Permission(Guid id, PermissionCode code, string? description)
    {
        Id = id;
        Code = code;
        Description = description;
    }

    public Guid Id { get; }

    // This stable, language-neutral code is the sole future authorization key.
    // Description is optional display text and must never be used for authorization.
    public PermissionCode Code { get; }

    public string? Description { get; }

    public static Permission Create(Guid id, PermissionCode? code, string? description = null)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("Permission id must not be empty.");
        }

        if (code is null)
        {
            throw new IdentityDomainException("Permission code must be provided.");
        }

        return new Permission(id, code, NormalizeDescription(description));
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var normalizedDescription = description.Trim();
        if (normalizedDescription.Length > MaximumDescriptionLength)
        {
            throw new IdentityDomainException(
                $"Permission description must not exceed {MaximumDescriptionLength} characters.");
        }

        return normalizedDescription;
    }
}

public static class StandardPermissionIds
{
    // UUIDv5 values derived once from the DNS namespace and
    // food-traceability.identity.permission.<normalized-code>, then retained as source literals.
    public static readonly Guid OrganizationRead = Guid.Parse("5239d89e-16e3-586b-9996-32085b6b867d");
    public static readonly Guid OrganizationManage = Guid.Parse("b9c07ec4-7da3-570b-b279-57f758496798");
    public static readonly Guid UserRead = Guid.Parse("542cd3e3-27c8-5a3d-b634-10cff370a922");
    public static readonly Guid UserManage = Guid.Parse("227a9dd3-048c-51e8-aae9-c0268a185640");
    public static readonly Guid RoleRead = Guid.Parse("51d975f4-e516-525c-a24b-b94fd8cfdea1");
    public static readonly Guid PermissionRead = Guid.Parse("5362b9ec-0f34-51f9-9423-ec9940ec8e22");
    public static readonly Guid ProductRead = Guid.Parse("13fae5e7-0a2d-5b72-9401-3c9e398b2b55");
    public static readonly Guid ProductCreate = Guid.Parse("becb86f7-1a71-5ccb-b0ab-3aade89e6177");
    public static readonly Guid ProductUpdate = Guid.Parse("06de594f-5714-5198-b877-5b84fdb8a1bc");
    public static readonly Guid ArticleRead = Guid.Parse("f57cf66c-3591-54bd-a12e-5f53f141c48e");
    public static readonly Guid ArticleCreate = Guid.Parse("b2d81829-2d1c-5e6a-9b19-7d275f3aa0cf");
    public static readonly Guid ArticleUpdate = Guid.Parse("953dda24-0a71-57bc-b06c-dfc895a1fae2");
    public static readonly Guid LotRead = Guid.Parse("8d153853-ab36-5e34-8537-2e05556feeee");
    public static readonly Guid LotCreate = Guid.Parse("da54dd5a-9013-5c1b-8b4e-fdc30258e02f");
    public static readonly Guid LotUpdate = Guid.Parse("a131fb8f-4ffb-510e-8aa3-2d7fa78c3383");
    public static readonly Guid TraceRead = Guid.Parse("1ed56bb0-2b7b-5c76-a63a-5072027ee5bf");
    public static readonly Guid TraceEventCreate = Guid.Parse("f999fb2f-ee88-5dd9-a4ec-9170587f7d9b");
    public static readonly Guid QualityRead = Guid.Parse("d8f29f61-9bbb-5659-8b88-559242e75918");
    public static readonly Guid QualitySampleCreate = Guid.Parse("123005cc-2642-5a72-b2d8-f3f5d989bd32");
    public static readonly Guid QualityResultCreate = Guid.Parse("bad85ec4-523c-5505-b4aa-933cd062add9");
    public static readonly Guid QualityRelease = Guid.Parse("87bbc734-d0ce-544b-ad62-ddefb7f56a80");
    public static readonly Guid QualityBlock = Guid.Parse("e577d2b4-c89d-58db-a0f7-5be7f040734a");
    public static readonly Guid DocumentRead = Guid.Parse("ad1e5f46-77ad-5029-85b1-5048d78c6f8d");
    public static readonly Guid DocumentUpload = Guid.Parse("78562e51-4c55-5309-94b3-c96ef465ad9b");
    public static readonly Guid TransportRead = Guid.Parse("e6708b10-357c-5285-a292-69deae973983");
    public static readonly Guid TransportCreate = Guid.Parse("b0f144ee-b4da-5e04-9d5f-253a24922db2");
    public static readonly Guid DeliveryRead = Guid.Parse("27c75590-f8fe-5fbe-afc4-190fcca52776");
    public static readonly Guid DeliveryCreate = Guid.Parse("d8f57584-4487-5fa8-8a59-5e7effd62f06");
    public static readonly Guid AuditRead = Guid.Parse("573fe7e9-c8f3-5b46-a885-3f2ae13ca228");
}
