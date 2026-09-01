namespace FoodTraceability.Modules.Identity.Domain;

public sealed class Role
{
    public const int MaximumNameLength = 100;
    public const int MaximumDescriptionLength = 500;

    private Role(
        Guid id,
        RoleCode code,
        RoleAssignmentScope assignmentScope,
        string name,
        string? description)
    {
        Id = id;
        Code = code;
        AssignmentScope = assignmentScope;
        Name = name;
        Description = description;
    }

    public Guid Id { get; }

    // This stable, language-neutral code is the future authorization key. Name is display text only.
    public RoleCode Code { get; }

    public RoleAssignmentScope AssignmentScope { get; }

    public string Name { get; }

    public string? Description { get; }

    public static Role Create(
        Guid id,
        RoleCode? code,
        RoleAssignmentScope? assignmentScope,
        string? name,
        string? description = null)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("Role id must not be empty.");
        }

        if (code is null)
        {
            throw new IdentityDomainException("Role code must be provided.");
        }

        if (assignmentScope is not RoleAssignmentScope.Platform
            and not RoleAssignmentScope.Organization)
        {
            throw new IdentityDomainException("Role assignment scope must be provided and valid.");
        }

        return new Role(
            id,
            code,
            assignmentScope.Value,
            NormalizeName(name),
            NormalizeDescription(description));
    }

    private static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new IdentityDomainException(
                "Role name must not be null, empty, or consist only of whitespace.");
        }

        var normalizedName = name.Trim();
        if (normalizedName.Length > MaximumNameLength)
        {
            throw new IdentityDomainException(
                $"Role name must not exceed {MaximumNameLength} characters.");
        }

        return normalizedName;
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
                $"Role description must not exceed {MaximumDescriptionLength} characters.");
        }

        return normalizedDescription;
    }
}

public static class StandardRoleIds
{
    // UUIDv5 values derived once from the DNS namespace and
    // food-traceability.identity.role.<ROLE_CODE>, then retained as source literals.
    public static readonly Guid PlatformAdmin = Guid.Parse("ec72b8b5-2610-5efd-aa7f-6aa59889da7d");
    public static readonly Guid OrganizationAdmin = Guid.Parse("00ec29aa-1bc7-540e-b04d-02c3497f50b3");
    public static readonly Guid Producer = Guid.Parse("d8c0f985-5ce5-59b9-bf3b-71ae6bc5616a");
    public static readonly Guid Processor = Guid.Parse("351aaedc-26d7-5406-9109-f4f8139ec1a8");
    public static readonly Guid QualityManager = Guid.Parse("38623612-6286-55ce-9af2-ea523f760be3");
    public static readonly Guid Laboratory = Guid.Parse("2ef0f055-298c-512e-a35e-10d180133f51");
    public static readonly Guid Bottler = Guid.Parse("34644a1b-9bbb-5005-98a1-b3584dd8bf69");
    public static readonly Guid Logistics = Guid.Parse("222d9d3e-a711-5607-820a-59c9f497bbaf");
    public static readonly Guid Retailer = Guid.Parse("0002868f-4330-5c7c-aac4-77420d2aff52");
    public static readonly Guid Auditor = Guid.Parse("a187d055-fa04-56ef-b488-2b6ad6216007");
}
