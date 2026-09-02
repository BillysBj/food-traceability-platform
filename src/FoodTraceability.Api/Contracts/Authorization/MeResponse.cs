namespace FoodTraceability.Api.Contracts.Authorization;

public sealed record MeResponse(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<string> PlatformPermissions,
    IReadOnlyList<OrganizationPermissionsResponse> OrganizationPermissions,
    IReadOnlyList<LocationPermissionsResponse> LocationPermissions);

public sealed record OrganizationPermissionsResponse(
    Guid OrganizationId,
    IReadOnlyList<string> Permissions);

public sealed record LocationPermissionsResponse(
    Guid OrganizationId,
    Guid LocationId,
    IReadOnlyList<string> Permissions);
