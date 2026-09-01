using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.UnitTests;

public sealed class UserCredentialTests
{
    private static readonly Guid UserId = Guid.Parse("519d954a-8227-4712-83d7-4edac02db28b");
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 1, 16, 0, 0, TimeSpan.Zero);

    public static IEnumerable<object?[]> InvalidPasswordHashes()
    {
        yield return [null];
        yield return [string.Empty];
        yield return ["   "];
    }

    [Fact]
    public void ValidCredentialIsCreatedWithExpectedValues()
    {
        var updatedAt = CreatedAt.AddMinutes(1);

        var credential = UserCredential.Create(UserId, "password-hash", CreatedAt, updatedAt);

        Assert.Equal(UserId, credential.UserId);
        Assert.Equal("password-hash", credential.PasswordHash);
        Assert.Equal(CreatedAt, credential.CreatedAt);
        Assert.Equal(updatedAt, credential.UpdatedAt);
    }

    [Fact]
    public void EmptyUserIdIsRejected()
    {
        Assert.Throws<IdentityDomainException>(() => UserCredential.Create(
            Guid.Empty,
            "password-hash",
            CreatedAt,
            CreatedAt));
    }

    [Theory]
    [MemberData(nameof(InvalidPasswordHashes))]
    public void InvalidPasswordHashIsRejected(string? passwordHash)
    {
        Assert.Throws<IdentityDomainException>(() => UserCredential.Create(
            UserId,
            passwordHash,
            CreatedAt,
            CreatedAt));
    }

    [Fact]
    public void UpdatedAtBeforeCreatedAtIsRejected()
    {
        Assert.Throws<IdentityDomainException>(() => UserCredential.Create(
            UserId,
            "password-hash",
            CreatedAt,
            CreatedAt.AddTicks(-1)));
    }

    [Fact]
    public void ChangePasswordHashUpdatesHashAndTimestamp()
    {
        var credential = CreateCredential();
        var updatedAt = CreatedAt.AddHours(1);

        credential.ChangePasswordHash("changed-password-hash", updatedAt);

        Assert.Equal("changed-password-hash", credential.PasswordHash);
        Assert.Equal(updatedAt, credential.UpdatedAt);
    }

    [Theory]
    [MemberData(nameof(InvalidPasswordHashes))]
    public void ChangeToInvalidPasswordHashIsRejected(string? passwordHash)
    {
        var credential = CreateCredential();

        Assert.Throws<IdentityDomainException>(() =>
            credential.ChangePasswordHash(passwordHash, CreatedAt.AddHours(1)));
    }

    [Fact]
    public void ChangeBeforeCreatedAtIsRejected()
    {
        var credential = CreateCredential();

        Assert.Throws<IdentityDomainException>(() =>
            credential.ChangePasswordHash("changed-password-hash", CreatedAt.AddTicks(-1)));
    }

    private static UserCredential CreateCredential()
    {
        return UserCredential.Create(UserId, "password-hash", CreatedAt, CreatedAt);
    }
}
