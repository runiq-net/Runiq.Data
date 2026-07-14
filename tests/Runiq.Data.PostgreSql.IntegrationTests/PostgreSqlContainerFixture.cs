using Npgsql;
using Testcontainers.PostgreSql;

namespace Runiq.Data.PostgreSql.IntegrationTests;

/// <summary>
/// Owns one PostgreSQL Testcontainers instance for provider contract tests.
/// </summary>
/// <remarks>
/// The fixture intentionally reports Docker startup failures as infrastructure failures. These
/// tests are provider contracts and must not pass unless a real PostgreSQL container is used.
/// </remarks>
public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    private const string PostgreSqlImage = "postgres:17-alpine";

    private readonly PostgreSqlContainer container = new PostgreSqlBuilder(PostgreSqlImage)
        .Build();

    /// <summary>
    /// Gets the explicit PostgreSQL image used by the contract test container.
    /// </summary>
    public string Image => PostgreSqlImage;

    /// <summary>
    /// Starts PostgreSQL and relies on Testcontainers readiness before tests create connections.
    /// </summary>
    public async Task InitializeAsync()
    {
        await container.StartAsync();
    }

    /// <summary>
    /// Stops and disposes the PostgreSQL container after all collection tests complete.
    /// </summary>
    public async Task DisposeAsync()
    {
        await container.DisposeAsync();
    }

    /// <summary>
    /// Creates a closed NpgsqlConnection for callers that validate connection ownership.
    /// </summary>
    /// <returns>A closed PostgreSQL connection owned by the caller.</returns>
    public NpgsqlConnection CreateConnection()
    {
        return new NpgsqlConnection(container.GetConnectionString());
    }
}
