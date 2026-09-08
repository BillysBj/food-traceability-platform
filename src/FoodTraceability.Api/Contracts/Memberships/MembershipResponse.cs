namespace FoodTraceability.Api.Contracts.Memberships;

public sealed record MembershipResponse(
    Guid OrganizationId,
    Guid UserId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AssignedRoleResponse> Roles);

public sealed record AssignedRoleResponse(
    Guid RoleId,
    string RoleCode,
    DateTimeOffset CreatedAt);
