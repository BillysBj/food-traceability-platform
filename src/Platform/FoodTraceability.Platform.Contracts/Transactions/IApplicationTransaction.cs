namespace FoodTraceability.Platform.Contracts.Transactions;

/// <summary>Opens the scope's shared transaction without exposing persistence types.</summary>
public interface IApplicationTransaction
{
    Task<IApplicationTransactionHandle> BeginAsync(CancellationToken cancellationToken);
}

/// <summary>Disposal without commit rolls back all participating writes.</summary>
public interface IApplicationTransactionHandle : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);

    Task RollbackAsync(CancellationToken cancellationToken);
}
