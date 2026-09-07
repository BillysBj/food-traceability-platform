using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.Modules.Identity.Application.Bootstrap;

public interface IBootstrapReader
{
    Task<bool> PlatformAdministratorExistsAsync(CancellationToken cancellationToken);

    Task<bool> UserExistsAsync(string normalizedEmail, CancellationToken cancellationToken);
}

public interface IBootstrapWriter
{
    Task AddAsync(
        User user,
        UserCredential credential,
        PlatformRoleAssignment assignment,
        CancellationToken cancellationToken);
}
