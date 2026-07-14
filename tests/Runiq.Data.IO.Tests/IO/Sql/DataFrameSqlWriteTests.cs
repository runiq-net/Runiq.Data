using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Reflection;
using Runiq.Data.IO;
using Runiq.Data.IO.Tests.IO.Sql.TestDoubles;
using Runiq.Data.Schema;
using Runiq.Data.Series;

namespace Runiq.Data.IO.Tests.IO.Sql;

/// <summary>
/// Verifies SQL Write public API, validation, command generation, ownership, type mapping, and failure behavior.
/// </summary>
public sealed class DataFrameSqlWriteTests
{
    // Verifies that the SQL Write public API exposes only provider-independent ADO.NET contracts.
    [Fact]
    public void WriteSql_PublicApiSurfaceAndDependencies_AreProviderIndependent()
    {
        var methods = typeof(global::Runiq.Data.DataFrame).GetMethods()
            .Where(method => method.Name == "WriteSql")
            .Select(method => method.GetParameters().Select(parameter => parameter.ParameterType).ToArray())
            .ToArray();

        Assert.Contains(methods, parameters => parameters.SequenceEqual([typeof(DbConnection), typeof(string)]));
        Assert.Contains(methods, parameters => parameters.SequenceEqual([typeof(DbConnection), typeof(string), typeof(SqlWriteOptions)]));
        Assert.Equal(typeof(DbTransaction), typeof(SqlWriteOptions).GetProperty(nameof(SqlWriteOptions.Transaction))!.PropertyType);
        Assert.Equal(typeof(int?), typeof(SqlWriteOptions).GetProperty(nameof(SqlWriteOptions.CommandTimeout))!.PropertyType);

        var referencedAssemblies = typeof(global::Runiq.Data.DataFrame).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();
        Assert.DoesNotContain("Microsoft.Data.SqlClient", referencedAssemblies);
        Assert.DoesNotContain("Npgsql", referencedAssemblies);
        Assert.DoesNotContain("Microsoft.Data.Sqlite", referencedAssemblies);
        Assert.DoesNotContain("Dapper", referencedAssemblies);
        Assert.DoesNotContain("EntityFrameworkCore", referencedAssemblies);
    }

    // Verifies that newly added public SQL Write members are documented in source XML comments.
    [Fact]
    public void WriteSql_PublicApiDocumentation_IsPresent()
    {
        var dataFrameSource = ReadRepositoryFile("src", "Runiq.Data", "DataFrame", "Core", "DataFrame.cs");
        var optionsSource = ReadRepositoryFile("src", "Runiq.Data", "IO", "Sql", "SqlWriteOptions.cs");

        Assert.Contains("Appends the current DataFrame rows to an existing SQL table", dataFrameSource, StringComparison.Ordinal);
        Assert.Contains("public void WriteSql(DbConnection connection, string tableName)", dataFrameSource, StringComparison.Ordinal);
        Assert.Contains("public void WriteSql(DbConnection connection, string tableName, SqlWriteOptions options)", dataFrameSource, StringComparison.Ordinal);
        Assert.Contains("identifiers", dataFrameSource, StringComparison.Ordinal);
        Assert.Contains("internal transaction", dataFrameSource, StringComparison.Ordinal);
        Assert.Contains("external transaction", dataFrameSource, StringComparison.Ordinal);
        Assert.Contains("Configures provider-independent SQL append behavior", optionsSource, StringComparison.Ordinal);
        Assert.Contains("public DbTransaction? Transaction", optionsSource, StringComparison.Ordinal);
        Assert.Contains("public int? CommandTimeout", optionsSource, StringComparison.Ordinal);
    }

    // Verifies fail-fast validation for null connection, table name, options, and timeout values.
    [Fact]
    public void WriteSql_WithInvalidArguments_ThrowsBeforeDatabaseWork()
    {
        var df = CreateDataFrame("Id", [1]);
        var connection = new StubDbConnection(ConnectionState.Open);

        Assert.Throws<ArgumentNullException>(() => df.WriteSql(null!, "Employees"));
        Assert.Throws<ArgumentNullException>(() => df.WriteSql(connection, null!));
        Assert.ThrowsAny<ArgumentException>(() => df.WriteSql(connection, ""));
        Assert.ThrowsAny<ArgumentException>(() => df.WriteSql(connection, "   "));
        Assert.Throws<ArgumentNullException>(() => df.WriteSql(connection, "Employees", null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => df.WriteSql(connection, "Employees", new SqlWriteOptions { CommandTimeout = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => df.WriteSql(connection, "Employees", new SqlWriteOptions { CommandTimeout = -1 }));

        Assert.Empty(connection.CreatedCommands);
        Assert.Empty(connection.CreatedTransactions);
    }

    // Verifies that unsafe table identifiers are rejected without trimming, quoting, or aliasing.
    [Theory]
    [InlineData("Employees; DROP TABLE Users")]
    [InlineData("dbo..Employees")]
    [InlineData("dbo.Employees extra")]
    [InlineData("[Employees]")]
    [InlineData("\"Employees\"")]
    [InlineData("Employees-Archive")]
    [InlineData("database.schema.table")]
    [InlineData("Employees/*")]
    [InlineData("Employees--")]
    public void WriteSql_WithInvalidTableIdentifier_Throws(string tableName)
    {
        var df = CreateDataFrame("Id", [1]);

        var exception = Assert.ThrowsAny<ArgumentException>(() => df.WriteSql(new StubDbConnection(ConnectionState.Open), tableName));

        Assert.Contains(tableName, exception.Message, StringComparison.Ordinal);
    }

    // Verifies that unsafe column identifiers are rejected before opening a connection.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Employee Name")]
    [InlineData("Employee-Name")]
    [InlineData("[Employee]")]
    [InlineData("Employee--")]
    public void WriteSql_WithInvalidColumnIdentifier_Throws(string columnName)
    {
        var df = CreateRawDataFrame([new TestSeries(columnName, typeof(int), [1])]);
        var connection = new StubDbConnection(ConnectionState.Closed);

        var exception = Assert.ThrowsAny<ArgumentException>(() => df.WriteSql(connection, "Employees"));

        Assert.Contains("Column", exception.Message, StringComparison.Ordinal);
        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.Equal(0, connection.OpenCount);
    }

    // Verifies that unsupported schema types fail before command creation.
    [Fact]
    public void WriteSql_WithUnsupportedColumnType_ThrowsBeforeDatabaseWork()
    {
        var df = CreateRawDataFrame([new TestSeries("Metadata", typeof(object), [new UnsupportedSqlValue()])]);
        var connection = new StubDbConnection(ConnectionState.Open);

        var exception = Assert.Throws<ArgumentException>(() => df.WriteSql(connection, "Employees"));

        Assert.Contains("Metadata", exception.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported SQL Write data type", exception.Message, StringComparison.Ordinal);
        Assert.Empty(connection.CreatedCommands);
    }

    // Verifies that a zero-column DataFrame is rejected even though normal public creation prevents it.
    [Fact]
    public void WriteSql_WithZeroColumnDataFrame_Throws()
    {
        var df = CreateZeroColumnDataFrame();

        var exception = Assert.Throws<ArgumentException>(() => df.WriteSql(new StubDbConnection(ConnectionState.Open), "Employees"));

        Assert.Contains("at least one", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Verifies that a zero-row DataFrame validates identifiers but performs no database work.
    [Fact]
    public void WriteSql_WithZeroRows_IsNoOpWithoutOpeningConnection()
    {
        var df = CreateDataFrame("Id", Array.Empty<int>());
        var connection = new StubDbConnection(ConnectionState.Closed);

        df.WriteSql(connection, "dbo.Employees");

        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.Equal(0, connection.OpenCount);
        Assert.Empty(connection.CreatedCommands);
        Assert.Empty(connection.CreatedTransactions);
    }

    // Verifies generated SQL shape, identifier preservation, parameter names, timeout, and command reuse.
    [Fact]
    public void WriteSql_GeneratesSingleParameterizedInsertCommandAndReusesIt()
    {
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1, 2 }, Name = new[] { "Ada", "Grace" } });
        var connection = new StubDbConnection(ConnectionState.Open);

        df.WriteSql(connection, "dbo.Employees", new SqlWriteOptions { CommandTimeout = 42 });

        var command = Assert.Single(connection.CreatedCommands);
        Assert.Equal("INSERT INTO dbo.Employees (Id, Name) VALUES (@p0, @p1)", command.CommandText);
        Assert.DoesNotContain("Ada", command.CommandText, StringComparison.Ordinal);
        Assert.Equal(42, command.CommandTimeout);
        Assert.Equal(2, command.Parameters.Count);
        Assert.Equal("@p0", ((DbParameter)command.Parameters[0]).ParameterName);
        Assert.Equal("@p1", ((DbParameter)command.Parameters[1]).ParameterName);
        Assert.Equal(DbType.Int32, ((DbParameter)command.Parameters[0]).DbType);
        Assert.Equal(DbType.String, ((DbParameter)command.Parameters[1]).DbType);
        Assert.Equal(2, command.ExecuteNonQueryCount);
        Assert.Equal([1, "Ada"], command.ExecuteNonQueryParameterValues[0]);
        Assert.Equal([2, "Grace"], command.ExecuteNonQueryParameterValues[1]);
    }

    // Verifies a null CommandTimeout leaves the provider command default unchanged.
    [Fact]
    public void WriteSql_WithNullCommandTimeout_PreservesProviderDefault()
    {
        var connection = new StubDbConnection(ConnectionState.Open);

        CreateDataFrame("Id", [1]).WriteSql(connection, "Employees", new SqlWriteOptions());

        Assert.Equal(30, Assert.Single(connection.CreatedCommands).CommandTimeout);
    }

    // Verifies connection ownership for initially open and initially closed connections on success.
    [Fact]
    public void WriteSql_PreservesConnectionOwnershipOnSuccess()
    {
        var openConnection = new StubDbConnection(ConnectionState.Open);
        CreateDataFrame("Id", [1]).WriteSql(openConnection, "Employees");
        Assert.Equal(ConnectionState.Open, openConnection.State);
        Assert.Equal(0, openConnection.OpenCount);
        Assert.False(openConnection.WasDisposed);

        var closedConnection = new StubDbConnection(ConnectionState.Closed);
        CreateDataFrame("Id", [1]).WriteSql(closedConnection, "Employees");
        Assert.Equal(ConnectionState.Closed, closedConnection.State);
        Assert.Equal(1, closedConnection.OpenCount);
        Assert.Equal(1, closedConnection.CloseCount);
        Assert.False(closedConnection.WasDisposed);
    }

    // Verifies unsupported connection states fail before command or transaction creation.
    [Theory]
    [InlineData(ConnectionState.Broken)]
    [InlineData(ConnectionState.Connecting)]
    [InlineData(ConnectionState.Executing)]
    [InlineData(ConnectionState.Fetching)]
    public void WriteSql_WithInvalidConnectionState_Throws(ConnectionState state)
    {
        var connection = new StubDbConnection(state);

        var exception = Assert.Throws<InvalidOperationException>(() => CreateDataFrame("Id", [1]).WriteSql(connection, "Employees"));

        Assert.Contains(state.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Empty(connection.CreatedCommands);
    }

    // Verifies that default SQL Write uses one internal transaction and commits it only after success.
    [Fact]
    public void WriteSql_WithInternalTransaction_CommitsAndDisposesOnSuccess()
    {
        var connection = new StubDbConnection(ConnectionState.Open);

        CreateDataFrame("Id", [1, 2]).WriteSql(connection, "Employees");

        var transaction = Assert.Single(connection.CreatedTransactions);
        var command = Assert.Single(connection.CreatedCommands);
        Assert.Same(transaction, command.Transaction);
        Assert.Equal(1, transaction.CommitCount);
        Assert.Equal(0, transaction.RollbackCount);
        Assert.True(transaction.WasDisposed);
        Assert.True(command.WasDisposed);
    }

    // Verifies that a failed row rolls back internal work, disposes resources, and restores the connection.
    [Fact]
    public void WriteSql_WhenInternalExecutionFails_RollsBackDisposesAndRestoresConnection()
    {
        var connection = new StubDbConnection(ConnectionState.Closed) { FailExecuteNonQueryOnCall = 2 };

        var exception = Assert.Throws<DataException>(() => CreateDataFrame("Id", [1, 2, 3]).WriteSql(connection, "Employees"));

        var transaction = Assert.Single(connection.CreatedTransactions);
        var command = Assert.Single(connection.CreatedCommands);
        Assert.Contains("call 2", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, transaction.CommitCount);
        Assert.Equal(1, transaction.RollbackCount);
        Assert.True(transaction.WasDisposed);
        Assert.True(command.WasDisposed);
        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.Equal(1, connection.CloseCount);
        Assert.Equal(2, command.ExecuteNonQueryCount);
    }

    // Verifies that rollback cleanup failure does not hide the primary provider write failure.
    [Fact]
    public void WriteSql_WhenRollbackFails_PreservesPrimaryWriteFailure()
    {
        var connection = new StubDbConnection(ConnectionState.Open)
        {
            FailExecuteNonQueryOnCall = 1,
            RollbackException = new DataException("rollback failed")
        };

        var exception = Assert.Throws<DataException>(() => CreateDataFrame("Id", [1]).WriteSql(connection, "Employees"));

        Assert.Contains("ExecuteNonQuery failed", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, Assert.Single(connection.CreatedTransactions).RollbackCount);
    }

    // Verifies that external transactions are assigned but never committed, rolled back, or disposed.
    [Fact]
    public void WriteSql_WithExternalTransaction_PreservesCallerOwnership()
    {
        var connection = new StubDbConnection(ConnectionState.Open);
        var transaction = new StubDbTransaction(connection);

        CreateDataFrame("Id", [1]).WriteSql(connection, "Employees", new SqlWriteOptions { Transaction = transaction });

        var command = Assert.Single(connection.CreatedCommands);
        Assert.Same(transaction, command.Transaction);
        Assert.Empty(connection.CreatedTransactions);
        Assert.Equal(0, transaction.CommitCount);
        Assert.Equal(0, transaction.RollbackCount);
        Assert.False(transaction.WasDisposed);
    }

    // Verifies external transaction failures are propagated while transaction ownership remains with the caller.
    [Fact]
    public void WriteSql_WhenExternalTransactionExecutionFails_DoesNotRollbackOrDisposeTransaction()
    {
        var connection = new StubDbConnection(ConnectionState.Open) { FailExecuteNonQueryOnCall = 1 };
        var transaction = new StubDbTransaction(connection);

        var exception = Assert.Throws<DataException>(() => CreateDataFrame("Id", [1]).WriteSql(connection, "Employees", new SqlWriteOptions { Transaction = transaction }));

        Assert.Contains("ExecuteNonQuery failed", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, transaction.CommitCount);
        Assert.Equal(0, transaction.RollbackCount);
        Assert.False(transaction.WasDisposed);
        Assert.True(Assert.Single(connection.CreatedCommands).WasDisposed);
    }

    // Verifies that invalid external transaction ownership is rejected before command creation.
    [Fact]
    public void WriteSql_WithInvalidExternalTransaction_Throws()
    {
        var df = CreateDataFrame("Id", [1]);
        var connection = new StubDbConnection(ConnectionState.Open);
        var otherConnection = new StubDbConnection(ConnectionState.Open);
        var closedConnection = new StubDbConnection(ConnectionState.Closed);
        var noConnectionTransaction = new StubDbTransaction(connection) { ClearConnection = true };

        Assert.Throws<InvalidOperationException>(() => df.WriteSql(connection, "Employees", new SqlWriteOptions { Transaction = noConnectionTransaction }));
        Assert.Throws<InvalidOperationException>(() => df.WriteSql(connection, "Employees", new SqlWriteOptions { Transaction = new StubDbTransaction(otherConnection) }));
        Assert.Throws<InvalidOperationException>(() => df.WriteSql(closedConnection, "Employees", new SqlWriteOptions { Transaction = new StubDbTransaction(closedConnection) }));
    }

    // Verifies affected-row counts other than one fail and trigger internal rollback.
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-1)]
    public void WriteSql_WhenAffectedRowsIsNotOne_Throws(int affectedRows)
    {
        var connection = new StubDbConnection(ConnectionState.Open);
        connection.ExecuteNonQueryResults.Enqueue(affectedRows);

        var exception = Assert.Throws<InvalidOperationException>(() => CreateDataFrame("Id", [1]).WriteSql(connection, "Employees"));

        Assert.Contains($"reported {affectedRows}", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, Assert.Single(connection.CreatedTransactions).RollbackCount);
    }

    // Verifies every supported CLR column type receives the expected DbType mapping.
    [Theory]
    [MemberData(nameof(SupportedTypeMappings))]
    public void WriteSql_MapsSupportedClrTypesToDbType(Type dataType, object? value, DbType expectedDbType, object? expectedValue)
    {
        var df = CreateDataFrame("Value", dataType, [value]);
        var connection = new StubDbConnection(ConnectionState.Open);

        df.WriteSql(connection, "Employees");

        var command = Assert.Single(connection.CreatedCommands);
        Assert.Equal(expectedDbType, ((DbParameter)command.Parameters[0]).DbType);
        if (expectedValue is byte[] expectedBytes)
        {
            Assert.Equal(expectedBytes, (byte[])command.ExecuteNonQueryParameterValues[0][0]!);
        }
        else
        {
            Assert.Equal(expectedValue, command.ExecuteNonQueryParameterValues[0][0]);
        }
    }

    // Verifies null and non-null parameter values replace each other without leaking prior row values.
    [Fact]
    public void WriteSql_UpdatesParameterValuesForEveryRow()
    {
        var df = CreateDataFrame("Value", typeof(int?), [1, null, 3]);
        var connection = new StubDbConnection(ConnectionState.Open);

        df.WriteSql(connection, "Employees");

        var values = Assert.Single(connection.CreatedCommands).ExecuteNonQueryParameterValues;
        Assert.Equal(1, values[0][0]);
        Assert.Equal(DBNull.Value, values[1][0]);
        Assert.Equal(3, values[2][0]);
    }

    // Verifies byte arrays are snapshotted when assigned to provider parameters.
    [Fact]
    public void WriteSql_PassesByteArraySnapshotAsParameterValue()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var df = CreateDataFrame("Payload", [bytes]);
        var connection = new StubDbConnection(ConnectionState.Open);

        df.WriteSql(connection, "Files");
        bytes[0] = 9;

        Assert.Equal(new byte[] { 1, 2, 3 }, (byte[])Assert.Single(connection.CreatedCommands).ExecuteNonQueryParameterValues[0][0]!);
    }

    // Verifies unsupported runtime values fail with column name, row index, and actual CLR type.
    [Fact]
    public void WriteSql_WithUnsupportedRuntimeValue_ThrowsDiagnosticMessage()
    {
        var df = CreateRawDataFrame([new TestSeries("Metadata", typeof(string), ["ok", new UnsupportedSqlValue()])]);
        var connection = new StubDbConnection(ConnectionState.Open);

        var exception = Assert.Throws<ArgumentException>(() => df.WriteSql(connection, "Employees"));

        Assert.Contains("Metadata", exception.Message, StringComparison.Ordinal);
        Assert.Contains("row 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(UnsupportedSqlValue).FullName!, exception.Message, StringComparison.Ordinal);
    }

    // Verifies non-finite floating-point values are rejected instead of being sent to providers.
    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void WriteSql_WithNonFiniteFloatingPointValue_Throws(object value)
    {
        var df = CreateDataFrame("Value", value.GetType(), [value]);

        var exception = Assert.Throws<ArgumentException>(() => df.WriteSql(new StubDbConnection(ConnectionState.Open), "Employees"));

        Assert.Contains("Value", exception.Message, StringComparison.Ordinal);
        Assert.Contains("row 0", exception.Message, StringComparison.Ordinal);
    }

    // Verifies string-looking values remain strings and are never parsed into typed SQL values.
    [Fact]
    public void WriteSql_DoesNotParseStringLookingValues()
    {
        var values = new[] { "123", "true", "null", "2026-07-14" };
        var df = CreateDataFrame("Value", values);
        var connection = new StubDbConnection(ConnectionState.Open);

        df.WriteSql(connection, "Employees");

        var writtenValues = Assert.Single(connection.CreatedCommands).ExecuteNonQueryParameterValues;
        Assert.Equal(values, writtenValues.Select(static row => Assert.IsType<string>(row[0])).ToArray());
    }

    /// <summary>
    /// Provides supported SQL Write CLR values and their expected provider-independent DbType mappings.
    /// </summary>
    /// <returns>Supported value, DbType, and parameter value cases.</returns>
    public static TheoryData<Type, object?, DbType, object?> SupportedTypeMappings()
    {
        var guid = Guid.Parse("2f06c82a-02f7-4f97-9d97-3653ac75d18a");
        var bytes = new byte[] { 1, 2, 3 };
        return new TheoryData<Type, object?, DbType, object?>
        {
            { typeof(string), "text", DbType.String, "text" },
            { typeof(char), 'x', DbType.String, "x" },
            { typeof(bool), true, DbType.Boolean, true },
            { typeof(byte), (byte)1, DbType.Byte, (byte)1 },
            { typeof(sbyte), (sbyte)-1, DbType.Int16, (short)-1 },
            { typeof(short), (short)-2, DbType.Int16, (short)-2 },
            { typeof(ushort), (ushort)2, DbType.Int32, 2 },
            { typeof(int), -3, DbType.Int32, -3 },
            { typeof(uint), 3u, DbType.Int64, 3L },
            { typeof(long), -4L, DbType.Int64, -4L },
            { typeof(ulong), 4UL, DbType.Decimal, 4m },
            { typeof(float), 1.5f, DbType.Single, 1.5f },
            { typeof(double), 2.5d, DbType.Double, 2.5d },
            { typeof(decimal), 3.5m, DbType.Decimal, 3.5m },
            { typeof(DateTime), new DateTime(2024, 1, 2, 3, 4, 5), DbType.DateTime2, new DateTime(2024, 1, 2, 3, 4, 5) },
            { typeof(DateTimeOffset), new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero), DbType.DateTimeOffset, new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero) },
            { typeof(DateOnly), new DateOnly(2024, 1, 2), DbType.Date, new DateOnly(2024, 1, 2) },
            { typeof(TimeOnly), new TimeOnly(3, 4, 5), DbType.Time, new TimeOnly(3, 4, 5) },
            { typeof(TimeSpan), TimeSpan.FromMinutes(6), DbType.Time, TimeSpan.FromMinutes(6) },
            { typeof(Guid), guid, DbType.Guid, guid },
            { typeof(byte[]), bytes, DbType.Binary, bytes }
        };
    }

    private static global::Runiq.Data.DataFrame CreateDataFrame<T>(string columnName, IReadOnlyList<T> values)
    {
        return CreateDataFrame(columnName, typeof(T), values.Cast<object?>());
    }

    private static global::Runiq.Data.DataFrame CreateDataFrame(string columnName, Type dataType, IEnumerable<object?> values)
    {
        var series = typeof(Series<>)
            .MakeGenericType(dataType)
            .GetMethod(nameof(Series<int>.Create), BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [columnName, CreateTypedArray(dataType, values)])!;

        return CreateDataFrameFromSeries([(ISeries)series]);
    }

    private static Array CreateTypedArray(Type dataType, IEnumerable<object?> values)
    {
        var snapshot = values.ToArray();
        var array = Array.CreateInstance(dataType, snapshot.Length);
        for (var index = 0; index < snapshot.Length; index++)
        {
            array.SetValue(snapshot[index], index);
        }

        return array;
    }

    private static global::Runiq.Data.DataFrame CreateDataFrameFromSeries(IReadOnlyList<ISeries> series)
    {
        return (global::Runiq.Data.DataFrame)typeof(global::Runiq.Data.DataFrame)
            .GetMethod("CreateFromSeries", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [series])!;
    }

    private static global::Runiq.Data.DataFrame CreateZeroColumnDataFrame()
    {
        return CreateRawDataFrame([]);
    }

    private static global::Runiq.Data.DataFrame CreateRawDataFrame(IReadOnlyList<ISeries> series)
    {
        var schema = typeof(DataFrameSchema)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(ReadOnlyCollection<ColumnSchema>)], null)!
            .Invoke([Array.AsReadOnly(Array.Empty<ColumnSchema>())]);

        return (global::Runiq.Data.DataFrame)typeof(global::Runiq.Data.DataFrame)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(DataFrameSchema), typeof(ReadOnlyCollection<ISeries>)], null)!
            .Invoke([schema, Array.AsReadOnly(series.ToArray())]);
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Runiq.Data.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(new[] { directory!.FullName }.Concat(pathParts).ToArray()));
    }

    /// <summary>
    /// Represents a provider-specific value type that SQL Write must reject without string fallback.
    /// </summary>
    private sealed class UnsupportedSqlValue;

    /// <summary>
    /// Provides direct DataFrame column metadata and values for validation paths that public Series creation rejects earlier.
    /// </summary>
    private sealed class TestSeries : ISeries
    {
        private readonly IReadOnlyList<object?> values;

        internal TestSeries(string name, Type dataType, IReadOnlyList<object?> values)
        {
            Name = name;
            DataType = dataType;
            this.values = values;
        }

        public string Name { get; }

        public int Count => values.Count;

        public Type DataType { get; }

        public bool IsNullable => !DataType.IsValueType || Nullable.GetUnderlyingType(DataType) is not null;

        public object? GetValue(int index)
        {
            return values[index];
        }
    }
}
