using FoodTraceability.Modules.Identity.Application.Authentication;
using FoodTraceability.Modules.Identity.Application.Users;
using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.UnitTests;

public sealed class CreateUserServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 8, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidCommandReturnsDetailsWithNormalizedEmailAddress()
    {
        var fixture = new UserFixture();

        var result = await fixture.Service.CreateAsync(
            CreateCommand(),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("new.user@example.com", result.Email);
        Assert.Equal("New", result.FirstName);
        Assert.Equal("User", result.LastName);
        Assert.True(result.IsActive);
        Assert.Equal(Now, result.CreatedAt);
        Assert.Equal(result.Id, fixture.Writer.User?.Id);
        Assert.Equal(result.Id, fixture.Writer.Credential?.UserId);
    }

    [Fact]
    public async Task PasswordIsPassedToHasherWithoutModification()
    {
        const string password = "  αβγδεζηθικλμ  ";
        var fixture = new UserFixture();

        await fixture.Service.CreateAsync(
            CreateCommand(password),
            CancellationToken.None);

        Assert.Equal(password, fixture.PasswordHasher.Password);
    }

    [Fact]
    public async Task ShortPasswordIsRejectedWithoutCallingWriter()
    {
        var fixture = new UserFixture();

        await Assert.ThrowsAsync<UserValidationException>(() =>
            fixture.Service.CreateAsync(
                CreateCommand(new string('a', PasswordPolicy.MinimumLength - 1)),
                CancellationToken.None));

        Assert.False(fixture.PasswordHasher.WasCalled);
        Assert.Equal(0, fixture.Writer.CallCount);
    }

    [Fact]
    public async Task InvalidEmailAddressIsRejectedWithoutCallingWriter()
    {
        var fixture = new UserFixture();

        await Assert.ThrowsAsync<UserValidationException>(() =>
            fixture.Service.CreateAsync(
                CreateCommand(email: "not-an-email-address"),
                CancellationToken.None));

        Assert.False(fixture.PasswordHasher.WasCalled);
        Assert.Equal(0, fixture.Writer.CallCount);
    }

    [Fact]
    public async Task UserDetailsContainsNeitherPasswordNorHash()
    {
        var fixture = new UserFixture();

        var result = await fixture.Service.CreateAsync(
            CreateCommand(),
            CancellationToken.None);

        var propertyNames = result.GetType()
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            ["CreatedAt", "Email", "FirstName", "Id", "IsActive", "LastName"],
            propertyNames);
        Assert.DoesNotContain(
            propertyNames,
            name => name.Contains("password", StringComparison.OrdinalIgnoreCase)
                || name.Contains("hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CommandToStringRedactsPasswordAndRetainsDiagnosticFields()
    {
        const string password = "uniquely-recognizable-initial-password";
        var command = new CreateUserCommand(
            "new.user@example.com",
            "New",
            "User",
            password);

        var result = command.ToString();

        Assert.DoesNotContain(password, result, StringComparison.Ordinal);
        Assert.Contains("new.user@example.com", result, StringComparison.Ordinal);
        Assert.Contains("New", result, StringComparison.Ordinal);
        Assert.Contains("User", result, StringComparison.Ordinal);
        Assert.Contains("Password = ***", result, StringComparison.Ordinal);
    }

    private static CreateUserCommand CreateCommand(
        string password = "valid-initial-password",
        string? email = " New.User@Example.COM ")
    {
        return new CreateUserCommand(
            email,
            " New ",
            " User ",
            password);
    }

    private sealed class UserFixture
    {
        public UserFixture()
        {
            Service = new CreateUserService(
                Writer,
                PasswordHasher,
                new FixedTimeProvider(Now));
        }

        public CapturingUserWriter Writer { get; } = new();

        public CapturingPasswordHasher PasswordHasher { get; } = new();

        public CreateUserService Service { get; }
    }

    private sealed class CapturingUserWriter : IUserWriter
    {
        public int CallCount { get; private set; }

        public User? User { get; private set; }

        public UserCredential? Credential { get; private set; }

        public Task AddAsync(
            User user,
            UserCredential credential,
            CancellationToken cancellationToken)
        {
            CallCount++;
            User = user;
            Credential = credential;
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingPasswordHasher : IPasswordHasher
    {
        public const string GeneratedHash = "generated-password-hash";

        public bool WasCalled { get; private set; }

        public string? Password { get; private set; }

        public string Hash(string password)
        {
            WasCalled = true;
            Password = password;
            return GeneratedHash;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

public sealed class UserQueryServiceTests
{
    [Fact]
    public async Task EmptyUserIdReturnsNullWithoutCallingReader()
    {
        var reader = new StubUserReader();
        var service = new UserQueryService(reader);

        var result = await service.FindByIdAsync(Guid.Empty, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, reader.CallCount);
    }

    private sealed class StubUserReader : IUserReader
    {
        public int CallCount { get; private set; }

        public Task<UserDetails?> FindByIdAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult<UserDetails?>(null);
        }
    }
}
