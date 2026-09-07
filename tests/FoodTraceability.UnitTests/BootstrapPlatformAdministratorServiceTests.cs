using FoodTraceability.Modules.Identity.Application.Authentication;
using FoodTraceability.Modules.Identity.Application.Bootstrap;
using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.UnitTests;

public sealed class BootstrapPlatformAdministratorServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidCommandCreatesUserCredentialAndPlatformRoleAssignment()
    {
        var fixture = new BootstrapFixture();

        var result = await fixture.Service.BootstrapAsync(
            CreateCommand(),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.UserId);
        Assert.Equal("admin@example.com", result.Email);
        Assert.Equal(result.UserId, fixture.Writer.User?.Id);
        Assert.Equal("Admin", fixture.Writer.User?.FirstName);
        Assert.Equal("Operator", fixture.Writer.User?.LastName);
        Assert.True(fixture.Writer.User?.IsActive);
        Assert.Equal(result.UserId, fixture.Writer.Credential?.UserId);
        Assert.Equal(FakePasswordHasher.GeneratedHash, fixture.Writer.Credential?.PasswordHash);
        Assert.Equal(result.UserId, fixture.Writer.Assignment?.UserId);
        Assert.Equal(Now, fixture.Writer.User?.CreatedAt);
        Assert.Equal(Now, fixture.Writer.Credential?.CreatedAt);
        Assert.Equal(Now, fixture.Writer.Assignment?.CreatedAt);
    }

    [Fact]
    public async Task AssignmentUsesStandardPlatformAdministratorRole()
    {
        var fixture = new BootstrapFixture();

        await fixture.Service.BootstrapAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal(StandardRoleIds.PlatformAdmin, fixture.Writer.Assignment?.RoleId);
        Assert.Equal(RoleAssignmentScope.Platform, fixture.Writer.Assignment?.AssignmentScope);
    }

    [Fact]
    public async Task ExistingPlatformAdministratorIsRejectedWithoutWriting()
    {
        var fixture = new BootstrapFixture(platformAdministratorExists: true);

        await Assert.ThrowsAsync<PlatformAdministratorAlreadyExistsException>(() =>
            fixture.Service.BootstrapAsync(CreateCommand(), CancellationToken.None));

        Assert.False(fixture.Reader.UserExistsWasCalled);
        Assert.False(fixture.PasswordHasher.WasCalled);
        Assert.False(fixture.Writer.WasCalled);
    }

    [Fact]
    public async Task ExistingEmailAddressIsRejectedWithoutWriting()
    {
        var fixture = new BootstrapFixture(userExists: true);

        await Assert.ThrowsAsync<BootstrapValidationException>(() =>
            fixture.Service.BootstrapAsync(CreateCommand(), CancellationToken.None));

        Assert.Equal("admin@example.com", fixture.Reader.NormalizedEmail);
        Assert.False(fixture.PasswordHasher.WasCalled);
        Assert.False(fixture.Writer.WasCalled);
    }

    [Fact]
    public async Task ShortPasswordIsRejectedBeforeAnyReadOrWrite()
    {
        var fixture = new BootstrapFixture();
        var command = CreateCommand(new string('a', PasswordPolicy.MinimumLength - 1));

        await Assert.ThrowsAsync<BootstrapValidationException>(() =>
            fixture.Service.BootstrapAsync(command, CancellationToken.None));

        Assert.False(fixture.Reader.PlatformAdministratorExistsWasCalled);
        Assert.False(fixture.Reader.UserExistsWasCalled);
        Assert.False(fixture.PasswordHasher.WasCalled);
        Assert.False(fixture.Writer.WasCalled);
    }

    [Fact]
    public async Task PasswordIsPassedToHasherWithoutModification()
    {
        const string password = "  αβγδεζηθικλμ  ";
        var fixture = new BootstrapFixture();

        await fixture.Service.BootstrapAsync(
            CreateCommand(password),
            CancellationToken.None);

        Assert.Equal(password, fixture.PasswordHasher.Password);
        Assert.Equal(
            System.Text.Encoding.UTF8.GetBytes(password),
            System.Text.Encoding.UTF8.GetBytes(fixture.PasswordHasher.Password!));
    }

    [Fact]
    public async Task ResultContainsOnlyUserIdAndEmail()
    {
        var fixture = new BootstrapFixture();

        var result = await fixture.Service.BootstrapAsync(
            CreateCommand(),
            CancellationToken.None);

        var propertyNames = result.GetType()
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["Email", "UserId"], propertyNames);
        Assert.DoesNotContain(
            result.GetType().GetProperties(),
            property => property.Name.Contains("password", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CommandToStringRedactsPasswordAndRetainsDiagnosticFields()
    {
        const string cleartextPassword = "uniquely-recognizable-secret-92741";
        var command = new BootstrapPlatformAdministratorCommand(
            "admin@example.com",
            "Admin",
            "Operator",
            cleartextPassword);

        var result = command.ToString();

        Assert.DoesNotContain(cleartextPassword, result, StringComparison.Ordinal);
        Assert.Contains("admin@example.com", result, StringComparison.Ordinal);
        Assert.Contains("Admin", result, StringComparison.Ordinal);
        Assert.Contains("Operator", result, StringComparison.Ordinal);
    }

    private static BootstrapPlatformAdministratorCommand CreateCommand(
        string password = "valid-password-value")
    {
        return new BootstrapPlatformAdministratorCommand(
            " Admin@Example.COM ",
            " Admin ",
            " Operator ",
            password);
    }

    private sealed class BootstrapFixture
    {
        public BootstrapFixture(
            bool platformAdministratorExists = false,
            bool userExists = false)
        {
            Reader.PlatformAdministratorExists = platformAdministratorExists;
            Reader.UserExists = userExists;
            Service = new BootstrapPlatformAdministratorService(
                Reader,
                Writer,
                PasswordHasher,
                new FixedTimeProvider(Now));
        }

        public FakeBootstrapReader Reader { get; } = new();

        public FakeBootstrapWriter Writer { get; } = new();

        public FakePasswordHasher PasswordHasher { get; } = new();

        public BootstrapPlatformAdministratorService Service { get; }
    }

    private sealed class FakeBootstrapReader : IBootstrapReader
    {
        public bool PlatformAdministratorExists { get; set; }

        public bool UserExists { get; set; }

        public bool PlatformAdministratorExistsWasCalled { get; private set; }

        public bool UserExistsWasCalled { get; private set; }

        public string? NormalizedEmail { get; private set; }

        public Task<bool> PlatformAdministratorExistsAsync(
            CancellationToken cancellationToken)
        {
            PlatformAdministratorExistsWasCalled = true;
            return Task.FromResult(PlatformAdministratorExists);
        }

        public Task<bool> UserExistsAsync(
            string normalizedEmail,
            CancellationToken cancellationToken)
        {
            UserExistsWasCalled = true;
            NormalizedEmail = normalizedEmail;
            return Task.FromResult(UserExists);
        }
    }

    private sealed class FakeBootstrapWriter : IBootstrapWriter
    {
        public bool WasCalled { get; private set; }

        public User? User { get; private set; }

        public UserCredential? Credential { get; private set; }

        public PlatformRoleAssignment? Assignment { get; private set; }

        public Task AddAsync(
            User user,
            UserCredential credential,
            PlatformRoleAssignment assignment,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            User = user;
            Credential = credential;
            Assignment = assignment;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
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
