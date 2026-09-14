using FoodTraceability.Platform.Contracts.Transactions;

namespace FoodTraceability.Platform.Persistence;

internal sealed class ApplicationTransaction(ScopedTransaction transaction) : IApplicationTransaction
{
    public async Task<IApplicationTransactionHandle> BeginAsync(CancellationToken cancellationToken) =>
        new Handle(await transaction.BeginAsync(cancellationToken));

    private sealed class Handle(ScopedTransaction.Handle handle) : IApplicationTransactionHandle
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            handle.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken) =>
            handle.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => handle.DisposeAsync();
    }
}
