using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.UnitTests;

public sealed class UserTests
{
    private static readonly Guid UserId = Guid.Parse("2f8c9300-4bed-42b7-80e5-89bdba22d10a");
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 1, 8, 30, 0, TimeSpan.Zero);

    public static IEnumerable<object?[]> InvalidNames()
    {
        var tooLongName = new string('a', User.MaximumNameLength + 1);

        yield return [null, "Doe"];
        yield return [string.Empty, "Doe"];
        yield return ["   ", "Doe"];
        yield return [tooLongName, "Doe"];
        yield return ["Jane", null];
        yield return ["Jane", string.Empty];
        yield return ["Jane", "   "];
        yield return ["Jane", tooLongName];
    }

    [Fact]
    public void ValidUserIsCreatedWithExpectedValues()
    {
        var email = EmailAddress.Create("jane.doe@example.com");

        var user = User.Create(UserId, email, "Jane", "Doe", CreatedAt);

        Assert.Equal(UserId, user.Id);
        Assert.Equal(email, user.Email);
        Assert.Equal("Jane", user.FirstName);
        Assert.Equal("Doe", user.LastName);
        Assert.True(user.IsActive);
        Assert.Equal(CreatedAt, user.CreatedAt);
        Assert.Equal(CreatedAt, user.UpdatedAt);
    }

    [Fact]
    public void NamesAreTrimmed()
    {
        var user = User.Create(
            UserId,
            EmailAddress.Create("jane.doe@example.com"),
            "  Jane  ",
            "  Doe  ",
            CreatedAt);

        Assert.Equal("Jane", user.FirstName);
        Assert.Equal("Doe", user.LastName);
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void InvalidNameIsRejected(string? firstName, string? lastName)
    {
        Assert.Throws<IdentityDomainException>(() => User.Create(
            UserId,
            EmailAddress.Create("jane.doe@example.com"),
            firstName,
            lastName,
            CreatedAt));
    }

    [Fact]
    public void EmptyIdIsRejected()
    {
        Assert.Throws<IdentityDomainException>(() => User.Create(
            Guid.Empty,
            EmailAddress.Create("jane.doe@example.com"),
            "Jane",
            "Doe",
            CreatedAt));
    }

    [Fact]
    public void NewUserIsActiveAndTimestampsMatch()
    {
        var user = CreateUser();

        Assert.True(user.IsActive);
        Assert.Equal(user.CreatedAt, user.UpdatedAt);
    }

    [Fact]
    public void DeactivateSetsInactiveAndUpdatesTimestamp()
    {
        var user = CreateUser();
        var deactivatedAt = CreatedAt.AddHours(1);

        user.Deactivate(deactivatedAt);

        Assert.False(user.IsActive);
        Assert.Equal(deactivatedAt, user.UpdatedAt);
    }

    [Fact]
    public void ActivateSetsActiveAndUpdatesTimestamp()
    {
        var user = CreateUser();
        user.Deactivate(CreatedAt.AddHours(1));
        var activatedAt = CreatedAt.AddHours(2);

        user.Activate(activatedAt);

        Assert.True(user.IsActive);
        Assert.Equal(activatedAt, user.UpdatedAt);
    }

    [Fact]
    public void RepeatedDeactivateDoesNotChangeUpdatedAt()
    {
        var user = CreateUser();
        var firstDeactivationAt = CreatedAt.AddHours(1);
        user.Deactivate(firstDeactivationAt);

        user.Deactivate(CreatedAt.AddHours(2));

        Assert.False(user.IsActive);
        Assert.Equal(firstDeactivationAt, user.UpdatedAt);
    }

    [Fact]
    public void RepeatedActivateDoesNotChangeUpdatedAt()
    {
        var user = CreateUser();
        user.Deactivate(CreatedAt.AddHours(1));
        var firstActivationAt = CreatedAt.AddHours(2);
        user.Activate(firstActivationAt);

        user.Activate(CreatedAt.AddHours(3));

        Assert.True(user.IsActive);
        Assert.Equal(firstActivationAt, user.UpdatedAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OccurredAtBeforeCreatedAtIsRejected(bool deactivate)
    {
        var user = CreateUser();
        var occurredAt = CreatedAt.AddTicks(-1);

        void ChangeState()
        {
            if (deactivate)
            {
                user.Deactivate(occurredAt);
                return;
            }

            user.Activate(occurredAt);
        }

        Assert.Throws<IdentityDomainException>(ChangeState);
    }

    private static User CreateUser()
    {
        return User.Create(
            UserId,
            EmailAddress.Create("jane.doe@example.com"),
            "Jane",
            "Doe",
            CreatedAt);
    }
}
