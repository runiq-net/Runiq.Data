namespace Runiq.Data.SqlServer.IntegrationTests;

/// <summary>
/// Shares one SQL Server container across contract tests while keeping test data isolated.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerContainerFixture>
{
    /// <summary>
    /// Gets the xUnit collection name used by SQL Server provider contract tests.
    /// </summary>
    public const string Name = "SQL Server contract collection";
}
