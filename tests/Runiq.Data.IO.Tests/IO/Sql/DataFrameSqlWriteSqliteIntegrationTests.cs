using System.Data;
using Microsoft.Data.Sqlite;

namespace Runiq.Data.IO.Tests.IO.Sql;

/// <summary>
/// Verifies that SQL Write appends DataFrame rows through the real Microsoft.Data.Sqlite ADO.NET provider.
/// </summary>
public sealed class DataFrameSqlWriteSqliteIntegrationTests
{
    // Verifies that default SQL Write appends rows to an existing SQLite table and preserves primitive values.
    [Fact]
    public void WriteSql_WithSqliteConnection_AppendsEmployees()
    {
        using var connection = CreateOpenEmployeesConnection();
        var df = CreateEmployeesDataFrame();

        df.WriteSql(connection, "Employees");

        Assert.Equal(3L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM Employees"));
        Assert.Equal([1L, 2L, 3L], QueryColumn<long>(connection, "SELECT Id FROM Employees ORDER BY Id"));
        Assert.Equal(["Ali", "Ayse", "Mehmet"], QueryColumn<string>(connection, "SELECT Name FROM Employees ORDER BY Id"));
        Assert.Equal(["Engineering", "Finance", "Engineering"], QueryColumn<string>(connection, "SELECT Department FROM Employees ORDER BY Id"));
        Assert.Equal(125000.50d, ExecuteScalar<double>(connection, "SELECT Salary FROM Employees WHERE Id = 1"));
        Assert.Equal(110000.00d, ExecuteScalar<double>(connection, "SELECT Salary FROM Employees WHERE Id = 2"));
        Assert.Equal(1L, ExecuteScalar<long>(connection, "SELECT Salary IS NULL FROM Employees WHERE Id = 3"));
        Assert.Equal([1L, 1L, 0L], QueryColumn<long>(connection, "SELECT Active FROM Employees ORDER BY Id"));
    }

    // Verifies that SQL Write preserves existing SQLite rows and performs append-only inserts.
    [Fact]
    public void WriteSql_WithExistingSqliteRows_AppendsWithoutReplacing()
    {
        using var connection = CreateOpenEmployeesConnection();
        ExecuteNonQuery(
            connection,
            """
            INSERT INTO Employees (Id, Name, Department, Salary, Active)
            VALUES (10, 'Existing', 'Operations', 90000.00, 1);
            """);
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 11, 12 },
            Name = new[] { "New One", "New Two" },
            Department = new[] { "Support", "Sales" },
            Salary = new double?[] { 80000.00, 85000.00 },
            Active = new[] { true, false }
        });

        df.WriteSql(connection, "Employees");

        Assert.Equal([10L, 11L, 12L], QueryColumn<long>(connection, "SELECT Id FROM Employees ORDER BY Id"));
        Assert.Equal(1L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM Employees WHERE Name = 'Existing'"));
    }

    // Verifies that the connection overload uses a real internal transaction and leaves an open connection usable.
    [Fact]
    public void WriteSql_WithInternalTransaction_CommitsAndLeavesOpenSqliteConnectionUsable()
    {
        using var connection = CreateOpenEmployeesConnection();

        CreateEmployeesDataFrame().WriteSql(connection, "Employees");

        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Equal(3L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM Employees"));
        ExecuteNonQuery(
            connection,
            """
            INSERT INTO Employees (Id, Name, Department, Salary, Active)
            VALUES (4, 'Usable', 'Quality', NULL, 1);
            """);
        Assert.Equal(4L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM Employees"));
    }

    // Verifies that a real SQLite constraint failure rolls back all rows written by the internal transaction.
    [Fact]
    public void WriteSql_WhenInternalTransactionFails_RollsBackPartialSqliteRows()
    {
        using var connection = CreateOpenEmployeesConnection();
        ExecuteNonQuery(
            connection,
            """
            INSERT INTO Employees (Id, Name, Department, Salary, Active)
            VALUES (100, 'Existing', 'Operations', NULL, 1);
            """);
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 1, 2, 100 },
            Name = new[] { "First", "Second", "Duplicate" },
            Department = new[] { "Engineering", "Finance", "Legal" },
            Salary = new double?[] { 1.0d, 2.0d, 3.0d },
            Active = new[] { true, true, true }
        });

        Assert.Throws<SqliteException>(() => df.WriteSql(connection, "Employees"));

        Assert.Equal([100L], QueryColumn<long>(connection, "SELECT Id FROM Employees ORDER BY Id"));
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    // Verifies that a caller-owned SQLite transaction sees written rows and remains rollback-controlled by the caller.
    [Fact]
    public void WriteSql_WithExternalTransaction_RowsAreVisibleUntilCallerRollback()
    {
        using var connection = CreateOpenEmployeesConnection();
        using var transaction = connection.BeginTransaction();

        CreateEmployeesDataFrame().WriteSql(connection, "Employees", new SqlWriteOptions { Transaction = transaction });

        Assert.Equal(3L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM Employees", transaction));
        transaction.Rollback();
        Assert.Equal(0L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM Employees"));
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    // Verifies that caller commit, not SQL Write, makes rows durable for an external SQLite transaction.
    [Fact]
    public void WriteSql_WithExternalTransaction_CallerCommitPersistsRows()
    {
        using var connection = CreateOpenEmployeesConnection();
        using var transaction = connection.BeginTransaction();

        CreateEmployeesDataFrame().WriteSql(connection, "Employees", new SqlWriteOptions { Transaction = transaction });
        transaction.Commit();

        Assert.Equal(3L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM Employees"));
    }

    // Verifies that external transaction failures do not dispose or rollback the caller-owned SQLite transaction.
    [Fact]
    public void WriteSql_WhenExternalTransactionFails_CallerCanObserveAndRollback()
    {
        using var connection = CreateOpenEmployeesConnection();
        using var transaction = connection.BeginTransaction();
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 1, 2, 1 },
            Name = new[] { "First", "Second", "Duplicate" },
            Department = new[] { "Engineering", "Finance", "Legal" },
            Salary = new double?[] { 1.0d, 2.0d, 3.0d },
            Active = new[] { true, true, true }
        });

        Assert.Throws<SqliteException>(() => df.WriteSql(connection, "Employees", new SqlWriteOptions { Transaction = transaction }));

        Assert.Equal(2L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM Employees", transaction));
        transaction.Rollback();
        Assert.Equal(0L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM Employees"));
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    // Verifies that DataFrame null cells are written as SQL NULL through real SQLite parameters.
    [Fact]
    public void WriteSql_WithNullCell_WritesSqlNull()
    {
        using var connection = CreateOpenEmployeesConnection();

        CreateEmployeesDataFrame().WriteSql(connection, "Employees");

        Assert.Equal(1L, ExecuteScalar<long>(connection, "SELECT Salary IS NULL FROM Employees WHERE Id = 3"));
    }

    // Verifies that string-looking values remain SQLite TEXT and are not parsed as primitives.
    [Fact]
    public void WriteSql_WithStringLookingValues_PreservesText()
    {
        using var connection = CreateOpenConnection();
        ExecuteNonQuery(connection, "CREATE TABLE TextValues (Value TEXT NOT NULL);");
        var values = new[] { "123", "true", "null", "2026-07-14", "=SUM(A1:A2)" };
        var df = global::Runiq.Data.DataFrame.Create(new { Value = values });

        df.WriteSql(connection, "TextValues");

        Assert.Equal(values, QueryColumn<string>(connection, "SELECT Value FROM TextValues ORDER BY rowid"));
        Assert.Equal(["text", "text", "text", "text", "text"], QueryColumn<string>(connection, "SELECT typeof(Value) FROM TextValues ORDER BY rowid"));
    }

    // Verifies that integer-family values are stored as SQLite numeric values without expecting CLR type round-trip identity.
    [Fact]
    public void WriteSql_WithIntegerFamilyValues_WritesSqliteIntegers()
    {
        using var connection = CreateOpenConnection();
        ExecuteNonQuery(
            connection,
            """
            CREATE TABLE IntegerValues (
                ByteValue INTEGER NOT NULL,
                ShortValue INTEGER NOT NULL,
                IntValue INTEGER NOT NULL,
                LongValue INTEGER NOT NULL
            );
            """);
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            ByteValue = new[] { (byte)7 },
            ShortValue = new[] { (short)-8 },
            IntValue = new[] { 123 },
            LongValue = new[] { 9000000000L }
        });

        df.WriteSql(connection, "IntegerValues");

        Assert.Equal(7L, ExecuteScalar<long>(connection, "SELECT ByteValue FROM IntegerValues"));
        Assert.Equal(-8L, ExecuteScalar<long>(connection, "SELECT ShortValue FROM IntegerValues"));
        Assert.Equal(123L, ExecuteScalar<long>(connection, "SELECT IntValue FROM IntegerValues"));
        Assert.Equal(9000000000L, ExecuteScalar<long>(connection, "SELECT LongValue FROM IntegerValues"));
    }

    // Verifies that floating-point and decimal values are written using SQLite's natural REAL affinity.
    [Fact]
    public void WriteSql_WithFloatingPointAndDecimalValues_WritesNumericValues()
    {
        using var connection = CreateOpenConnection();
        ExecuteNonQuery(
            connection,
            """
            CREATE TABLE NumericValues (
                SingleValue REAL NOT NULL,
                DoubleValue REAL NOT NULL,
                DecimalValue REAL NOT NULL
            );
            """);
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            SingleValue = new[] { 1.25f },
            DoubleValue = new[] { 2.5d },
            DecimalValue = new[] { 3.75m }
        });

        df.WriteSql(connection, "NumericValues");

        Assert.Equal(1.25d, ExecuteScalar<double>(connection, "SELECT SingleValue FROM NumericValues"), precision: 8);
        Assert.Equal(2.5d, ExecuteScalar<double>(connection, "SELECT DoubleValue FROM NumericValues"), precision: 8);
        Assert.Equal(3.75d, ExecuteScalar<double>(connection, "SELECT DecimalValue FROM NumericValues"), precision: 8);
    }

    // Verifies SQLite's provider-natural Guid storage without assuming a native UUID type exists.
    [Fact]
    public void WriteSql_WithGuidValue_WritesProviderTextRepresentation()
    {
        using var connection = CreateOpenConnection();
        ExecuteNonQuery(connection, "CREATE TABLE GuidValues (Value TEXT NOT NULL);");
        var guid = Guid.Parse("2f06c82a-02f7-4f97-9d97-3653ac75d18a");
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { guid } });

        df.WriteSql(connection, "GuidValues");

        Assert.Equal(guid.ToString("D").ToUpperInvariant(), ExecuteScalar<string>(connection, "SELECT Value FROM GuidValues"));
        Assert.Equal("text", ExecuteScalar<string>(connection, "SELECT typeof(Value) FROM GuidValues"));
    }

    // Verifies DateTime is accepted by Microsoft.Data.Sqlite and can be read through provider conversion.
    [Fact]
    public void WriteSql_WithDateTimeValue_RoundTripsThroughSqliteProvider()
    {
        using var connection = CreateOpenConnection();
        ExecuteNonQuery(connection, "CREATE TABLE DateTimeValues (Value TEXT NOT NULL);");
        var value = new DateTime(2026, 7, 14, 10, 11, 12, DateTimeKind.Unspecified);
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { value } });

        df.WriteSql(connection, "DateTimeValues");

        Assert.Equal(value, ExecuteReaderValue(connection, "SELECT Value FROM DateTimeValues", static reader => reader.GetDateTime(0)));
        Assert.Equal("text", ExecuteScalar<string>(connection, "SELECT typeof(Value) FROM DateTimeValues"));
    }

    // Verifies DateTimeOffset is accepted by Microsoft.Data.Sqlite and preserves the caller-supplied instant and offset.
    [Fact]
    public void WriteSql_WithDateTimeOffsetValue_RoundTripsThroughSqliteProvider()
    {
        using var connection = CreateOpenConnection();
        ExecuteNonQuery(connection, "CREATE TABLE DateTimeOffsetValues (Value TEXT NOT NULL);");
        var value = new DateTimeOffset(2026, 7, 14, 10, 11, 12, TimeSpan.FromHours(3));
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { value } });

        df.WriteSql(connection, "DateTimeOffsetValues");

        Assert.Equal(value, ExecuteReaderValue(connection, "SELECT Value FROM DateTimeOffsetValues", static reader => reader.GetDateTimeOffset(0)));
        Assert.Equal("text", ExecuteScalar<string>(connection, "SELECT typeof(Value) FROM DateTimeOffsetValues"));
    }

    // Verifies DateOnly is accepted by Microsoft.Data.Sqlite and round-trips through provider date conversion.
    [Fact]
    public void WriteSql_WithDateOnlyValue_RoundTripsThroughSqliteProvider()
    {
        using var connection = CreateOpenConnection();
        ExecuteNonQuery(connection, "CREATE TABLE DateValues (Value TEXT NOT NULL);");
        var value = new DateOnly(2026, 7, 14);
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { value } });

        df.WriteSql(connection, "DateValues");

        Assert.Equal(value, ExecuteReaderValue(connection, "SELECT Value FROM DateValues", static reader => reader.GetFieldValue<DateOnly>(0)));
        Assert.Equal("text", ExecuteScalar<string>(connection, "SELECT typeof(Value) FROM DateValues"));
    }

    // Verifies TimeOnly and TimeSpan DbType.Time mappings against Microsoft.Data.Sqlite's real conversion behavior.
    [Fact]
    public void WriteSql_WithTimeOnlyAndTimeSpanValues_RoundTripsThroughSqliteProvider()
    {
        using var connection = CreateOpenConnection();
        ExecuteNonQuery(
            connection,
            """
            CREATE TABLE TimeValues (
                TimeOnlyValue TEXT NOT NULL,
                TimeSpanValue TEXT NOT NULL
            );
            """);
        var timeOnly = new TimeOnly(10, 11, 12);
        var timeSpan = new TimeSpan(10, 11, 12);
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            TimeOnlyValue = new[] { timeOnly },
            TimeSpanValue = new[] { timeSpan }
        });

        df.WriteSql(connection, "TimeValues");

        Assert.Equal(timeOnly, ExecuteReaderValue(connection, "SELECT TimeOnlyValue FROM TimeValues", static reader => reader.GetFieldValue<TimeOnly>(0)));
        Assert.Equal(timeSpan, ExecuteReaderValue(connection, "SELECT TimeSpanValue FROM TimeValues", static reader => reader.GetFieldValue<TimeSpan>(0)));
        Assert.Equal(["text", "text"], QueryColumn<string>(connection, "SELECT typeof(TimeOnlyValue), typeof(TimeSpanValue) FROM TimeValues"));
    }

    // Verifies byte arrays are written as SQLite BLOB values and the stored sequence is stable.
    [Fact]
    public void WriteSql_WithByteArrayValue_WritesBlob()
    {
        using var connection = CreateOpenConnection();
        ExecuteNonQuery(connection, "CREATE TABLE Files (Payload BLOB NOT NULL);");
        var bytes = new byte[] { 1, 2, 3, 4 };
        var df = global::Runiq.Data.DataFrame.Create(new { Payload = new[] { bytes } });

        df.WriteSql(connection, "Files");
        bytes[0] = 9;

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, ExecuteReaderValue(connection, "SELECT Payload FROM Files", static reader => (byte[])reader.GetValue(0)));
        Assert.Equal("blob", ExecuteScalar<string>(connection, "SELECT typeof(Payload) FROM Files"));
    }

    // Verifies zero-row SQL Write is a no-op against an existing open SQLite connection.
    [Fact]
    public void WriteSql_WithEmptyDataFrame_DoesNotChangeExistingSqliteTable()
    {
        using var connection = CreateOpenEmployeesConnection();
        var df = global::Runiq.Data.DataFrame.Create(new { Id = Array.Empty<int>() });

        df.WriteSql(connection, "Employees");

        Assert.Equal(0L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM Employees"));
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    // Verifies an initially open SQLite connection remains open and its in-memory database remains available.
    [Fact]
    public void WriteSql_WithInitiallyOpenSqliteConnection_LeavesConnectionOpen()
    {
        using var connection = CreateOpenEmployeesConnection();

        CreateEmployeesDataFrame().WriteSql(connection, "Employees");

        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Equal(3L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM Employees"));
    }

    // Verifies an initially closed SQLite connection is opened only temporarily and remains reusable after writing.
    [Fact]
    public void WriteSql_WithInitiallyClosedSqliteConnection_RestoresClosedStateAndWritesRows()
    {
        var databasePath = CreateTempDatabasePath();
        try
        {
            using (var setupConnection = new SqliteConnection(CreateFileConnectionString(databasePath)))
            {
                setupConnection.Open();
                ExecuteNonQuery(setupConnection, CreateEmployeesTableSql);
            }

            using (var connection = new SqliteConnection(CreateFileConnectionString(databasePath)))
            {
                CreateEmployeesDataFrame().WriteSql(connection, "Employees");

                Assert.Equal(ConnectionState.Closed, connection.State);
                connection.Open();
                Assert.Equal(3L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM Employees"));
            }
        }
        finally
        {
            DeleteSqliteDatabaseFiles(databasePath);
        }
    }

    // Verifies missing SQLite destination tables propagate provider exceptions and leave the connection usable.
    [Fact]
    public void WriteSql_WithMissingSqliteTable_ThrowsProviderExceptionAndKeepsConnectionUsable()
    {
        using var connection = CreateOpenEmployeesConnection();

        var exception = Assert.Throws<SqliteException>(() => CreateEmployeesDataFrame().WriteSql(connection, "MissingEmployees"));

        Assert.Contains("MissingEmployees", exception.Message, StringComparison.Ordinal);
        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Equal(0L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM Employees"));
    }

    // Verifies SQLite constraint failures propagate provider exceptions, roll back internal work, and keep the connection reusable.
    [Fact]
    public void WriteSql_WithSqliteConstraintFailure_ThrowsProviderExceptionAndRollsBack()
    {
        using var connection = CreateOpenEmployeesConnection();
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 1, 1 },
            Name = new[] { "First", "Duplicate" },
            Department = new[] { "Engineering", "Finance" },
            Salary = new double?[] { 1.0d, 2.0d },
            Active = new[] { true, true }
        });

        Assert.Throws<SqliteException>(() => df.WriteSql(connection, "Employees"));

        Assert.Equal(0L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM Employees"));
        ExecuteNonQuery(
            connection,
            """
            INSERT INTO Employees (Id, Name, Department, Salary, Active)
            VALUES (99, 'Reusable', 'Quality', NULL, 1);
            """);
        Assert.Equal(1L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM Employees"));
    }

    // Verifies SQLite accepts simple two-part identifiers through the main schema qualifier.
    [Fact]
    public void WriteSql_WithMainSchemaQualifiedSqliteTable_AppendsRows()
    {
        using var connection = CreateOpenEmployeesConnection();

        CreateEmployeesDataFrame().WriteSql(connection, "main.Employees");

        Assert.Equal(3L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM main.Employees"));
    }

    // Verifies unsafe identifiers fail validation before SQLite can execute injected SQL.
    [Fact]
    public void WriteSql_WithUnsafeIdentifier_RejectsBeforeProviderMutation()
    {
        using var connection = CreateOpenEmployeesConnection();

        Assert.ThrowsAny<ArgumentException>(() => CreateEmployeesDataFrame().WriteSql(connection, "Employees;DROP_TABLE"));

        Assert.Equal(0L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM Employees"));
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    // Verifies SQL Write does not mutate the source DataFrame while SQLite receives the inserted rows.
    [Fact]
    public void WriteSql_DoesNotMutateSourceDataFrame()
    {
        using var connection = CreateOpenEmployeesConnection();
        var df = CreateEmployeesDataFrame();
        var columnsBefore = df.Schema.Columns.Select(static column => column.Name).ToArray();
        var valuesBefore = Enumerable.Range(0, df["Id"].Count)
            .Select(row => df.Schema.Columns.Select(column => df[column.Name].GetValue(row)).ToArray())
            .ToArray();

        df.WriteSql(connection, "Employees");

        Assert.Equal(columnsBefore, df.Schema.Columns.Select(static column => column.Name));
        Assert.Equal(3, df["Id"].Count);
        Assert.Equal(5, df.Schema.Count);
        for (var row = 0; row < valuesBefore.Length; row++)
        {
            Assert.Equal(valuesBefore[row], df.Schema.Columns.Select(column => df[column.Name].GetValue(row)).ToArray());
        }
    }

    // Verifies a representative SQLite Write/Read round trip for provider-stable primitive values.
    [Fact]
    public void WriteSql_ThenReadSql_RoundTripsStableSqliteValues()
    {
        using var connection = CreateOpenConnection();
        ExecuteNonQuery(
            connection,
            """
            CREATE TABLE RoundTripValues (
                Id INTEGER NOT NULL,
                Name TEXT NOT NULL,
                Active INTEGER NOT NULL,
                Score REAL NULL,
                Payload BLOB NOT NULL
            );
            """);
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 1, 2 },
            Name = new[] { "One", "Two" },
            Active = new[] { true, false },
            Score = new double?[] { 1.5d, null },
            Payload = new[] { new byte[] { 1, 2 }, new byte[] { 3, 4 } }
        });

        df.WriteSql(connection, "RoundTripValues");
        var read = global::Runiq.Data.DataFrame.ReadSql(connection, "SELECT Id, Name, Active, Score, Payload FROM RoundTripValues ORDER BY Id");

        Assert.Equal([1L, 2L], Values(read, "Id"));
        Assert.Equal(["One", "Two"], Values(read, "Name"));
        Assert.Equal([1L, 0L], Values(read, "Active"));
        Assert.Equal(1.5d, read["Score"].GetValue(0));
        Assert.Null(read["Score"].GetValue(1));
        Assert.Equal(new byte[] { 1, 2 }, (byte[])read["Payload"].GetValue(0)!);
        Assert.Equal(new byte[] { 3, 4 }, (byte[])read["Payload"].GetValue(1)!);
    }

    private const string CreateEmployeesTableSql =
        """
        CREATE TABLE Employees (
            Id INTEGER NOT NULL PRIMARY KEY,
            Name TEXT NOT NULL,
            Department TEXT NOT NULL,
            Salary REAL NULL,
            Active INTEGER NOT NULL
        );
        """;

    /// <summary>
    /// Creates an isolated open in-memory SQLite database with an empty Employees table.
    /// </summary>
    private static SqliteConnection CreateOpenEmployeesConnection()
    {
        // SQLite in-memory databases are tied to the owning open connection, so each test owns
        // the connection through setup, write execution, and verification.
        var connection = CreateOpenConnection();
        ExecuteNonQuery(connection, CreateEmployeesTableSql);
        return connection;
    }

    /// <summary>
    /// Creates an open in-memory SQLite connection owned by the caller.
    /// </summary>
    private static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Creates the representative Employees DataFrame used by append and transaction tests.
    /// </summary>
    private static global::Runiq.Data.DataFrame CreateEmployeesDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 1, 2, 3 },
            Name = new[] { "Ali", "Ayse", "Mehmet" },
            Department = new[] { "Engineering", "Finance", "Engineering" },
            Salary = new double?[] { 125000.50d, 110000.00d, null },
            Active = new[] { true, true, false }
        });
    }

    /// <summary>
    /// Executes SQLite setup or assertion SQL against a caller-owned connection.
    /// </summary>
    private static void ExecuteNonQuery(SqliteConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Executes a scalar SQLite query, optionally inside a caller-owned transaction.
    /// </summary>
    private static T ExecuteScalar<T>(SqliteConnection connection, string commandText, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        return (T)command.ExecuteScalar()!;
    }

    /// <summary>
    /// Executes a SQLite reader query and converts the first row with provider APIs.
    /// </summary>
    private static T ExecuteReaderValue<T>(SqliteConnection connection, string commandText, Func<SqliteDataReader, T> read)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return read(reader);
    }

    /// <summary>
    /// Reads SQLite result values into a typed array while preserving provider row and column order.
    /// </summary>
    private static T[] QueryColumn<T>(SqliteConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        using var reader = command.ExecuteReader();
        var values = new List<T>();
        while (reader.Read())
        {
            for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
            {
                values.Add((T)reader.GetValue(ordinal));
            }
        }

        return values.ToArray();
    }

    /// <summary>
    /// Reads a DataFrame column into object values while preserving row order for round-trip assertions.
    /// </summary>
    private static object?[] Values(global::Runiq.Data.DataFrame df, string columnName)
    {
        var column = df[columnName];
        return Enumerable.Range(0, column.Count).Select(column.GetValue).ToArray();
    }

    /// <summary>
    /// Creates a unique temporary database path without creating a machine-specific hard-coded location.
    /// </summary>
    private static string CreateTempDatabasePath()
    {
        return Path.Combine(Path.GetTempPath(), $"Runiq.Data.SqliteWrite.{Guid.NewGuid():N}.db");
    }

    /// <summary>
    /// Creates a SQLite connection string for a temporary file database without pooling file handles.
    /// </summary>
    private static string CreateFileConnectionString(string databasePath)
    {
        return new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString();
    }

    /// <summary>
    /// Removes a temporary SQLite database and possible WAL/SHM sidecar files on success or failure.
    /// </summary>
    private static void DeleteSqliteDatabaseFiles(string databasePath)
    {
        foreach (var path in new[] { databasePath, $"{databasePath}-wal", $"{databasePath}-shm" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
