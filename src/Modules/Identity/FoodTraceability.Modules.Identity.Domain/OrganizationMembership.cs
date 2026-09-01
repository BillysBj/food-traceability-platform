namespace FoodTraceability.Modules.Identity.Domain;

public sealed class OrganizationMembership
{
    private OrganizationMembership(
        Guid userId,
        Guid organizationId,
        DateTimeOffset createdAt)
    {
        UserId = userId;
        OrganizationId = organizationId;
        CreatedAt = createdAt;
    }

    public Guid UserId { get; }

    public Guid OrganizationId { get; }

    public DateTimeOffset CreatedAt { get; }

    public static OrganizationMembership Create(
        Guid userId,
        Guid organizationId,
        DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new IdentityDomainException("Membership user id must not be empty.");
        }

        if (organizationId == Guid.Empty)
        {
            throw new IdentityDomainException("Membership organization id must not be empty.");
        }

        return new OrganizationMembership(userId, organizationId, createdAt);
    }
}
