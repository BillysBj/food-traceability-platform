namespace FoodTraceability.Modules.Identity.Domain;

public sealed class RefreshToken
{
    private RefreshToken(
        Guid id,
        Guid userId,
        Guid sessionId,
        string tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        UserId = userId;
        SessionId = sessionId;
        TokenHash = tokenHash;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public Guid SessionId { get; }

    public string TokenHash { get; }

    public DateTimeOffset IssuedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public static RefreshToken Create(
        Guid id,
        Guid userId,
        Guid sessionId,
        string? tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("Refresh token id must not be empty.");
        }

        if (userId == Guid.Empty)
        {
            throw new IdentityDomainException("Refresh token user id must not be empty.");
        }

        if (sessionId == Guid.Empty)
        {
            throw new IdentityDomainException("Refresh token session id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new IdentityDomainException(
                "Refresh token hash must not be null, empty, or consist only of whitespace.");
        }

        if (expiresAt <= issuedAt)
        {
            throw new IdentityDomainException(
                "Refresh token expiration must be after its issue time.");
        }

        return new RefreshToken(id, userId, sessionId, tokenHash, issuedAt, expiresAt);
    }

    public void Revoke(DateTimeOffset occurredAt)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        if (occurredAt < IssuedAt)
        {
            throw new IdentityDomainException(
                "Refresh token revocation must not occur before its issue time.");
        }

        RevokedAt = occurredAt;
    }

    public bool IsActive(DateTimeOffset now)
    {
        return RevokedAt is null && now >= IssuedAt && now < ExpiresAt;
    }
}
