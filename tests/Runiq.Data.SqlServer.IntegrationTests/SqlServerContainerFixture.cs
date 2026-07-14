using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Runiq.Data.SqlServer.IntegrationTests;

/// <summary>
/// Owns one SQL Server Testcontainers instance for provider contract tests.
/// </summary>
/// <remarks>
/// The fixture intentionally does not skip tests when Docker is unavailable. Startup failure
/// must be reported as an infrastructure failure because these tests are provider contracts.
/// </remarks>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

    private readonly MsSqlContainer container = new MsSqlBuilder(SqlServerImage)
        .Build();

    /// <summary>
    /// Gets the explicit SQL Server image used by the contract test container.
    /// </summary>
    public string Image => SqlServerImage;

    /// <summary>
    /// Starts the SQL Server container and lets Testcontainers wait until it is ready.
    /// </summary>
    public async Task InitializeAsync()
    {
        await container.StartAsync();
    }

    /// <summary>
    /// Stops and disposes the SQL Server container after all collection tests complete.
    /// </summary>
    public async Task DisposeAsync()
    {
        await container.DisposeAsync();
    }

    /// <summary>
    /// Creates a closed SqlConnection for callers that need to validate connection ownership.
    /// </summary>
    /// <returns>A closed SQL Server connection owned by the caller.</returns>
    public SqlConnection CreateConnection()
    {
        var builder = new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            TrustServerCertificate = true
        };

        return new SqlConnection(builder.ConnectionString);
    }
}
