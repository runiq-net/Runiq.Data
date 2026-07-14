using System.Data;
using Microsoft.Data.SqlClient;

namespace Runiq.Data.SqlServer.IntegrationTests;

/// <summary>
/// Verifies SQL Read contracts against a real Microsoft SQL Server provider and container.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class DataFrameSqlServerReadTests(SqlServerContainerFixture fixture)
{
    // Verifies that the DbConnection overload reads a real SQL Server result set with schema and row order.
    [Fact]
    public void ReadSql_WithSqlServerConnectionAndCommandText_ReadsEmployees()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateEmployeesTable(connection);

        try
        {
            var df = global::Runiq.Data.DataFrame.ReadSql(
                connection,
                $"""
                SELECT Id, Name, Department, Salary, Active, CreatedAt, ExternalId, Payload
                FROM [{table}]
                ORDER BY Id
                """);

            Assert.Equal(8, df.Schema.Count);
            Assert.Equal(["Id", "Name", "Department", "Salary", "Active", "CreatedAt", "ExternalId", "Payload"], df.Schema.Columns.Select(column => column.Name));
            Assert.Equal([1, 2, 3], Values(df, "Id"));
            Assert.Equal(["Ali", "Ayşe", "Mehmet"], Values(df, "Name"));
            Assert.Equal("Engineering", df["Department"].GetValue(0));
            Assert.Equal(125000.50m, df["Salary"].GetValue(0));
            Assert.Null(df["Salary"].GetValue(2));
            Assert.Equal([true, true, false], Values(df, "Active"));
            Assert.Equal(new DateTime(2026, 7, 14, 9, 30, 0), df["CreatedAt"].GetValue(0));
            Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), df["ExternalId"].GetValue(0));
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, (byte[])df["Payload"].GetValue(0)!);
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies that the DbCommand overload leaves a consumer-owned SqlCommand usable after reading.
    [Fact]
    public void ReadSql_WithSqlCommand_ReadsRowsAndLeavesCommandUsable()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateEmployeesTable(connection);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT Id, Name FROM [{table}] ORDER BY Id";

            var df = global::Runiq.Data.DataFrame.ReadSql(command);
            var scalar = command.ExecuteScalar();

            Assert.Equal([1, 2, 3], Values(df, "Id"));
            Assert.Equal(1, scalar);
            Assert.Equal($"SELECT Id, Name FROM [{table}] ORDER BY Id", command.CommandText);
            Assert.Same(connection, command.Connection);
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies that real SqlParameter usage filters rows and preserves command state.
    [Fact]
    public void ReadSql_WithSqlParameter_PreservesCommandState()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateEmployeesTable(connection);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT Id, Name, Salary
                FROM [{table}]
                WHERE Department = @department
                ORDER BY Id
                """;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 77;
            var parameter = command.Parameters.Add("@department", SqlDbType.NVarChar, 100);
            parameter.Value = "Engineering";

            var df = global::Runiq.Data.DataFrame.ReadSql(command);

            Assert.Equal([1, 3], Values(df, "Id"));
            Assert.Equal(["Ali", "Mehmet"], Values(df, "Name"));
            Assert.Single(command.Parameters);
            Assert.Same(parameter, command.Parameters[0]);
            Assert.Equal("Engineering", command.Parameters["@department"].Value);
            Assert.Equal(CommandType.Text, command.CommandType);
            Assert.Equal(77, command.CommandTimeout);
            Assert.Same(connection, command.Connection);
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies SQL Server primitive type mappings returned by Microsoft.Data.SqlClient.
    [Fact]
    public void ReadSql_WithSqlServerPrimitiveTypes_PreservesProviderClrTypes()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = UniqueName("TypeValues");
        ExecuteNonQuery(
            connection,
            $"""
            CREATE TABLE [{table}] (
                IntValue INT NOT NULL,
                BigIntValue BIGINT NOT NULL,
                SmallIntValue SMALLINT NOT NULL,
                TinyIntValue TINYINT NOT NULL,
                BitValue BIT NOT NULL,
                DecimalValue DECIMAL(18,2) NOT NULL,
                RealValue REAL NOT NULL,
                FloatValue FLOAT NOT NULL,
                TextValue NVARCHAR(100) NOT NULL,
                DateTime2Value DATETIME2 NOT NULL,
                DateTimeOffsetValue DATETIMEOFFSET NOT NULL,
                GuidValue UNIQUEIDENTIFIER NOT NULL,
                BinaryValue VARBINARY(MAX) NOT NULL
            );

            INSERT INTO [{table}] VALUES (
                42,
                9000000000,
                32000,
                250,
                1,
                12345.67,
                CAST(1.25 AS REAL),
                CAST(2.5 AS FLOAT),
                N'123',
                CAST('2026-07-14T10:15:30' AS DATETIME2),
                CAST('2026-07-14T10:15:30+03:00' AS DATETIMEOFFSET),
                '44444444-4444-4444-4444-444444444444',
                0x01020304
            );
            """);

        try
        {
            var df = global::Runiq.Data.DataFrame.ReadSql(connection, $"SELECT * FROM [{table}]");

            Assert.IsType<int>(df["IntValue"].GetValue(0));
            Assert.IsType<long>(df["BigIntValue"].GetValue(0));
            Assert.IsType<short>(df["SmallIntValue"].GetValue(0));
            Assert.IsType<byte>(df["TinyIntValue"].GetValue(0));
            Assert.IsType<bool>(df["BitValue"].GetValue(0));
            Assert.IsType<decimal>(df["DecimalValue"].GetValue(0));
            Assert.IsType<float>(df["RealValue"].GetValue(0));
            Assert.IsType<double>(df["FloatValue"].GetValue(0));
            Assert.IsType<string>(df["TextValue"].GetValue(0));
            Assert.IsType<DateTime>(df["DateTime2Value"].GetValue(0));
            Assert.IsType<DateTimeOffset>(df["DateTimeOffsetValue"].GetValue(0));
            Assert.IsType<Guid>(df["GuidValue"].GetValue(0));
            Assert.IsType<byte[]>(df["BinaryValue"].GetValue(0));
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, (byte[])df["BinaryValue"].GetValue(0)!);
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies that SQL Server NVARCHAR values are not parsed into other primitive types.
    [Fact]
    public void ReadSql_WithSqlServerTextValues_DoesNotParseStrings()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = UniqueName("TextValues");
        ExecuteNonQuery(
            connection,
            $"""
            CREATE TABLE [{table}] (Value NVARCHAR(100) NOT NULL);
            INSERT INTO [{table}] VALUES (N'123'), (N'true'), (N'null'), (N'2026-07-14');
            """);

        try
        {
            var df = global::Runiq.Data.DataFrame.ReadSql(connection, $"SELECT Value FROM [{table}] ORDER BY Value");

            Assert.Equal(["123", "2026-07-14", "null", "true"], Values(df, "Value"));
            Assert.All(Enumerable.Range(0, df["Value"].Count), row => Assert.IsType<string>(df["Value"].GetValue(row)));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies that SQL NULL values become DataFrame null values instead of DBNull.Value.
    [Fact]
    public void ReadSql_WithSqlServerNull_MapsToNull()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateEmployeesTable(connection);

        try
        {
            var df = global::Runiq.Data.DataFrame.ReadSql(connection, $"SELECT Salary, Payload FROM [{table}] WHERE Id = 3");

            Assert.Null(df["Salary"].GetValue(0));
            Assert.Null(df["Payload"].GetValue(0));
            Assert.NotSame(DBNull.Value, df["Salary"].GetValue(0));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies that an empty SQL Server result preserves metadata-derived schema.
    [Fact]
    public void ReadSql_WithSqlServerEmptyResult_PreservesSchema()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateEmployeesTable(connection);

        try
        {
            var df = global::Runiq.Data.DataFrame.ReadSql(
                connection,
                $"SELECT Id, Name, Salary FROM [{table}] WHERE 1 = 0");

            Assert.Equal(0, df["Id"].Count);
            Assert.Equal(3, df.Schema.Count);
            Assert.Equal(["Id", "Name", "Salary"], df.Schema.Columns.Select(column => column.Name));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies that aliases and projection order are preserved exactly as SQL Server reports them.
    [Fact]
    public void ReadSql_WithSqlServerAliasesAndProjectionOrder_PreservesMetadataOrder()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateEmployeesTable(connection);

        try
        {
            var aliasFrame = global::Runiq.Data.DataFrame.ReadSql(
                connection,
                $"SELECT Id AS EmployeeID, Name AS employeeName FROM [{table}] WHERE Id = 1");
            var orderedFrame = global::Runiq.Data.DataFrame.ReadSql(
                connection,
                $"SELECT Name, Active, Id FROM [{table}] ORDER BY Id");

            Assert.Equal(["EmployeeID", "employeeName"], aliasFrame.Schema.Columns.Select(column => column.Name));
            Assert.Equal(["Name", "Active", "Id"], orderedFrame.Schema.Columns.Select(column => column.Name));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies that ORDER BY row order is preserved and Runiq.Data does not sort rows.
    [Fact]
    public void ReadSql_WithSqlServerOrderByDescending_PreservesRowOrder()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateEmployeesTable(connection);

        try
        {
            var df = global::Runiq.Data.DataFrame.ReadSql(connection, $"SELECT Id, Name FROM [{table}] ORDER BY Id DESC");

            Assert.Equal([3, 2, 1], Values(df, "Id"));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies real SqlClient duplicate-name metadata and Runiq.Data's fail-fast behavior when duplicates are reported.
    [Fact]
    public void ReadSql_WithSqlServerDuplicateColumns_RejectsDuplicateProviderNames()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateEmployeesTable(connection);

        try
        {
            string[] providerNames;
            using (var metadataCommand = connection.CreateCommand())
            {
                metadataCommand.CommandText = $"SELECT Id, Id FROM [{table}]";
                using var reader = metadataCommand.ExecuteReader();
                providerNames = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
            }

            if (providerNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() == providerNames.Length)
            {
                var df = global::Runiq.Data.DataFrame.ReadSql(connection, $"SELECT Id, Id FROM [{table}]");
                Assert.Equal(providerNames, df.Schema.Columns.Select(column => column.Name));
            }
            else
            {
                var exception = Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadSql(connection, $"SELECT Id, Id FROM [{table}]"));
                Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies that SQL Server expression columns are read by alias with decimal provider mapping.
    [Fact]
    public void ReadSql_WithSqlServerExpressionColumn_ReadsAliasAndDecimalValue()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateEmployeesTable(connection);

        try
        {
            var df = global::Runiq.Data.DataFrame.ReadSql(
                connection,
                $"""
                SELECT
                    Id,
                    Salary * CAST(1.10 AS DECIMAL(18,2)) AS AdjustedSalary
                FROM [{table}]
                WHERE Salary IS NOT NULL
                ORDER BY Id
                """);

            Assert.Equal(["Id", "AdjustedSalary"], df.Schema.Columns.Select(column => column.Name));
            Assert.Equal([1, 2], Values(df, "Id"));
            Assert.IsType<decimal>(df["AdjustedSalary"].GetValue(0));
            Assert.Equal(137500.5500m, df["AdjustedSalary"].GetValue(0));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies that a caller-opened SqlConnection remains open after SQL Read completes.
    [Fact]
    public void ReadSql_WithOpenSqlConnection_LeavesConnectionOpen()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateEmployeesTable(connection);

        try
        {
            global::Runiq.Data.DataFrame.ReadSql(connection, $"SELECT Id FROM [{table}] WHERE Id = 1");

            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal(3, CountRows(connection, table));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies that a closed SqlConnection is opened temporarily and restored to Closed after success.
    [Fact]
    public void ReadSql_WithClosedSqlConnection_RestoresClosedStateAndRemainsReusable()
    {
        string table;
        using (var setupConnection = fixture.CreateConnection())
        {
            setupConnection.Open();
            table = CreateEmployeesTable(setupConnection);
        }

        using var connection = fixture.CreateConnection();
        try
        {
            var df = global::Runiq.Data.DataFrame.ReadSql(connection, $"SELECT Id FROM [{table}] ORDER BY Id");

            Assert.Equal([1, 2, 3], Values(df, "Id"));
            Assert.Equal(ConnectionState.Closed, connection.State);
            connection.Open();
            Assert.Equal(3, CountRows(connection, table));
        }
        finally
        {
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            DropTable(connection, table);
        }
    }

    // Verifies provider exception propagation and ownership restoration for invalid SQL on open and closed connections.
    [Fact]
    public void ReadSql_WithInvalidSqlServerQuery_PreservesConnectionAndCommandOwnership()
    {
        using var openConnection = fixture.CreateConnection();
        openConnection.Open();
        var table = CreateEmployeesTable(openConnection);

        try
        {
            using var command = openConnection.CreateCommand();
            command.CommandText = $"SELECT MissingColumn FROM [{table}]";

            Assert.Throws<SqlException>(() => global::Runiq.Data.DataFrame.ReadSql(command));
            Assert.Equal(ConnectionState.Open, openConnection.State);
            command.CommandText = $"SELECT COUNT(*) FROM [{table}]";
            Assert.Equal(3, command.ExecuteScalar());

            using var closedConnection = fixture.CreateConnection();
            Assert.Throws<SqlException>(() => global::Runiq.Data.DataFrame.ReadSql(closedConnection, $"SELECT MissingColumn FROM [{table}]"));
            Assert.Equal(ConnectionState.Closed, closedConnection.State);
            closedConnection.Open();
            Assert.Equal(3, CountRows(closedConnection, table));
        }
        finally
        {
            DropTable(openConnection, table);
        }
    }

    // Verifies that real SQL Server multiple result sets are rejected and connection ownership is preserved.
    [Fact]
    public void ReadSql_WithSqlServerMultipleResultSets_Throws()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateEmployeesTable(connection);

        try
        {
            var exception = Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadSql(
                connection,
                $"""
                SELECT Id FROM [{table}];
                SELECT Name FROM [{table}];
                """));

            Assert.Contains("multiple result sets", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal(3, CountRows(connection, table));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies that CommandType.StoredProcedure is supported with a real SqlCommand and SqlParameter.
    [Fact]
    public void ReadSql_WithSqlServerStoredProcedure_ReadsRowsAndPreservesCommandType()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateEmployeesTable(connection);
        var procedure = UniqueName("GetEmployeesByDepartment");

        try
        {
            ExecuteNonQuery(
                connection,
                $"""
                CREATE PROCEDURE [{procedure}]
                    @department NVARCHAR(100)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SELECT Id, Name, Salary
                    FROM [{table}]
                    WHERE Department = @department
                    ORDER BY Id;
                END
                """);

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = procedure;
            var parameter = command.Parameters.Add("@department", SqlDbType.NVarChar, 100);
            parameter.Value = "Engineering";

            var df = global::Runiq.Data.DataFrame.ReadSql(command);

            Assert.Equal([1, 3], Values(df, "Id"));
            Assert.Equal(CommandType.StoredProcedure, command.CommandType);
            Assert.Same(parameter, command.Parameters[0]);
        }
        finally
        {
            DropProcedure(connection, procedure);
            DropTable(connection, table);
        }
    }

    /// <summary>
    /// Creates a uniquely named Employees table with representative SQL Server value types.
    /// </summary>
    private static string CreateEmployeesTable(SqlConnection connection)
    {
        var table = UniqueName("Employees");
        ExecuteNonQuery(
            connection,
            $"""
            CREATE TABLE [{table}] (
                Id INT NOT NULL,
                Name NVARCHAR(100) NOT NULL,
                Department NVARCHAR(100) NOT NULL,
                Salary DECIMAL(18,2) NULL,
                Active BIT NOT NULL,
                CreatedAt DATETIME2 NOT NULL,
                ExternalId UNIQUEIDENTIFIER NOT NULL,
                Payload VARBINARY(MAX) NULL
            );

            INSERT INTO [{table}] (Id, Name, Department, Salary, Active, CreatedAt, ExternalId, Payload) VALUES
                (1, N'Ali', N'Engineering', 125000.50, 1, CAST('2026-07-14T09:30:00' AS DATETIME2), '11111111-1111-1111-1111-111111111111', 0x01020304),
                (2, N'Ayşe', N'Finance', 110000.00, 1, CAST('2026-07-15T09:30:00' AS DATETIME2), '22222222-2222-2222-2222-222222222222', 0x05060708),
                (3, N'Mehmet', N'Engineering', NULL, 0, CAST('2026-07-16T09:30:00' AS DATETIME2), '33333333-3333-3333-3333-333333333333', NULL);
            """);

        return table;
    }

    /// <summary>
    /// Executes a SQL command against a caller-owned open connection.
    /// </summary>
    private static void ExecuteNonQuery(SqlConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Drops a generated table name during cleanup without hiding primary test failures.
    /// </summary>
    private static void DropTable(SqlConnection connection, string table)
    {
        ExecuteNonQuery(connection, $"DROP TABLE IF EXISTS [{table}];");
    }

    /// <summary>
    /// Drops a generated stored procedure name during cleanup without hiding primary test failures.
    /// </summary>
    private static void DropProcedure(SqlConnection connection, string procedure)
    {
        ExecuteNonQuery(connection, $"DROP PROCEDURE IF EXISTS [{procedure}];");
    }

    /// <summary>
    /// Counts rows in a generated test table to verify connection reuse after SQL Read.
    /// </summary>
    private static int CountRows(SqlConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM [{table}]";
        return (int)command.ExecuteScalar()!;
    }

    /// <summary>
    /// Reads a DataFrame column into object values while preserving provider row order.
    /// </summary>
    private static object?[] Values(global::Runiq.Data.DataFrame df, string columnName)
    {
        var column = df[columnName];
        return Enumerable.Range(0, column.Count).Select(column.GetValue).ToArray();
    }

    /// <summary>
    /// Generates an internal SQL identifier using only letters, digits, and underscores.
    /// </summary>
    private static string UniqueName(string prefix)
    {
        return $"{prefix}_{Guid.NewGuid():N}";
    }
}
