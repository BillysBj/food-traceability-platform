namespace FoodTraceability.Modules.Identity.Application.Memberships;

public sealed record AddMemberCommand(Guid OrganizationId, Guid UserId);

public sealed record AssignRoleCommand(
    Guid OrganizationId,
    Guid UserId,
    Guid RoleId);

public sealed record MembershipDetails(
    Guid OrganizationId,
    Guid UserId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AssignedRole> Roles);

public sealed record AssignedRole(
    Guid RoleId,
    string RoleCode,
    DateTimeOffset CreatedAt);
