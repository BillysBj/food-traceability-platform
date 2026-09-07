using System.Net;
using System.Net.Http.Json;
using FoodTraceability.Modules.Identity.Application.Bootstrap;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Identity.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class PlatformAdminBootstrapTests(PostgreSqlContainerFixture database)
{
    private const string ValidPassword = "bootstrap-password-value";

    [Fact]
    public async Task BootstrapCreatesAllRequiredActiveAdministratorRowsAndSecondAttemptChangesNothing()
    {
        await ResetDatabaseAsync();
        await using var provider = CreateBootstrapServiceProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider
            .GetRequiredService<BootstrapPlatformAdministratorService>();
        var command = CreateCommand(ValidPassword);

        var result = await service.BootstrapAsync(command, CancellationToken.None);

        await using (var verificationContext =
            database.CreatePlatformAdminBootstrapIdentityDbContext())
        {
            var user = await verificationContext.Users
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == result.UserId);
            var credential = await verificationContext.UserCredentials
                .AsNoTracking()
                .SingleAsync(candidate => candidate.UserId == result.UserId);
            var assignment = await verificationContext.PlatformRoleAssignments
                .AsNoTracking()
                .SingleAsync(candidate => candidate.UserId == result.UserId);

            Assert.True(user.IsActive);
            Assert.False(string.IsNullOrWhiteSpace(credential.PasswordHash));
            Assert.Equal(StandardRoleIds.PlatformAdmin, assignment.RoleId);
            Assert.Equal("bootstrap-admin@example.com", result.Email);
        }

        var countsBeforeSecondAttempt = await ReadRowCountsAsync();

        await Assert.ThrowsAsync<PlatformAdministratorAlreadyExistsException>(() =>
            service.BootstrapAsync(
                new BootstrapPlatformAdministratorCommand(
                    "second-admin@example.com",
                    "Second",
                    "Administrator",
                    ValidPassword),
                CancellationToken.None));

        Assert.Equal(countsBeforeSecondAttempt, await ReadRowCountsAsync());
    }

    [Fact]
    public async Task BootstrappedAdministratorCanLoginOnlyWithSamePassword()
    {
        await ResetDatabaseAsync();
        var command = CreateCommand(ValidPassword);
        await BootstrapAsync(command);
        await using var factory = CreateApiFactory();
        using var client = factory.CreateClient();

        using var successfulResponse = await LoginAsync(
            client,
            command.Email!,
            ValidPassword,
            factory.RequestCancellationToken);
        var tokens = await successfulResponse.Content.ReadFromJsonAsync<TokenResponse>(
            factory.RequestCancellationToken);
        using var rejectedResponse = await LoginAsync(
            client,
            command.Email!,
            "different-password-value",
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.OK, successfulResponse.StatusCode);
        Assert.NotNull(tokens);
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedResponse.StatusCode);
    }

    [Fact]
    public async Task UnicodePasswordRemainsUnchangedAcrossBootstrapAndLogin()
    {
        const string unicodePassword = "α\u0301βγδεζηθικλμνξ";
        await ResetDatabaseAsync();
        var command = CreateCommand(unicodePassword);
        await BootstrapAsync(command);
        await using var factory = CreateApiFactory();
        using var client = factory.CreateClient();

        using var response = await LoginAsync(
            client,
            command.Email!,
            unicodePassword,
            factory.RequestCancellationToken);

        Assert.Equal(PasswordPolicy.MinimumLength, unicodePassword.Length);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task BootstrapAsync(BootstrapPlatformAdministratorCommand command)
    {
        await using var provider = CreateBootstrapServiceProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider
            .GetRequiredService<BootstrapPlatformAdministratorService>();
        await service.BootstrapAsync(command, CancellationToken.None);
    }

    private ServiceProvider CreateBootstrapServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FoodTraceability"] =
                    database.PlatformAdminBootstrapConnectionString
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddIdentityAuthentication(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private ApiWebApplicationFactory CreateApiFactory()
    {
        return new ApiWebApplicationFactory(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:FoodTraceability"] =
                    database.PlatformAdminBootstrapConnectionString,
                ["RateLimiting:Authentication:PermitLimit"] = "100"
            });
    }

    private async Task ResetDatabaseAsync()
    {
        await using var context = database.CreatePlatformAdminBootstrapIdentityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        await context.RefreshTokens.ExecuteDeleteAsync();
        await context.PlatformRoleAssignments.ExecuteDeleteAsync();
        await context.UserCredentials.ExecuteDeleteAsync();
        await context.Users.ExecuteDeleteAsync();

        await transaction.CommitAsync();
    }

    private async Task<BootstrapRowCounts> ReadRowCountsAsync()
    {
        await using var context = database.CreatePlatformAdminBootstrapIdentityDbContext();
        return new BootstrapRowCounts(
            await context.Users.CountAsync(),
            await context.UserCredentials.CountAsync(),
            await context.PlatformRoleAssignments.CountAsync());
    }

    private static BootstrapPlatformAdministratorCommand CreateCommand(string password)
    {
        return new BootstrapPlatformAdministratorCommand(
            "bootstrap-admin@example.com",
            "Platform",
            "Administrator",
            password);
    }

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        return client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password },
            cancellationToken);
    }

    private sealed record BootstrapRowCounts(long Users, long Credentials, long Assignments);

    private sealed record TokenResponse(
        string AccessToken,
        int ExpiresIn,
        string RefreshToken);
}
