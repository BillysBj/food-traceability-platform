using FoodTraceability.Modules.Identity.Application.Authentication;
using FoodTraceability.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Identity.Infrastructure.Authentication;

internal sealed class AuthenticationSessionStore(IdentityDbContext dbContext)
    : IAuthenticationSessionStore
{
    public async Task<LoginAccount?> FindLoginAccountAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        EmailAddress emailAddress;
        try
        {
            emailAddress = EmailAddress.Create(normalizedEmail);
        }
        catch (IdentityDomainException)
        {
            return null;
        }

        return await (
            from user in dbContext.Users.AsNoTracking()
            join credential in dbContext.UserCredentials.AsNoTracking()
                on user.Id equals credential.UserId into credentials
            from credential in credentials.DefaultIfEmpty()
            where user.Email == emailAddress
            select new LoginAccount(
                user.Id,
                user.IsActive,
                credential == null ? null : credential.PasswordHash))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task CreateSessionAsync(
        Guid userId,
        Guid sessionId,
        NewRefreshToken refreshToken,
        CancellationToken cancellationToken)
    {
        dbContext.RefreshTokens.Add(CreateDomainToken(userId, sessionId, refreshToken));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RefreshRotationResult> RotateRefreshTokenAsync(
        string currentTokenHash,
        NewRefreshToken replacementToken,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockTokenRowAsync(currentTokenHash, cancellationToken);

        var currentToken = await dbContext.RefreshTokens.SingleOrDefaultAsync(
            token => token.TokenHash == currentTokenHash,
            cancellationToken);

        if (currentToken is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return RefreshRotationResult.Failed(RefreshRotationStatus.NotFound);
        }

        if (currentToken.RevokedAt is not null)
        {
            await RevokeActiveSessionTokensAsync(
                currentToken.SessionId,
                now,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return RefreshRotationResult.Failed(RefreshRotationStatus.Revoked);
        }

        if (now < currentToken.IssuedAt || now >= currentToken.ExpiresAt)
        {
            await transaction.CommitAsync(cancellationToken);
            return RefreshRotationResult.Failed(RefreshRotationStatus.Expired);
        }

        var userIsActive = await dbContext.Users
            .Where(user => user.Id == currentToken.UserId)
            .Select(user => user.IsActive)
            .SingleAsync(cancellationToken);
        if (!userIsActive)
        {
            await transaction.CommitAsync(cancellationToken);
            return RefreshRotationResult.Failed(RefreshRotationStatus.UserInactive);
        }

        currentToken.Revoke(now);
        dbContext.RefreshTokens.Add(CreateDomainToken(
            currentToken.UserId,
            currentToken.SessionId,
            replacementToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return RefreshRotationResult.Succeeded(currentToken.UserId);
    }

    public async Task RevokeSessionAsync(
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockTokenRowAsync(tokenHash, cancellationToken);

        var sessionId = await dbContext.RefreshTokens
            .Where(token => token.TokenHash == tokenHash)
            .Select(token => (Guid?)token.SessionId)
            .SingleOrDefaultAsync(cancellationToken);
        if (sessionId is not null)
        {
            await RevokeActiveSessionTokensAsync(sessionId.Value, now, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private Task<int> LockTokenRowAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM identity.refresh_token WHERE token_hash = {tokenHash} FOR UPDATE",
            cancellationToken);
    }

    private Task<int> RevokeActiveSessionTokensAsync(
        Guid sessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return dbContext.RefreshTokens
            .Where(token => token.SessionId == sessionId && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    token => token.RevokedAt,
                    (DateTimeOffset?)now),
                cancellationToken);
    }

    private static RefreshToken CreateDomainToken(
        Guid userId,
        Guid sessionId,
        NewRefreshToken refreshToken)
    {
        return RefreshToken.Create(
            refreshToken.Id,
            userId,
            sessionId,
            refreshToken.Hash,
            refreshToken.IssuedAt,
            refreshToken.ExpiresAt);
    }
}
