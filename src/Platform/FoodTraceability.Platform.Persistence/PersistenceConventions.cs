using FoodTraceability.BuildingBlocks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace FoodTraceability.Platform.Persistence;

public static class PersistenceConventions
{
    public const string MigrationsHistoryTableName = "__ef_migrations_history";

    public static DbContextOptionsBuilder UseFoodTraceabilityPostgres(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        string migrationsHistorySchema)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationsHistorySchema);

        return optionsBuilder
            .UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.UseFoodTraceabilityMigrationsHistory(migrationsHistorySchema))
            .UseSnakeCaseNamingConvention();
    }

    public static void UseFoodTraceabilityMigrationsHistory(
        this NpgsqlDbContextOptionsBuilder optionsBuilder,
        string schema)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        optionsBuilder.MigrationsHistoryTable(MigrationsHistoryTableName, schema);
    }

    public static void UseFoodTraceabilityModelConventions(
        this ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<MicrosecondDateTimeOffsetConverter>()
            .HaveColumnType("timestamp with time zone");
    }

    public sealed class MicrosecondDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
    {
        public MicrosecondDateTimeOffsetConverter()
            : base(
                value => TimestampPrecision.TruncateToMicroseconds(value),
                value => value)
        {
        }
    }
}
