namespace Runiq.Data.PostgreSql.IntegrationTests;

/// <summary>
/// Shares one PostgreSQL container across contract tests while each test isolates its schema.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlContainerFixture>
{
    /// <summary>
    /// Gets the xUnit collection name used by PostgreSQL provider contract tests.
    /// </summary>
    public const string Name = "PostgreSQL contract collection";
}
