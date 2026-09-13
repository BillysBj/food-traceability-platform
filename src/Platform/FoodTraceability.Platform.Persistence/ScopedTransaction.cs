using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace FoodTraceability.Platform.Persistence;

/// <summary>
/// Holds one explicitly opened transaction on the scope's shared connection.
/// Contexts enlist individually; only the opener receives the completion handle.
/// Database operations within a scope must remain sequential (D-43).
/// </summary>
public sealed class ScopedTransaction(NpgsqlConnection connection) : IDisposable, IAsyncDisposable
{
    private Handle? _current;
    private bool _starting;
    private bool _disposed;

    public bool IsActive => _current is not null || _starting;

    public async Task<Handle> BeginAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsActive)
        {
            throw new InvalidOperationException(
                "A transaction is already active in this DI scope; nested transactions are not supported.");
        }

        _starting = true;
        try
        {
            if (connection.State == ConnectionState.Closed)
            {
                await connection.OpenAsync(cancellationToken);
            }

            _current = new Handle(this, await connection.BeginTransactionAsync(cancellationToken));
            return _current;
        }
        finally
        {
            _starting = false;
        }
    }

    /// <summary>
    /// Validates that the context belongs to this connection, then enlists it if
    /// a transaction is running. Otherwise no database action is performed.
    /// </summary>
    public async Task EnlistAsync(DbContext context, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(context);
        if (!ReferenceEquals(connection, context.Database.GetDbConnection()))
        {
            throw new InvalidOperationException(
                "A scoped transaction requires the DbContext to use the same DbConnection instance.");
        }

        if (_current is not null)
        {
            await _current.EnlistAsync(context, cancellationToken);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _current?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (_current is not null)
        {
            await _current.DisposeAsync();
        }
    }

    /// <summary>Disposal without commit rolls back. Enlisted contexts do not own this handle.</summary>
    public sealed class Handle : IDisposable, IAsyncDisposable
    {
        private readonly ScopedTransaction _owner;
        private readonly NpgsqlTransaction _transaction;
        private readonly List<IDbContextTransaction> _enlistments = [];
        private bool _disposed;

        internal Handle(ScopedTransaction owner, NpgsqlTransaction transaction)
        {
            _owner = owner;
            _transaction = transaction;
        }

        internal async Task EnlistAsync(DbContext context, CancellationToken cancellationToken)
        {
            if (ReferenceEquals(context.Database.CurrentTransaction?.GetDbTransaction(), _transaction))
            {
                return;
            }

            var enlistment = await context.Database.UseTransactionAsync(_transaction, cancellationToken);
            _enlistments.Add(enlistment!);
        }

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await _transaction.CommitAsync(cancellationToken);
            await DisposeAsync();
        }

        public async Task RollbackAsync(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await _transaction.RollbackAsync(cancellationToken);
            await DisposeAsync();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                // These EF wrappers do not own the Npgsql transaction. Disposing them
                // clears CurrentTransaction so contexts can be reused in this scope.
                foreach (var enlistment in _enlistments)
                {
                    enlistment.Dispose();
                }
            }
            finally
            {
                try
                {
                    _transaction.Dispose();
                }
                finally
                {
                    _owner._current = null;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                foreach (var enlistment in _enlistments)
                {
                    await enlistment.DisposeAsync();
                }
            }
            finally
            {
                try
                {
                    await _transaction.DisposeAsync();
                }
                finally
                {
                    _owner._current = null;
                }
            }
        }
    }
}
