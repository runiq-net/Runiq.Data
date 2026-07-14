using System.Data;
using Microsoft.Data.Sqlite;

namespace Runiq.Data.IO.Tests.IO.Sql;

/// <summary>
/// Verifies that SQL Read works against the real Microsoft.Data.Sqlite ADO.NET provider.
/// </summary>
public sealed class DataFrameSqlReadSqliteIntegrationTests
{
    // Verifies that the connection and command text overload reads a real SQLite SELECT result.
    [Fact]
    public void ReadSql_WithSqliteConnectionAndCommandText_ReadsEmployees()
    {
        using var connection = CreateOpenEmployeesConnection();

        var df = global::Runiq.Data.DataFrame.ReadSql(
            connection,
            """
            SELECT Id, Name, Salary, Active, Notes
            FROM Employees
            ORDER BY Id
            """);

        Assert.Equal(5, df.Schema.Count);
        Assert.Equal(["Id", "Name", "Salary", "Active", "Notes"], df.Schema.Columns.Select(column => column.Name));
        Assert.Equal(3, df["Id"].Count);
        Assert.Equal([1L, 2L, 3L], Values(df, "Id"));
        Assert.Equal(["Ali", "Ayse", "Mehmet"], Values(df, "Name"));
        Assert.Equal(125000.50d, df["Salary"].GetValue(0));
        Assert.Null(df["Salary"].GetValue(1));
        Assert.Equal(98000.25d, df["Salary"].GetValue(2));
        Assert.Equal([1L, 0L, 1L], Values(df, "Active"));
        Assert.Equal("Senior engineer", df["Notes"].GetValue(0));
        Assert.Null(df["Notes"].GetValue(1));
        Assert.Equal("Remote", df["Notes"].GetValue(2));
    }

    // Verifies that a caller-created SQLite command remains usable after SQL Read consumes its reader.
    [Fact]
    public void ReadSql_WithSqliteCommand_ReadsRowsAndLeavesCommandUsable()
    {
        using var connection = CreateOpenEmployeesConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name FROM Employees ORDER BY Id";

        var df = global::Runiq.Data.DataFrame.ReadSql(command);
        var scalar = command.ExecuteScalar();

        Assert.Equal([1L, 2L, 3L], Values(df, "Id"));
        Assert.Equal(1L, scalar);
        Assert.Equal("SELECT Id, Name FROM Employees ORDER BY Id", command.CommandText);
        Assert.Same(connection, command.Connection);
    }

    // Verifies that SQL Read preserves real SQLite parameters and reads their filtered result.
    [Fact]
    public void ReadSql_WithSqliteParameterizedQuery_PreservesParameterCollection()
    {
        using var connection = CreateOpenEmployeesConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, Name, Salary
            FROM Employees
            WHERE Active = @active
            ORDER BY Id
            """;
        var parameter = command.Parameters.Add("@active", SqliteType.Integer);
        parameter.Value = 1;

        var df = global::Runiq.Data.DataFrame.ReadSql(command);

        Assert.Single(command.Parameters);
        Assert.Same(parameter, command.Parameters[0]);
        Assert.Equal(1, command.Parameters["@active"].Value);
        Assert.Equal([1L, 3L], Values(df, "Id"));
        Assert.Equal(["Ali", "Mehmet"], Values(df, "Name"));
    }

    // Verifies that SQL Read keeps the exact SQLite column ordinal order from the SELECT list.
    [Fact]
    public void ReadSql_WithSqliteColumnProjection_PreservesColumnOrder()
    {
        using var connection = CreateOpenEmployeesConnection();

        var df = global::Runiq.Data.DataFrame.ReadSql(
            connection,
            "SELECT Name, Id, Active FROM Employees ORDER BY Id");

        Assert.Equal(["Name", "Id", "Active"], df.Schema.Columns.Select(column => column.Name));
    }

    // Verifies that SQL Read preserves SQLite row order and does not apply additional sorting.
    [Fact]
    public void ReadSql_WithSqliteOrderByDescending_PreservesRowOrder()
    {
        using var connection = CreateOpenEmployeesConnection();

        var df = global::Runiq.Data.DataFrame.ReadSql(
            connection,
            "SELECT Id, Name FROM Employees ORDER BY Id DESC");

        Assert.Equal([3L, 2L, 1L], Values(df, "Id"));
    }

    // Verifies that SQLite NULL values become DataFrame null values rather than DBNull.Value.
    [Fact]
    public void ReadSql_WithSqliteNullValue_MapsToNull()
    {
        using var connection = CreateOpenEmployeesConnection();

        var df = global::Runiq.Data.DataFrame.ReadSql(
            connection,
            "SELECT Salary, Notes FROM Employees WHERE Id = 2");

        Assert.Null(df["Salary"].GetValue(0));
        Assert.Null(df["Notes"].GetValue(0));
        Assert.NotSame(DBNull.Value, df["Salary"].GetValue(0));
    }

    // Verifies the actual SQLite provider CLR mappings for INTEGER and REAL values.
    [Fact]
    public void ReadSql_WithSqliteNumericValues_PreservesProviderClrTypes()
    {
        using var connection = CreateOpenEmployeesConnection();

        var df = global::Runiq.Data.DataFrame.ReadSql(
            connection,
            "SELECT Id, Salary FROM Employees WHERE Id = 1");

        Assert.IsType<long>(df["Id"].GetValue(0));
        Assert.Equal(1L, df["Id"].GetValue(0));
        Assert.IsType<double>(df["Salary"].GetValue(0));
        Assert.Equal(125000.50d, df["Salary"].GetValue(0));
    }

    // Verifies that SQLite TEXT remains string even when the content resembles other primitives.
    [Fact]
    public void ReadSql_WithSqliteTextValues_DoesNotParseStrings()
    {
        using var connection = CreateOpenEmployeesConnection();
        ExecuteNonQuery(
            connection,
            """
            CREATE TABLE TextValues (Value TEXT NOT NULL);
            INSERT INTO TextValues (Value) VALUES ('123'), ('true'), ('null'), ('2026-07-14');
            """);

        var df = global::Runiq.Data.DataFrame.ReadSql(
            connection,
            "SELECT Value FROM TextValues ORDER BY rowid");

        Assert.Equal(["123", "true", "null", "2026-07-14"], Values(df, "Value"));
        Assert.All(Enumerable.Range(0, df["Value"].Count), row => Assert.IsType<string>(df["Value"].GetValue(row)));
    }

    // Verifies that SQLite BLOB values are read as byte arrays and snapshot-copied into the DataFrame.
    [Fact]
    public void ReadSql_WithSqliteBlob_ReadsByteArraySnapshot()
    {
        using var connection = CreateOpenEmployeesConnection();
        ExecuteNonQuery(connection, "CREATE TABLE Files (Payload BLOB NOT NULL);");
        using var insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO Files (Payload) VALUES (@payload)";
        var bytes = new byte[] { 1, 2, 3, 4 };
        insert.Parameters.Add("@payload", SqliteType.Blob).Value = bytes;
        insert.ExecuteNonQuery();

        var df = global::Runiq.Data.DataFrame.ReadSql(connection, "SELECT Payload FROM Files");
        bytes[0] = 9;

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, (byte[])df["Payload"].GetValue(0)!);
    }

    // Verifies that an empty SQLite result still supplies metadata for DataFrame schema creation.
    [Fact]
    public void ReadSql_WithSqliteEmptyResult_PreservesSchema()
    {
        using var connection = CreateOpenEmployeesConnection();

        var df = global::Runiq.Data.DataFrame.ReadSql(
            connection,
            """
            SELECT Id, Name, Salary
            FROM Employees
            WHERE 1 = 0
            """);

        Assert.Equal(0, df["Id"].Count);
        Assert.Equal(3, df.Schema.Count);
        Assert.Equal(["Id", "Name", "Salary"], df.Schema.Columns.Select(column => column.Name));
    }

    // Verifies that an open in-memory SQLite connection remains open so its database lifetime is preserved.
    [Fact]
    public void ReadSql_WithOpenSqliteConnection_LeavesConnectionOpen()
    {
        using var connection = CreateOpenEmployeesConnection();

        global::Runiq.Data.DataFrame.ReadSql(connection, "SELECT Id FROM Employees LIMIT 1");

        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Equal(3L, CountEmployees(connection));
    }

    // Verifies that SQLite execution failures propagate without closing the caller-owned connection or command.
    [Fact]
    public void ReadSql_WithInvalidSqliteQuery_PropagatesProviderExceptionAndPreservesResources()
    {
        using var connection = CreateOpenEmployeesConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MissingColumn FROM Employees";

        Assert.Throws<SqliteException>(() => global::Runiq.Data.DataFrame.ReadSql(command));

        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Equal("SELECT MissingColumn FROM Employees", command.CommandText);
        command.CommandText = "SELECT COUNT(*) FROM Employees";
        Assert.Equal(3L, command.ExecuteScalar());
    }

    // Verifies that SQL Read does not mutate consumer command state when using a real SQLite command.
    [Fact]
    public void ReadSql_WithSqliteCommand_PreservesCommandState()
    {
        using var connection = CreateOpenEmployeesConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM Employees WHERE Active = @active ORDER BY Id";
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 77;
        var parameter = command.Parameters.Add("@active", SqliteType.Integer);
        parameter.Value = 1;

        global::Runiq.Data.DataFrame.ReadSql(command);

        Assert.Equal("SELECT Id FROM Employees WHERE Active = @active ORDER BY Id", command.CommandText);
        Assert.Equal(CommandType.Text, command.CommandType);
        Assert.Equal(77, command.CommandTimeout);
        Assert.Same(connection, command.Connection);
        Assert.Single(command.Parameters);
        Assert.Same(parameter, command.Parameters[0]);
        Assert.Equal(1, command.Parameters["@active"].Value);
        Assert.Equal(1L, command.ExecuteScalar());
    }

    // Verifies the real SQLite metadata behavior for duplicate unaliased projections.
    [Fact]
    public void ReadSql_WithSqliteDuplicateProjection_RejectsOrPreservesProviderAliases()
    {
        using var connection = CreateOpenEmployeesConnection();

        using var metadataCommand = connection.CreateCommand();
        metadataCommand.CommandText = "SELECT Id, Id FROM Employees LIMIT 1";
        using var reader = metadataCommand.ExecuteReader();
        var providerNames = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();

        if (providerNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() == providerNames.Length)
        {
            // SQLite may provide distinct metadata names for duplicate expressions; in that case
            // SQL Read must preserve the provider contract rather than force a duplicate failure.
            var df = global::Runiq.Data.DataFrame.ReadSql(connection, "SELECT Id, Id FROM Employees LIMIT 1");
            Assert.Equal(providerNames, df.Schema.Columns.Select(column => column.Name));
        }
        else
        {
            Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadSql(connection, "SELECT Id, Id FROM Employees LIMIT 1"));
        }
    }

    // Verifies that SQLite aliases keep their exact casing in the DataFrame schema.
    [Fact]
    public void ReadSql_WithSqliteAliases_PreservesAliasCasing()
    {
        using var connection = CreateOpenEmployeesConnection();

        var df = global::Runiq.Data.DataFrame.ReadSql(
            connection,
            """
            SELECT
                Id AS EmployeeID,
                Name AS employeeName
            FROM Employees
            LIMIT 1
            """);

        Assert.Equal(["EmployeeID", "employeeName"], df.Schema.Columns.Select(column => column.Name));
    }

    // Verifies that SQLite expression columns are read by alias with the provider numeric result.
    [Fact]
    public void ReadSql_WithSqliteExpressionColumn_ReadsAliasAndValue()
    {
        using var connection = CreateOpenEmployeesConnection();

        var df = global::Runiq.Data.DataFrame.ReadSql(
            connection,
            """
            SELECT Id, Salary * 1.10 AS AdjustedSalary
            FROM Employees
            WHERE Salary IS NOT NULL
            ORDER BY Id
            """);

        Assert.Equal(["Id", "AdjustedSalary"], df.Schema.Columns.Select(column => column.Name));
        Assert.Equal([1L, 3L], Values(df, "Id"));
        Assert.Equal(137500.55d, (double)df["AdjustedSalary"].GetValue(0)!, precision: 8);
        Assert.Equal(107800.275d, (double)df["AdjustedSalary"].GetValue(1)!, precision: 8);
    }

    /// <summary>
    /// Creates an isolated open in-memory SQLite database with the shared Employees fixture.
    /// </summary>
    /// <returns>An open connection owned by the caller.</returns>
    private static SqliteConnection CreateOpenEmployeesConnection()
    {
        // SQLite in-memory databases live only while their connection remains open, so each
        // integration test owns one open connection for setup, read, and verification.
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        ExecuteNonQuery(
            connection,
            """
            CREATE TABLE Employees (
                Id INTEGER NOT NULL,
                Name TEXT NOT NULL,
                Salary REAL NULL,
                Active INTEGER NOT NULL,
                Notes TEXT NULL
            );

            INSERT INTO Employees (Id, Name, Salary, Active, Notes) VALUES
                (1, 'Ali', 125000.50, 1, 'Senior engineer'),
                (2, 'Ayse', NULL, 0, NULL),
                (3, 'Mehmet', 98000.25, 1, 'Remote');
            """);

        return connection;
    }

    /// <summary>
    /// Executes SQLite setup SQL against a caller-owned open connection.
    /// </summary>
    private static void ExecuteNonQuery(SqliteConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Reads a DataFrame column into object values while preserving row order for assertions.
    /// </summary>
    private static object?[] Values(global::Runiq.Data.DataFrame df, string columnName)
    {
        var column = df[columnName];
        return Enumerable.Range(0, column.Count).Select(column.GetValue).ToArray();
    }

    /// <summary>
    /// Counts fixture rows to prove the open SQLite in-memory database remains available.
    /// </summary>
    private static long CountEmployees(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Employees";
        return (long)command.ExecuteScalar()!;
    }
}
