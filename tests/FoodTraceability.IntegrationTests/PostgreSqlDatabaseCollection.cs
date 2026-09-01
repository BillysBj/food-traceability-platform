namespace FoodTraceability.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class PostgreSqlDatabaseCollection
    : ICollectionFixture<PostgreSqlContainerFixture>
{
    public const string Name = "PostgreSQL database";
}
