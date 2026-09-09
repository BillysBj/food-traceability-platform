using FoodTraceability.Modules.Traceability.Domain;
using FoodTraceability.Modules.Traceability.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class EventTypeMigrationTests(PostgreSqlContainerFixture database)
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 9, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EventTypeTableHasExactlyTheExpectedColumns()
    {
        const string sql = """
            SELECT column_name,
                   is_nullable,
                   data_type,
                   character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'trace'
              AND table_name = 'event_type'
            ORDER BY ordinal_position;
            """;

        var columns = await QueryAsync(
            sql,
            static reader => new DatabaseColumn(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3)));

        Assert.Equal(
            [
                new DatabaseColumn("event_type_id", "NO", "uuid", null),
                new DatabaseColumn(
                    "code",
                    "NO",
                    "character varying",
                    EventTypeCode.MaximumLength),
                new DatabaseColumn("created_at", "NO", "timestamp with time zone", null),
            ],
            columns);
    }

    [Fact]
    public async Task ExactlyTheNineteenCoreEventTypesAreSeededWithExpectedIds()
    {
        const string sql = """
            SELECT event_type_id, code
            FROM trace.event_type
            ORDER BY code;
            """;

        var eventTypes = await QueryAsync(
            sql,
            static reader => new SeededEventType(reader.GetGuid(0), reader.GetString(1)));

        Assert.Equal(
            [
                new SeededEventType(
                    Guid.Parse("7a5bb899-9c04-5dbf-9341-64bc402c2ed3"),
                    "BLOCK"),
                new SeededEventType(
                    Guid.Parse("76ad5624-efd3-54d8-903e-7a9fdb221adb"),
                    "BOTTLE"),
                new SeededEventType(
                    Guid.Parse("243a920d-b978-5202-896e-6428a74a2c22"),
                    "DELIVER"),
                new SeededEventType(
                    Guid.Parse("36ca16f4-df60-56f3-8d30-60af75da9c92"),
                    "DISPOSE"),
                new SeededEventType(
                    Guid.Parse("784ded8c-54a9-595e-b2b2-10acbf16e887"),
                    "HARVEST"),
                new SeededEventType(
                    Guid.Parse("58929c0a-8d65-5a38-a6d1-6d74cb144b44"),
                    "MIX"),
                new SeededEventType(
                    Guid.Parse("b94a7503-17b1-506a-a738-b169290da379"),
                    "PACK"),
                new SeededEventType(
                    Guid.Parse("b2830815-b10e-5507-9ed3-13679fc08e5e"),
                    "PRESS"),
                new SeededEventType(
                    Guid.Parse("99147ae3-6e5b-599d-830d-3f55d80d8242"),
                    "PROCESS"),
                new SeededEventType(
                    Guid.Parse("dbe268c4-4290-5249-b3ad-5ffe81b3fdc8"),
                    "QUALITY_RELEASE"),
                new SeededEventType(
                    Guid.Parse("0195e858-b3f0-54be-ad01-581f168ecf8f"),
                    "RECEIVE"),
                new SeededEventType(
                    Guid.Parse("ad863244-bf75-5f34-886a-eb400f82985d"),
                    "RETURN"),
                new SeededEventType(
                    Guid.Parse("49163869-4ef0-501f-984d-ab3adb5e1996"),
                    "SAMPLE"),
                new SeededEventType(
                    Guid.Parse("3022cd5f-94b0-52d8-af19-2f1959360602"),
                    "SELL"),
                new SeededEventType(
                    Guid.Parse("7b93d110-a9e4-53af-b3fc-5f7580f7a8f6"),
                    "SHIP"),
                new SeededEventType(
                    Guid.Parse("5373e85b-e968-55b7-a8bc-7f44008647d2"),
                    "SPLIT"),
                new SeededEventType(
                    Guid.Parse("26614721-e8f4-5513-a9cf-0c2422fc003b"),
                    "STORE"),
                new SeededEventType(
                    Guid.Parse("4e471a84-b9d6-5618-aee9-6ca1f3373ddd"),
                    "TRANSFER"),
                new SeededEventType(
                    Guid.Parse("0283c126-a16a-516d-aeec-b246af44b88a"),
                    "UNBLOCK"),
            ],
            eventTypes);
    }

    [Fact]
    public async Task EventTypeCodeHasAUniqueIndex()
    {
        const string sql = """
            SELECT indexdef
            FROM pg_indexes
            WHERE schemaname = 'trace'
              AND tablename = 'event_type'
              AND indexname = 'ix_event_type_code';
            """;

        var indexDefinitions = await QueryAsync(sql, static reader => reader.GetString(0));
        var indexDefinition = Assert.Single(indexDefinitions);

        Assert.Contains("CREATE UNIQUE INDEX", indexDefinition, StringComparison.Ordinal);
        Assert.Contains("(code)", indexDefinition, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DuplicateEventTypeCodeIsRejectedByTheExpectedIndex()
    {
        await using var context = database.CreateTraceabilityDbContext();
        context.EventTypes.Add(EventType.Create(
            Guid.NewGuid(),
            EventTypeCode.Create("harvest"),
            CreatedAt));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);

        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
        Assert.Equal("ix_event_type_code", postgresException.ConstraintName);
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<NpgsqlDataReader, T> map)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.TraceabilityConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(timeout.Token);
        var rows = new List<T>();

        while (await reader.ReadAsync(timeout.Token))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    private sealed record DatabaseColumn(
        string Name,
        string IsNullable,
        string DataType,
        int? MaximumLength);

    private sealed record SeededEventType(Guid Id, string Code);
}
