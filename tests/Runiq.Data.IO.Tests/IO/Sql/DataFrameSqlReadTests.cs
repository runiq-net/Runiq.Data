using System.Data;
using System.Data.Common;
using Runiq.Data.IO.Tests.IO.Sql.TestDoubles;

namespace Runiq.Data.IO.Tests.IO.Sql;

/// <summary>
/// Verifies SQL Read validation, ownership, schema, type mapping, failure, and public API contracts.
/// </summary>
public sealed class DataFrameSqlReadTests
{
    // Verifies that the connection overload rejects a null connection before creating a command.
    [Fact]
    public void ReadSql_WithNullConnection_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => global::Runiq.Data.DataFrame.ReadSql((DbConnection)null!, "select 1"));
    }

    // Verifies that the command overload rejects a null command before any ownership-sensitive work.
    [Fact]
    public void ReadSql_WithNullCommand_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => global::Runiq.Data.DataFrame.ReadSql((DbCommand)null!));
    }

    // Verifies that null, empty, and whitespace command text values fail fast.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReadSql_WithInvalidCommandText_Throws(string? commandText)
    {
        var connection = new StubDbConnection(ConnectionState.Open);

        Assert.ThrowsAny<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadSql(connection, commandText!));

        var command = new StubDbCommand(connection) { CommandText = commandText! };
        Assert.ThrowsAny<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadSql(command));
    }

    // Verifies that a consumer command must have a connection before execution can begin.
    [Fact]
    public void ReadSql_WithCommandWithoutConnection_Throws()
    {
        var command = new StubDbCommand { CommandText = "select 1" };

        var exception = Assert.Throws<InvalidOperationException>(() => global::Runiq.Data.DataFrame.ReadSql(command));

        Assert.Contains("connection", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Verifies that an initially open consumer connection remains open and is not disposed.
    [Fact]
    public void ReadSql_WithInitiallyOpenConnection_LeavesConnectionOpen()
    {
        var connection = new StubDbConnection(ConnectionState.Open);

        global::Runiq.Data.DataFrame.ReadSql(connection, "select 1");

        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Equal(0, connection.OpenCount);
        Assert.Equal(0, connection.CloseCount);
        Assert.False(connection.WasDisposed);
    }

    // Verifies that an initially closed connection is opened temporarily and restored after success.
    [Fact]
    public void ReadSql_WithInitiallyClosedConnection_OpensAndClosesConnection()
    {
        var connection = new StubDbConnection(ConnectionState.Closed);

        global::Runiq.Data.DataFrame.ReadSql(connection, "select 1");

        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.Equal(1, connection.OpenCount);
        Assert.Equal(1, connection.CloseCount);
        Assert.False(connection.WasDisposed);
    }

    // Verifies that execution failure restores an initially closed connection.
    [Fact]
    public void ReadSql_WhenExecutionFails_RestoresClosedConnection()
    {
        var connection = new StubDbConnection(ConnectionState.Closed)
        {
            ExecuteException = new DataException("boom")
        };

        Assert.Throws<DataException>(() => global::Runiq.Data.DataFrame.ReadSql(connection, "select 1"));

        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.Equal(1, connection.CloseCount);
        Assert.False(connection.WasDisposed);
    }

    // Verifies that unsupported connection states fail deterministically before execution.
    [Theory]
    [InlineData(ConnectionState.Broken)]
    [InlineData(ConnectionState.Connecting)]
    [InlineData(ConnectionState.Executing)]
    [InlineData(ConnectionState.Fetching)]
    public void ReadSql_WithInvalidConnectionState_Throws(ConnectionState state)
    {
        var connection = new StubDbConnection(state);

        var exception = Assert.Throws<InvalidOperationException>(() => global::Runiq.Data.DataFrame.ReadSql(connection, "select 1"));

        Assert.Contains(state.ToString(), exception.Message, StringComparison.Ordinal);
    }

    // Verifies that a consumer-owned command and its mutable properties are not disposed or changed.
    [Fact]
    public void ReadSql_WithConsumerCommand_PreservesCommandOwnershipAndProperties()
    {
        var connection = new StubDbConnection(ConnectionState.Open);
        var transaction = new StubDbTransaction(connection);
        var parameter = new StubDbParameter { ParameterName = "@id", Value = 1 };
        var command = new StubDbCommand(connection)
        {
            CommandText = "dbo.GetValues",
            CommandTimeout = 123,
            CommandType = CommandType.StoredProcedure,
            StubTransaction = transaction
        };
        command.Parameters.Add(parameter);

        global::Runiq.Data.DataFrame.ReadSql(command);

        Assert.False(command.WasDisposed);
        Assert.Equal("dbo.GetValues", command.CommandText);
        Assert.Single(command.Parameters);
        Assert.Same(parameter, command.Parameters[0]);
        Assert.Same(transaction, command.Transaction);
        Assert.Equal(123, command.CommandTimeout);
        Assert.Equal(CommandType.StoredProcedure, command.CommandType);
        Assert.Same(connection, command.Connection);
    }

    // Verifies that the connection overload disposes only the command it creates internally.
    [Fact]
    public void ReadSql_WithConnectionOverload_DisposesInternalCommandOnly()
    {
        var connection = new StubDbConnection(ConnectionState.Open);

        global::Runiq.Data.DataFrame.ReadSql(connection, "select 1");

        Assert.NotNull(connection.LastCreatedCommand);
        Assert.True(connection.LastCreatedCommand.WasDisposed);
        Assert.False(connection.WasDisposed);
    }

    // Verifies that readers are disposed after successful reads and every requested failure path.
    [Fact]
    public void ReadSql_DisposesReaderOnSuccessAndFailures()
    {
        var successReader = StubDbDataReader.Create([("Value", typeof(int))], [[1]]);
        global::Runiq.Data.DataFrame.ReadSql(new StubDbCommand(new StubDbConnection(ConnectionState.Open)) { CommandText = "select 1", Reader = successReader });
        Assert.True(successReader.WasDisposed);

        var schemaReader = StubDbDataReader.Create([("", typeof(int))], [[1]]);
        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadSql(new StubDbCommand(new StubDbConnection(ConnectionState.Open)) { CommandText = "select 1", Reader = schemaReader }));
        Assert.True(schemaReader.WasDisposed);

        var unsupportedReader = StubDbDataReader.Create([("Value", typeof(object))], [[new object()]]);
        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadSql(new StubDbCommand(new StubDbConnection(ConnectionState.Open)) { CommandText = "select 1", Reader = unsupportedReader }));
        Assert.True(unsupportedReader.WasDisposed);

        var readFailureReader = StubDbDataReader.Create([("Value", typeof(int))], [[1]]);
        readFailureReader.ReadException = new DataException("read failed");
        Assert.Throws<DataException>(() => global::Runiq.Data.DataFrame.ReadSql(new StubDbCommand(new StubDbConnection(ConnectionState.Open)) { CommandText = "select 1", Reader = readFailureReader }));
        Assert.True(readFailureReader.WasDisposed);

        var multipleReader = StubDbDataReader.Create([("Value", typeof(int))], [[1]]);
        multipleReader.HasSecondResult = true;
        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadSql(new StubDbCommand(new StubDbConnection(ConnectionState.Open)) { CommandText = "select 1", Reader = multipleReader }));
        Assert.True(multipleReader.WasDisposed);
    }

    // Verifies that column ordinals, exact names, and casing are preserved.
    [Fact]
    public void ReadSql_PreservesColumnOrderNamesAndCasing()
    {
        var reader = StubDbDataReader.Create(
            [("Id", typeof(int)), ("display Name", typeof(string)), ("URL", typeof(string))],
            [[1, "Ada", "https://example.test"]]);

        var df = Read(reader);

        Assert.Equal(["Id", "display Name", "URL"], df.Schema.Columns.Select(column => column.Name));
        Assert.Equal("Ada", df["display Name"].GetValue(0));
    }

    // Verifies that invalid and duplicate provider column names are rejected without alias generation.
    [Theory]
    [MemberData(nameof(InvalidColumnSchemas))]
    public void ReadSql_WithInvalidColumnNames_Throws(IReadOnlyList<(string Name, Type Type)> columns)
    {
        var reader = StubDbDataReader.Create(columns, [[1, 2]]);

        Assert.Throws<ArgumentException>(() => Read(reader));
    }

    // Verifies that empty result sets preserve metadata schema and produce zero rows.
    [Fact]
    public void ReadSql_WithEmptyResult_PreservesSchemaAndCreatesZeroRows()
    {
        var reader = StubDbDataReader.Create([("Id", typeof(int)), ("Name", typeof(string))], []);

        var df = Read(reader);

        Assert.Equal(2, df.Schema.Count);
        Assert.Equal(0, df["Id"].Count);
        Assert.Equal(typeof(int), df.Schema.GetColumn("Id").DataType);
        Assert.Equal(typeof(string), df.Schema.GetColumn("Name").DataType);
    }

    // Verifies that zero-column results are rejected as non-tabular results.
    [Fact]
    public void ReadSql_WithZeroColumnResult_Throws()
    {
        var reader = StubDbDataReader.Create([], []);

        var exception = Assert.Throws<ArgumentException>(() => Read(reader));

        Assert.Contains("tabular result set", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Verifies that row order is exactly the order returned by the reader.
    [Fact]
    public void ReadSql_PreservesRowOrder()
    {
        var reader = StubDbDataReader.Create([("Id", typeof(int))], [[3], [1], [2]]);

        var df = Read(reader);

        Assert.Equal([3, 1, 2], Enumerable.Range(0, df["Id"].Count).Select(row => df["Id"].GetValue(row)));
    }

    // Verifies that one-row and multi-row result sets are read without requiring special cases.
    [Fact]
    public void ReadSql_ReadsSingleAndMultipleRows()
    {
        Assert.Equal(1, Read(StubDbDataReader.Create([("Id", typeof(int))], [[10]]))["Id"].Count);
        Assert.Equal(2, Read(StubDbDataReader.Create([("Id", typeof(int))], [[10], [11]]))["Id"].Count);
    }

    // Verifies that DBNull is mapped to null and does not escape into DataFrame values.
    [Fact]
    public void ReadSql_MapsDbNullToNull()
    {
        var reader = StubDbDataReader.Create([("Value", typeof(int))], [[DBNull.Value], [1]]);

        var df = Read(reader);

        Assert.Null(df["Value"].GetValue(0));
        Assert.Equal(1, df["Value"].GetValue(1));
        Assert.Equal(typeof(int?), df.Schema.GetColumn("Value").DataType);
    }

    // Verifies that every supported CLR value type is preserved without parsing or normalization.
    [Fact]
    public void ReadSql_PreservesSupportedClrTypes()
    {
        var values = new object?[]
        {
            "001",
            'x',
            true,
            (byte)1,
            (sbyte)-1,
            (short)-2,
            (ushort)2,
            -3,
            3u,
            -4L,
            4UL,
            1.5f,
            2.5d,
            3.5m,
            new DateTime(2024, 1, 2, 3, 4, 5),
            new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero),
            new DateOnly(2024, 1, 2),
            new TimeOnly(3, 4, 5),
            TimeSpan.FromMinutes(6),
            Guid.Parse("2f06c82a-02f7-4f97-9d97-3653ac75d18a"),
            new byte[] { 1, 2, 3 }
        };
        var columns = values.Select((value, index) => ($"C{index}", value!.GetType())).ToArray();
        var reader = StubDbDataReader.Create(columns, [values]);

        var df = Read(reader);

        for (var index = 0; index < values.Length; index++)
        {
            Assert.Equal(values[index], df[$"C{index}"].GetValue(0));
            Assert.Equal(values[index]!.GetType(), df.Schema.GetColumn($"C{index}").DataType);
        }

        Assert.IsType<string>(df["C0"].GetValue(0));
        Assert.IsType<byte[]>(df["C20"].GetValue(0));
    }

    // Verifies that byte arrays are snapshotted so later provider-buffer mutation cannot affect the DataFrame.
    [Fact]
    public void ReadSql_CopiesByteArrays()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var df = Read(StubDbDataReader.Create([("Payload", typeof(byte[]))], [[bytes]]));

        bytes[0] = 9;

        Assert.Equal(new byte[] { 1, 2, 3 }, (byte[])df["Payload"].GetValue(0)!);
    }

    // Verifies that unsupported values fail with column name, zero-based row index, and CLR type.
    [Fact]
    public void ReadSql_WithUnsupportedValueType_ThrowsDiagnosticMessage()
    {
        var reader = StubDbDataReader.Create([("Metadata", typeof(string))], [["ok"], [new UnsupportedSqlValue()]]);

        var exception = Assert.Throws<ArgumentException>(() => Read(reader));

        Assert.Contains("Metadata", exception.Message, StringComparison.Ordinal);
        Assert.Contains("row 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(UnsupportedSqlValue).FullName!, exception.Message, StringComparison.Ordinal);
    }

    // Verifies that supported values are not silently converted to strings.
    [Fact]
    public void ReadSql_DoesNotSilentlyConvertValuesToString()
    {
        var df = Read(StubDbDataReader.Create([("Value", typeof(int))], [[42]]));

        Assert.IsType<int>(df["Value"].GetValue(0));
    }

    // Verifies that command execution and row read failures are propagated without returning a DataFrame.
    [Fact]
    public void ReadSql_PropagatesExecutionAndReadFailures()
    {
        var command = new StubDbCommand(new StubDbConnection(ConnectionState.Open))
        {
            CommandText = "select 1",
            ExecuteException = new DataException("execute failed")
        };
        Assert.Throws<DataException>(() => global::Runiq.Data.DataFrame.ReadSql(command));

        var reader = StubDbDataReader.Create([("Value", typeof(int))], [[1]]);
        reader.GetValueException = new DataException("row failed");
        Assert.Throws<DataException>(() => Read(reader));
        Assert.True(reader.WasDisposed);
    }

    // Verifies that extra result sets are rejected rather than silently ignored.
    [Fact]
    public void ReadSql_WithMultipleResultSets_Throws()
    {
        var reader = StubDbDataReader.Create([("Value", typeof(int))], [[1]]);
        reader.HasSecondResult = true;

        var exception = Assert.Throws<ArgumentException>(() => Read(reader));

        Assert.Contains("multiple result sets", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Verifies that the public SQL API surface exists and provider-specific production references were not added.
    [Fact]
    public void ReadSql_PublicApiSurfaceAndDependencies_AreProviderIndependent()
    {
        var methods = typeof(global::Runiq.Data.DataFrame).GetMethods()
            .Where(method => method.Name == "ReadSql")
            .Select(method => method.GetParameters().Select(parameter => parameter.ParameterType).ToArray())
            .ToArray();

        Assert.Contains(methods, parameters => parameters.SequenceEqual([typeof(DbConnection), typeof(string)]));
        Assert.Contains(methods, parameters => parameters.SequenceEqual([typeof(DbCommand)]));

        var referencedAssemblies = typeof(global::Runiq.Data.DataFrame).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();
        Assert.DoesNotContain("Microsoft.Data.SqlClient", referencedAssemblies);
        Assert.DoesNotContain("Npgsql", referencedAssemblies);
        Assert.DoesNotContain("Microsoft.Data.Sqlite", referencedAssemblies);
    }

    /// <summary>
    /// Provides invalid SQL column metadata cases that must fail before DataFrame creation.
    /// </summary>
    /// <returns>Column metadata containing empty, whitespace, or duplicate names.</returns>
    public static TheoryData<IReadOnlyList<(string Name, Type Type)>> InvalidColumnSchemas()
    {
        return new TheoryData<IReadOnlyList<(string Name, Type Type)>>
        {
            new[] { ("", typeof(int)), ("Other", typeof(int)) },
            new[] { ("   ", typeof(int)), ("Other", typeof(int)) },
            new[] { ("Id", typeof(int)), ("id", typeof(int)) }
        };
    }

    private static global::Runiq.Data.DataFrame Read(StubDbDataReader reader)
    {
        // The helper uses a caller-owned command so tests exercise the public command overload
        // while still controlling the exact reader returned by ExecuteReader.
        var connection = new StubDbConnection(ConnectionState.Open);
        var command = new StubDbCommand(connection)
        {
            CommandText = "select 1",
            Reader = reader
        };

        return global::Runiq.Data.DataFrame.ReadSql(command);
    }

    /// <summary>
    /// Represents a provider-specific value type that SQL Read must reject without string fallback.
    /// </summary>
    private sealed class UnsupportedSqlValue;
}
