using System.Collections.ObjectModel;
using System.Data;
using System.Reflection;
using Npgsql;
using Runiq.Data.IO;
using Runiq.Data.Schema;
using Runiq.Data.Series;

namespace Runiq.Data.PostgreSql.IntegrationTests;

/// <summary>
/// Verifies SQL Write append, transaction, ownership, type-mapping, and failure contracts against a shared PostgreSQL container.
/// </summary>
/// <remarks>
/// The PostgreSQL collection starts one Testcontainers PostgreSQL instance for all provider
/// contract tests. Each test creates a lowercase generated schema, places every object inside
/// that schema, and drops the schema with CASCADE during cleanup so tests never depend on order
/// or shared database state.
/// </remarks>
[Collection(PostgreSqlCollection.Name)]
public sealed class DataFramePostgreSqlWriteTests(PostgreSqlContainerFixture fixture)
{
    // Verifies default append behavior, INSERT column order, SQL NULL mapping, and DataFrame immutability.
    [Fact]
    public void WriteSql_WithDefaultOptions_AppendsEmployeesAndDoesNotMutateDataFrame()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        schema.CreateEmployees();
        var df = CreateEmployeesDataFrame();
        var snapshot = Snapshot(df);

        df.WriteSql(connection, schema.Employees);

        AssertEmployees(connection, schema.Employees);
        AssertDataFrameSnapshot(df, snapshot);
    }

    // Verifies existing PostgreSQL rows remain unchanged while SQL Write appends new rows.
    [Fact]
    public void WriteSql_WithExistingRows_PreservesRowsAndAppends()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("existing_rows", "id integer NOT NULL, name text NOT NULL");
        schema.Execute($"INSERT INTO {table} (id, name) VALUES (10, 'Existing');");

        global::Runiq.Data.DataFrame.Create(new { id = new[] { 11, 12 }, name = new[] { "NewA", "NewB" } })
            .WriteSql(connection, table);

        Assert.Equal([[10, "Existing"], [11, "NewA"], [12, "NewB"]], QueryRows(connection, $"SELECT id, name FROM {table} ORDER BY id"));
    }

    // Verifies the internally owned transaction rolls back all appended rows after a real PostgreSQL constraint failure.
    [Fact]
    public void WriteSql_WithInternalTransactionFailure_RollsBackAtomicallyAndKeepsConnectionUsable()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("atomic_rows", "id integer NOT NULL PRIMARY KEY, name text NOT NULL");
        schema.Execute($"INSERT INTO {table} (id, name) VALUES (1, 'Original');");
        var df = global::Runiq.Data.DataFrame.Create(new { id = new[] { 2, 3, 1 }, name = new[] { "A", "B", "Duplicate" } });

        var exception = Assert.Throws<PostgresException>(() => df.WriteSql(connection, table));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        Assert.Equal([[1, "Original"]], QueryRows(connection, $"SELECT id, name FROM {table} ORDER BY id"));
        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Equal(1, CountRows(connection, table));
    }

    // Verifies a successful internal transaction is committed by observing persistence from a separate connection.
    [Fact]
    public void WriteSql_WithInternalTransactionSuccess_CommitsRowsVisibleToNewConnection()
    {
        string schemaName;
        string table;
        using (var setupConnection = fixture.CreateConnection())
        {
            setupConnection.Open();
            using var schema = PostgreSqlWriteSchema.Create(setupConnection, dropOnDispose: false);
            schemaName = schema.Name;
            table = schema.CreateTable("committed_rows", "id integer NOT NULL, name text NOT NULL");
            global::Runiq.Data.DataFrame.Create(new { id = new[] { 1, 2 }, name = new[] { "One", "Two" } })
                .WriteSql(setupConnection, table);
        }

        using var verificationConnection = fixture.CreateConnection();
        verificationConnection.Open();
        using var cleanupSchema = new PostgreSqlWriteSchema(verificationConnection, schemaName);
        Assert.Equal([[1, "One"], [2, "Two"]], QueryRows(verificationConnection, $"SELECT id, name FROM {table} ORDER BY id"));
    }

    // Verifies external transactions remain caller-owned and can be explicitly rolled back after SQL Write returns.
    [Fact]
    public void WriteSql_WithExternalTransaction_AllowsCallerRollback()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("external_rollback", "id integer NOT NULL, name text NOT NULL");
        using var transaction = connection.BeginTransaction();

        global::Runiq.Data.DataFrame.Create(new { id = new[] { 1, 2 }, name = new[] { "One", "Two" } })
            .WriteSql(connection, table, new SqlWriteOptions { Transaction = transaction });

        Assert.Equal(2, CountRows(connection, transaction, table));
        transaction.Rollback();
        Assert.Equal(0, CountRows(connection, table));
    }

    // Verifies external transaction commits are performed only by the caller and persist to a new connection.
    [Fact]
    public void WriteSql_WithExternalTransaction_AllowsCallerCommit()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("external_commit", "id integer NOT NULL, name text NOT NULL");

        using (var transaction = connection.BeginTransaction())
        {
            global::Runiq.Data.DataFrame.Create(new { id = new[] { 1 }, name = new[] { "Committed" } })
                .WriteSql(connection, table, new SqlWriteOptions { Transaction = transaction });
            transaction.Commit();
        }

        using var verificationConnection = fixture.CreateConnection();
        verificationConnection.Open();
        Assert.Equal([[1, "Committed"]], QueryRows(verificationConnection, $"SELECT id, name FROM {table};"));
    }

    // Verifies PostgreSQL failed-transaction semantics: Runiq.Data leaves the aborted transaction for caller rollback.
    [Fact]
    public void WriteSql_WithExternalTransactionFailure_LeavesAbortedTransactionForCallerRollback()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("external_failure", "id integer NOT NULL PRIMARY KEY, name text NOT NULL");
        using var transaction = connection.BeginTransaction();
        ExecuteNonQuery(connection, transaction, $"INSERT INTO {table} (id, name) VALUES (1, 'Original');");
        var df = global::Runiq.Data.DataFrame.Create(new { id = new[] { 2, 1 }, name = new[] { "BeforeFailure", "Duplicate" } });

        Assert.Throws<PostgresException>(() => df.WriteSql(connection, table, new SqlWriteOptions { Transaction = transaction }));
        Assert.Throws<PostgresException>(() => CountRows(connection, transaction, table));

        transaction.Rollback();
        Assert.Equal(0, CountRows(connection, table));
    }

    // Verifies a caller-opened NpgsqlConnection remains open and reusable after SQL Write completes.
    [Fact]
    public void WriteSql_WithOpenConnection_LeavesConnectionOpenAndReusable()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("open_connection", "id integer NOT NULL");

        global::Runiq.Data.DataFrame.Create(new { id = new[] { 1 } }).WriteSql(connection, table);

        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Equal(1, CountRows(connection, table));
    }

    // Verifies a caller-closed NpgsqlConnection is opened temporarily, restored to Closed, and remains reusable.
    [Fact]
    public void WriteSql_WithClosedConnection_RestoresClosedStateAndRemainsReusable()
    {
        string schemaName;
        string table;
        using (var setupConnection = fixture.CreateConnection())
        {
            setupConnection.Open();
            using var schema = PostgreSqlWriteSchema.Create(setupConnection, dropOnDispose: false);
            schemaName = schema.Name;
            table = schema.CreateTable("closed_connection", "id integer NOT NULL");
        }

        using var connection = fixture.CreateConnection();
        try
        {
            global::Runiq.Data.DataFrame.Create(new { id = new[] { 1, 2 } }).WriteSql(connection, table);

            Assert.Equal(ConnectionState.Closed, connection.State);
            connection.Open();
            Assert.Equal(2, CountRows(connection, table));
        }
        finally
        {
            EnsureOpen(connection);
            using var cleanupSchema = new PostgreSqlWriteSchema(connection, schemaName);
        }
    }

    // Verifies missing-table provider diagnostics are preserved and closed connection ownership is restored after failure.
    [Fact]
    public void WriteSql_WithMissingTable_ThrowsPostgresExceptionAndRestoresConnection()
    {
        string schemaName;
        using (var setupConnection = fixture.CreateConnection())
        {
            setupConnection.Open();
            using var schema = PostgreSqlWriteSchema.Create(setupConnection, dropOnDispose: false);
            schemaName = schema.Name;
        }

        using var connection = fixture.CreateConnection();
        try
        {
            var table = PostgreSqlWriteSchema.Qualified(schemaName, "missing_employees");

            var exception = Assert.Throws<PostgresException>(() => global::Runiq.Data.DataFrame.Create(new { id = new[] { 1 } }).WriteSql(connection, table));

            Assert.Equal(PostgresErrorCodes.UndefinedTable, exception.SqlState);
            Assert.Equal(ConnectionState.Closed, connection.State);
            connection.Open();
            Assert.Equal(ConnectionState.Open, connection.State);
        }
        finally
        {
            EnsureOpen(connection);
            using var cleanupSchema = new PostgreSqlWriteSchema(connection, schemaName);
        }
    }

    // Verifies PostgreSQL constraint failures propagate provider diagnostics and internal rollback leaves no partial append.
    [Fact]
    public void WriteSql_WithConstraintFailure_ThrowsPostgresExceptionRollsBackAndConnectionIsReusable()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("constraint_failure", "id integer NOT NULL PRIMARY KEY");
        var df = global::Runiq.Data.DataFrame.Create(new { id = new[] { 1, 2, 1 } });

        var exception = Assert.Throws<PostgresException>(() => df.WriteSql(connection, table));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        Assert.Equal(0, CountRows(connection, table));
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    // Verifies a generated schema-qualified table name is preserved and works on PostgreSQL.
    [Fact]
    public void WriteSql_WithSchemaQualifiedTableName_AppendsRows()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("employees", "id integer NOT NULL, name text NOT NULL");

        global::Runiq.Data.DataFrame.Create(new { id = new[] { 1 }, name = new[] { "Schema" } }).WriteSql(connection, table);

        Assert.Equal([[1, "Schema"]], QueryRows(connection, $"SELECT id, name FROM {table};"));
    }

    // Verifies unsafe table identifiers are rejected before provider execution and leave real PostgreSQL tables unchanged.
    [Fact]
    public void WriteSql_WithInvalidIdentifier_RejectsBeforeProviderCallAndLeavesTableUnchanged()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("safe_employees", "id integer NOT NULL");

        var exception = Assert.ThrowsAny<ArgumentException>(() => global::Runiq.Data.DataFrame.Create(new { id = new[] { 1 } })
            .WriteSql(connection, "employees;drop_table"));

        Assert.Contains("simple SQL identifiers", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, CountRows(connection, table));
    }

    // Verifies an empty DataFrame is a no-op for a valid missing schema-qualified name and closed connection.
    [Fact]
    public void WriteSql_WithEmptyDataFrame_IsNoOpWithoutOpeningConnectionOrCheckingTable()
    {
        using var connection = fixture.CreateConnection();
        var schema = PostgreSqlWriteSchema.UniqueSchemaName();
        var table = PostgreSqlWriteSchema.Qualified(schema, "missing_employees");
        var df = global::Runiq.Data.DataFrame.Create(new { id = Array.Empty<int>() });

        df.WriteSql(connection, table);

        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    // Verifies SQL NULL and explicit-null default behavior against real PostgreSQL columns.
    [Fact]
    public void WriteSql_WithNullValues_WritesSqlNullAndDoesNotApplyColumnDefault()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("null_defaults", "id integer NOT NULL, optional_text text NULL DEFAULT 'default-value'");
        var df = CreateDataFrame("id", typeof(int), [1], "optional_text", typeof(string), [null]);

        df.WriteSql(connection, table);

        Assert.Equal(DBNull.Value, ReadScalar<object>(connection, $"SELECT optional_text FROM {table} WHERE id = 1;"));
    }

    // Verifies string-looking values and Unicode text are preserved as text without parsing or formula handling.
    [Fact]
    public void WriteSql_WithStringValues_PreservesExactText()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("string_values", "id integer NOT NULL, value text NOT NULL");
        var values = new[] { "123", "true", "null", "2026-07-14", "=SUM(A1:A2)", "Ay\u015fe" };

        global::Runiq.Data.DataFrame.Create(new { id = Enumerable.Range(1, values.Length).ToArray(), value = values }).WriteSql(connection, table);

        Assert.Equal(values, QueryColumn<string>(connection, $"SELECT value FROM {table} ORDER BY id;"));
    }

    // Verifies char values are written as single-character text values.
    [Fact]
    public void WriteSql_WithCharValue_WritesSingleCharacterText()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("char_values", "value char(1) NOT NULL");

        global::Runiq.Data.DataFrame.Create(new { value = new[] { 'Z' } }).WriteSql(connection, table);

        Assert.Equal("Z", ReadScalar<string>(connection, $"SELECT value FROM {table};"));
    }

    // Verifies signed, unsigned-widened, decimal, boolean, and floating-point mappings against PostgreSQL native columns.
    [Fact]
    public void WriteSql_WithNumericAndBooleanValues_WritesExpectedPostgreSqlValues()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable(
            "numeric_types",
            """
            id integer NOT NULL,
            bool_value boolean NOT NULL,
            sbyte_value smallint NOT NULL,
            byte_value smallint NOT NULL,
            short_value smallint NOT NULL,
            ushort_value integer NOT NULL,
            int_value integer NOT NULL,
            uint_value bigint NOT NULL,
            long_value bigint NOT NULL,
            decimal_value numeric(18,2) NOT NULL,
            float_value real NOT NULL,
            double_value double precision NOT NULL
            """);

        global::Runiq.Data.DataFrame.Create(new
        {
            id = new[] { 1, 2 },
            bool_value = new[] { true, false },
            sbyte_value = new sbyte[] { -8, 7 },
            byte_value = new byte[] { 250, 7 },
            short_value = new short[] { -123, 456 },
            ushort_value = new ushort[] { 65000, 42 },
            int_value = new[] { -123456, 654321 },
            uint_value = new uint[] { 4000000000u, 12u },
            long_value = new[] { -9000000000L, 9000000000L },
            decimal_value = new[] { 12345.67m, 76543.21m },
            float_value = new[] { 1.25f, 2.5f },
            double_value = new[] { 3.125d, 6.25d }
        }).WriteSql(connection, table);

        var rows = QueryRows(connection, $"SELECT bool_value, sbyte_value, byte_value, short_value, ushort_value, int_value, uint_value, long_value, decimal_value, float_value, double_value FROM {table} ORDER BY id;");
        Assert.Equal([true, (short)-8, (short)250, (short)-123, 65000, -123456, 4000000000L, -9000000000L, 12345.67m], rows[0].Take(9).ToArray());
        Assert.InRange((float)rows[0][9]!, 1.2499f, 1.2501f);
        Assert.InRange((double)rows[0][10]!, 3.124999d, 3.125001d);
        Assert.Equal([false, (short)7, (short)7, (short)456, 42, 654321, 12L, 9000000000L, 76543.21m], rows[1].Take(9).ToArray());
    }

    // Verifies ulong values widened to decimal are stored losslessly in PostgreSQL numeric(20,0).
    [Fact]
    public void WriteSql_WithUlongValues_PreservesFullUnsignedRange()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("ulong_values", "id integer NOT NULL, value numeric(20,0) NOT NULL");
        var values = new[] { 0UL, 9223372036854775808UL, ulong.MaxValue };

        global::Runiq.Data.DataFrame.Create(new { id = new[] { 1, 2, 3 }, value = values }).WriteSql(connection, table);

        Assert.Equal([0m, 9223372036854775808m, 18446744073709551615m], QueryColumn<decimal>(connection, $"SELECT value FROM {table} ORDER BY id;"));
    }

    // Verifies timestamp, date, time, and TimeSpan mappings using Npgsql's real precision and Kind behavior.
    [Fact]
    public void WriteSql_WithDateAndTimeValues_PreservesPostgreSqlTemporalContracts()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable(
            "date_time_types",
            """
            timestamp_value timestamp without time zone NOT NULL,
            date_value date NOT NULL,
            time_value time without time zone NOT NULL,
            timespan_value time without time zone NOT NULL
            """);
        var timestamp = new DateTime(2026, 7, 14, 10, 15, 30, 123, DateTimeKind.Unspecified).AddTicks(4560);
        var date = new DateOnly(2026, 7, 14);
        var time = new TimeOnly(10, 15, 30, 123).Add(TimeSpan.FromTicks(4560));
        var timeSpan = new TimeSpan(10, 15, 30).Add(TimeSpan.FromMilliseconds(123)).Add(TimeSpan.FromTicks(4560));

        global::Runiq.Data.DataFrame.Create(new
        {
            timestamp_value = new[] { timestamp },
            date_value = new[] { date },
            time_value = new[] { time },
            timespan_value = new[] { timeSpan }
        }).WriteSql(connection, table);

        var row = Assert.Single(QueryRows(connection, $"SELECT timestamp_value, date_value, time_value, timespan_value FROM {table};"));
        var readTimestamp = Assert.IsType<DateTime>(row[0]);
        Assert.Equal(timestamp, readTimestamp);
        Assert.Equal(DateTimeKind.Unspecified, readTimestamp.Kind);
        Assert.Equal(date, row[1]);
        Assert.Equal(time, row[2]);
        Assert.Equal(TimeOnly.FromTimeSpan(timeSpan), row[3]);
    }

    // Verifies DateTimeOffset is written to timestamptz by instant, not by preserving the original offset.
    [Fact]
    public void WriteSql_WithDateTimeOffsetValue_PreservesInstantAsUtcTimestamptz()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("timestamptz_values", "value timestamp with time zone NOT NULL");
        var instant = new DateTimeOffset(2026, 7, 14, 7, 15, 30, 123, TimeSpan.Zero).AddTicks(4560);

        global::Runiq.Data.DataFrame.Create(new { value = new[] { instant } }).WriteSql(connection, table);

        var readValue = Assert.IsType<DateTime>(ReadScalar<object>(connection, $"SELECT value FROM {table};"));
        Assert.Equal(instant.UtcDateTime, readValue);
        Assert.Equal(DateTimeKind.Utc, readValue.Kind);
    }

    // Verifies Guid and byte-array values are written without changing identity or binary sequence.
    [Fact]
    public void WriteSql_WithGuidAndBinaryValues_PreservesValues()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("guid_binary", "id uuid NOT NULL, payload bytea NOT NULL");
        var guid = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var bytes = new byte[] { 1, 2, 3, 4, 5 };

        global::Runiq.Data.DataFrame.Create(new { id = new[] { guid }, payload = new[] { bytes } }).WriteSql(connection, table);
        bytes[0] = 9;

        var row = Assert.Single(QueryRows(connection, $"SELECT id, payload FROM {table};"));
        Assert.Equal(guid, row[0]);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, (byte[])row[1]!);
    }

    // Verifies non-finite double values are rejected by Runiq.Data even though PostgreSQL floating types can represent them.
    [Fact]
    public void WriteSql_WithNonFiniteDoubleValues_RejectsBeforePartialAppend()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("non_finite", "id integer NOT NULL, value double precision NOT NULL");
        var df = global::Runiq.Data.DataFrame.Create(new { id = new[] { 1, 2, 3 }, value = new[] { 1.0d, double.NaN, double.PositiveInfinity } });

        var exception = Assert.Throws<ArgumentException>(() => df.WriteSql(connection, table));

        Assert.Contains("non-finite", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, CountRows(connection, table));
    }

    // Verifies unsupported custom runtime values fail with diagnostics and without using ToString fallback.
    [Fact]
    public void WriteSql_WithUnsupportedRuntimeValue_RejectsBeforeDatabaseMutation()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("unsupported_value", "id integer NOT NULL, metadata text NOT NULL");
        var df = CreateRawDataFrame([new TestSeries("id", typeof(int), [1, 2]), new TestSeries("metadata", typeof(string), ["ok", new UnsupportedSqlValue()])]);

        var exception = Assert.Throws<ArgumentException>(() => df.WriteSql(connection, table));

        Assert.Contains("metadata", exception.Message, StringComparison.Ordinal);
        Assert.Contains("row 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(UnsupportedSqlValue).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, CountRows(connection, table));
    }

    // Verifies PostgreSQL array destination support is not implicitly enabled for non-byte array cells.
    [Fact]
    public void WriteSql_WithNonByteArrayValue_RejectsBeforeProviderCall()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("array_values", "id integer NOT NULL, numbers integer[] NOT NULL");
        var df = CreateRawDataFrame([new TestSeries("id", typeof(int), [1]), new TestSeries("numbers", typeof(object), [new[] { 1, 2, 3 }])]);

        var exception = Assert.Throws<ArgumentException>(() => df.WriteSql(connection, table));

        Assert.Contains("numbers", exception.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported SQL Write data type", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, CountRows(connection, table));
    }

    // Verifies SQL Write and SQL Read round-trip stable PostgreSQL values without timestamptz offset assumptions.
    [Fact]
    public void WriteSql_ThenReadSql_RoundTripsStablePostgreSqlValues()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable(
            "round_trip",
            """
            text_value text NULL,
            bool_value boolean NOT NULL,
            short_value smallint NOT NULL,
            int_value integer NOT NULL,
            long_value bigint NOT NULL,
            decimal_value numeric(18,2) NOT NULL,
            double_value double precision NOT NULL,
            timestamp_value timestamp without time zone NOT NULL,
            date_value date NOT NULL,
            time_value time without time zone NOT NULL,
            guid_value uuid NOT NULL,
            binary_value bytea NULL
            """);
        var timestamp = new DateTime(2026, 7, 14, 8, 0, 0, DateTimeKind.Unspecified);
        var date = new DateOnly(2026, 7, 14);
        var time = new TimeOnly(8, 30, 0);
        var guid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var payload = new byte[] { 5, 4, 3, 2, 1 };

        CreateDataFrame(
            [
                Series<string?>.Create("text_value", ["text", null]),
                Series<bool>.Create("bool_value", [true, false]),
                Series<short>.Create("short_value", [1, 2]),
                Series<int>.Create("int_value", [1, 2]),
                Series<long>.Create("long_value", [10L, 20L]),
                Series<decimal>.Create("decimal_value", [12.34m, 56.78m]),
                Series<double>.Create("double_value", [1.5d, 2.5d]),
                Series<DateTime>.Create("timestamp_value", [timestamp, timestamp.AddDays(1)]),
                Series<DateOnly>.Create("date_value", [date, date.AddDays(1)]),
                Series<TimeOnly>.Create("time_value", [time, time.AddHours(1)]),
                Series<Guid>.Create("guid_value", [guid, guid]),
                Series<byte[]?>.Create("binary_value", [payload, null])
            ]).WriteSql(connection, table);

        var df = global::Runiq.Data.DataFrame.ReadSql(
            connection,
            $"SELECT text_value, bool_value, short_value, int_value, long_value, decimal_value, double_value, timestamp_value, date_value, time_value, guid_value, binary_value FROM {table} ORDER BY int_value;");

        Assert.Equal(["text", null], Values(df, "text_value"));
        Assert.Equal([true, false], Values(df, "bool_value"));
        Assert.Equal([(short)1, (short)2], Values(df, "short_value"));
        Assert.Equal([1, 2], Values(df, "int_value"));
        Assert.Equal([10L, 20L], Values(df, "long_value"));
        Assert.Equal([12.34m, 56.78m], Values(df, "decimal_value"));
        Assert.Equal([1.5d, 2.5d], Values(df, "double_value"));
        Assert.Equal([timestamp, timestamp.AddDays(1)], Values(df, "timestamp_value"));
        Assert.Equal([date, date.AddDays(1)], Values(df, "date_value"));
        Assert.Equal([time, time.AddHours(1)], Values(df, "time_value"));
        Assert.Equal([guid, guid], Values(df, "guid_value"));
        Assert.Equal(payload, (byte[])df["binary_value"].GetValue(0)!);
        Assert.Null(df["binary_value"].GetValue(1));
    }

    // Verifies DataFrame row order is the insert execution order by checking ordered identities and null transitions.
    [Fact]
    public void WriteSql_WithRowOrderSensitiveValues_BindsEachRowIndependently()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("row_order", "id integer NOT NULL, value text NULL");
        var df = CreateDataFrame("id", typeof(int), [3, 1, 2], "value", typeof(string), ["third", null, "second"]);

        df.WriteSql(connection, table);

        Assert.Equal([[1, DBNull.Value], [2, "second"], [3, "third"]], QueryRows(connection, $"SELECT id, value FROM {table} ORDER BY id;"));
    }

    // Verifies PostgreSQL defaults apply only for destination columns omitted from the DataFrame.
    [Fact]
    public void WriteSql_WhenDestinationColumnIsOmitted_AppliesDatabaseDefault()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("defaults", "id integer NOT NULL, name text NOT NULL, created_by text NOT NULL DEFAULT 'system'");

        global::Runiq.Data.DataFrame.Create(new { id = new[] { 1 }, name = new[] { "Defaulted" } }).WriteSql(connection, table);

        Assert.Equal("system", ReadScalar<string>(connection, $"SELECT created_by FROM {table} WHERE id = 1;"));
    }

    // Verifies generated identity columns can be omitted and generated by PostgreSQL without schema discovery.
    [Fact]
    public void WriteSql_WhenIdentityColumnIsOmitted_AllowsDatabaseGeneratedValue()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("identity_generated", "id bigint GENERATED ALWAYS AS IDENTITY, name text NOT NULL");

        global::Runiq.Data.DataFrame.Create(new { name = new[] { "Generated" } }).WriteSql(connection, table);

        Assert.Equal([[1L, "Generated"]], QueryRows(connection, $"SELECT id, name FROM {table};"));
    }

    // Verifies explicit values for GENERATED ALWAYS identity columns are rejected by PostgreSQL and not silently dropped.
    [Fact]
    public void WriteSql_WhenIdentityColumnIsExplicit_PropagatesDatabaseError()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlWriteSchema.Create(connection);
        var table = schema.CreateTable("identity_explicit", "id bigint GENERATED ALWAYS AS IDENTITY, name text NOT NULL");

        var exception = Assert.Throws<PostgresException>(() => global::Runiq.Data.DataFrame.Create(new { id = new[] { 42L }, name = new[] { "Explicit" } }).WriteSql(connection, table));

        Assert.Equal("428C9", exception.SqlState);
        Assert.Equal(0, CountRows(connection, table));
    }

    /// <summary>
    /// Creates the representative employee DataFrame used by append and mutation tests.
    /// </summary>
    private static global::Runiq.Data.DataFrame CreateEmployeesDataFrame()
    {
        var utc = new DateTimeOffset(2026, 7, 14, 9, 30, 0, TimeSpan.Zero);
        return CreateDataFrame(
            [
                Series<int>.Create("id", [1, 2, 3]),
                Series<string>.Create("name", ["Ali", "Ay\u015fe", "Mehmet"]),
                Series<string>.Create("department", ["Engineering", "Finance", "Engineering"]),
                Series<decimal?>.Create("salary", [125000.50m, 110000.00m, null]),
                Series<bool>.Create("active", [true, true, false]),
                Series<DateTime>.Create("created_at", [new DateTime(2026, 7, 14, 9, 30, 0), new DateTime(2026, 7, 15, 9, 30, 0), new DateTime(2026, 7, 16, 9, 30, 0)]),
                Series<DateTimeOffset>.Create("created_at_utc", [utc, utc.AddDays(1), utc.AddDays(2)]),
                Series<Guid>.Create("external_id", [Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222"), Guid.Parse("33333333-3333-3333-3333-333333333333")]),
                Series<byte[]?>.Create("payload", [new byte[] { 1, 2, 3, 4 }, new byte[] { 5, 6, 7, 8 }, null])
            ]);
    }

    /// <summary>
    /// Asserts employee values through ORDER BY because PostgreSQL does not guarantee natural table row order.
    /// </summary>
    private static void AssertEmployees(NpgsqlConnection connection, string table)
    {
        var rows = QueryRows(connection, $"SELECT id, name, department, salary, active, created_at, created_at_utc, external_id, payload FROM {table} ORDER BY id;");
        Assert.Equal(3, rows.Count);
        Assert.Equal([1, "Ali", "Engineering", 125000.50m, true, new DateTime(2026, 7, 14, 9, 30, 0), DateTime.SpecifyKind(new DateTime(2026, 7, 14, 9, 30, 0), DateTimeKind.Utc), Guid.Parse("11111111-1111-1111-1111-111111111111"), new byte[] { 1, 2, 3, 4 }], rows[0]);
        Assert.Equal([2, "Ay\u015fe", "Finance", 110000.00m, true, new DateTime(2026, 7, 15, 9, 30, 0), DateTime.SpecifyKind(new DateTime(2026, 7, 15, 9, 30, 0), DateTimeKind.Utc), Guid.Parse("22222222-2222-2222-2222-222222222222"), new byte[] { 5, 6, 7, 8 }], rows[1]);
        Assert.Equal(3, rows[2][0]);
        Assert.Equal(DBNull.Value, rows[2][3]);
        Assert.Equal(DBNull.Value, rows[2][8]);
    }

    /// <summary>
    /// Executes command text inside a caller-owned transaction without committing or rolling it back.
    /// </summary>
    private static void ExecuteNonQuery(NpgsqlConnection connection, NpgsqlTransaction transaction, string commandText)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Ensures a caller-owned connection is open before schema cleanup after ownership tests.
    /// </summary>
    private static void EnsureOpen(NpgsqlConnection connection)
    {
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }
    }

    /// <summary>
    /// Counts rows outside an external transaction to verify committed or rolled-back database state.
    /// </summary>
    private static int CountRows(NpgsqlConnection connection, string table)
    {
        return Convert.ToInt32(ReadScalar<long>(connection, $"SELECT COUNT(*) FROM {table};"));
    }

    /// <summary>
    /// Counts rows inside the supplied caller-owned transaction without changing its ownership.
    /// </summary>
    private static int CountRows(NpgsqlConnection connection, NpgsqlTransaction transaction, string table)
    {
        return Convert.ToInt32(ReadScalar<long>(connection, transaction, $"SELECT COUNT(*) FROM {table};"));
    }

    /// <summary>
    /// Reads a scalar value from PostgreSQL for precise contract assertions.
    /// </summary>
    private static T ReadScalar<T>(NpgsqlConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return (T)command.ExecuteScalar()!;
    }

    /// <summary>
    /// Reads a scalar value within an external transaction while leaving transaction ownership with the caller.
    /// </summary>
    private static T ReadScalar<T>(NpgsqlConnection connection, NpgsqlTransaction transaction, string commandText)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        return (T)command.ExecuteScalar()!;
    }

    /// <summary>
    /// Reads one PostgreSQL column into a typed array for mapping assertions.
    /// </summary>
    private static T[] QueryColumn<T>(NpgsqlConnection connection, string commandText)
    {
        return QueryRows(connection, commandText).Select(row => (T)row[0]!).ToArray();
    }

    /// <summary>
    /// Reads PostgreSQL rows as object arrays while preserving provider CLR values and DBNull markers.
    /// </summary>
    private static List<object?[]> QueryRows(NpgsqlConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        using var reader = command.ExecuteReader();
        var rows = new List<object?[]>();
        while (reader.Read())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            rows.Add(values);
        }

        return rows;
    }

    /// <summary>
    /// Creates a DataFrame from explicitly ordered series so INSERT column order is under test control.
    /// </summary>
    private static global::Runiq.Data.DataFrame CreateDataFrame(IReadOnlyList<ISeries> series)
    {
        return (global::Runiq.Data.DataFrame)typeof(global::Runiq.Data.DataFrame)
            .GetMethod("CreateFromSeries", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [series])!;
    }

    /// <summary>
    /// Creates a two-column DataFrame with explicit CLR column types for nullable and diagnostic scenarios.
    /// </summary>
    private static global::Runiq.Data.DataFrame CreateDataFrame(
        string firstName,
        Type firstType,
        IReadOnlyList<object?> firstValues,
        string secondName,
        Type secondType,
        IReadOnlyList<object?> secondValues)
    {
        return CreateDataFrame([CreateSeries(firstName, firstType, firstValues), CreateSeries(secondName, secondType, secondValues)]);
    }

    /// <summary>
    /// Creates a typed series by reflection while preserving nulls for nullable SQL Write test data.
    /// </summary>
    private static ISeries CreateSeries(string name, Type dataType, IEnumerable<object?> values)
    {
        var snapshot = values.ToArray();
        var array = Array.CreateInstance(dataType, snapshot.Length);
        for (var index = 0; index < snapshot.Length; index++)
        {
            array.SetValue(snapshot[index], index);
        }

        return (ISeries)typeof(Series<>)
            .MakeGenericType(dataType)
            .GetMethod(nameof(Series<int>.Create), BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [name, array])!;
    }

    /// <summary>
    /// Creates a DataFrame with custom series metadata to test runtime value validation paths.
    /// </summary>
    private static global::Runiq.Data.DataFrame CreateRawDataFrame(IReadOnlyList<ISeries> series)
    {
        var schema = typeof(DataFrameSchema)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(ReadOnlyCollection<ColumnSchema>)], null)!
            .Invoke([Array.AsReadOnly(Array.Empty<ColumnSchema>())]);

        return (global::Runiq.Data.DataFrame)typeof(global::Runiq.Data.DataFrame)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(DataFrameSchema), typeof(ReadOnlyCollection<ISeries>)], null)!
            .Invoke([schema, Array.AsReadOnly(series.ToArray())]);
    }

    /// <summary>
    /// Captures DataFrame shape, order, and byte-array cell content before SQL Write to prove it is not mutated.
    /// </summary>
    private static DataFrameSnapshot Snapshot(global::Runiq.Data.DataFrame df)
    {
        return new DataFrameSnapshot(
            df.Schema.Columns.Select(column => column.Name).ToArray(),
            df.Schema.Columns.Select(column => column.DataType).ToArray(),
            df.Schema.Columns.Select(column => column.IsNullable).ToArray(),
            df.Schema.Columns.Select(column => Enumerable.Range(0, df[column.Name].Count).Select(row => CloneCell(df[column.Name].GetValue(row))).ToArray()).ToArray());
    }

    /// <summary>
    /// Asserts DataFrame shape, column order, and values remain unchanged after SQL Write.
    /// </summary>
    private static void AssertDataFrameSnapshot(global::Runiq.Data.DataFrame df, DataFrameSnapshot snapshot)
    {
        Assert.Equal(snapshot.Names, df.Schema.Columns.Select(column => column.Name));
        Assert.Equal(snapshot.Types, df.Schema.Columns.Select(column => column.DataType));
        Assert.Equal(snapshot.Nullability, df.Schema.Columns.Select(column => column.IsNullable));
        for (var columnIndex = 0; columnIndex < snapshot.Names.Length; columnIndex++)
        {
            var column = df[snapshot.Names[columnIndex]];
            Assert.Equal(snapshot.Values[columnIndex].Length, column.Count);
            for (var rowIndex = 0; rowIndex < column.Count; rowIndex++)
            {
                var expected = snapshot.Values[columnIndex][rowIndex];
                var actual = column.GetValue(rowIndex);
                if (expected is byte[] expectedBytes)
                {
                    Assert.Equal(expectedBytes, (byte[])actual!);
                }
                else
                {
                    Assert.Equal(expected, actual);
                }
            }
        }
    }

    /// <summary>
    /// Clones byte-array cells so mutation validation checks binary content, not object identity.
    /// </summary>
    private static object? CloneCell(object? value)
    {
        return value is byte[] bytes ? bytes.ToArray() : value;
    }

    /// <summary>
    /// Reads DataFrame values from a named column while preserving row order.
    /// </summary>
    private static object?[] Values(global::Runiq.Data.DataFrame df, string columnName)
    {
        var column = df[columnName];
        return Enumerable.Range(0, column.Count).Select(column.GetValue).ToArray();
    }

    /// <summary>
    /// Stores a DataFrame mutation snapshot for post-write verification.
    /// </summary>
    private sealed record DataFrameSnapshot(string[] Names, Type[] Types, bool[] Nullability, object?[][] Values);

    /// <summary>
    /// Represents a runtime value that SQL Write must reject instead of converting with ToString.
    /// </summary>
    private sealed class UnsupportedSqlValue;

    /// <summary>
    /// Provides custom DataFrame series values for validation paths public Series creation cannot express.
    /// </summary>
    private sealed class TestSeries(string name, Type dataType, IReadOnlyList<object?> values) : ISeries
    {
        public string Name { get; } = name;

        public int Count => values.Count;

        public Type DataType { get; } = dataType;

        public bool IsNullable => !DataType.IsValueType || Nullable.GetUnderlyingType(DataType) is not null;

        public object? GetValue(int index)
        {
            return values[index];
        }
    }

    /// <summary>
    /// Owns one generated PostgreSQL schema and drops it with CASCADE during cleanup.
    /// </summary>
    private sealed class PostgreSqlWriteSchema : IDisposable
    {
        private readonly NpgsqlConnection connection;
        private readonly bool dropOnDispose;

        /// <summary>
        /// Wraps an existing generated schema name without taking ownership of the connection.
        /// </summary>
        internal PostgreSqlWriteSchema(NpgsqlConnection connection, string name, bool dropOnDispose = true)
        {
            this.connection = connection;
            Name = name;
            this.dropOnDispose = dropOnDispose;
        }

        /// <summary>
        /// Gets the generated lowercase schema name used to isolate one test's database state.
        /// </summary>
        internal string Name { get; }

        /// <summary>
        /// Gets the generated employees table identifier used by append contract tests.
        /// </summary>
        internal string Employees => Qualified("employees");

        /// <summary>
        /// Creates a new safe schema name and initializes it on the caller-owned open connection.
        /// </summary>
        internal static PostgreSqlWriteSchema Create(NpgsqlConnection connection, bool dropOnDispose = true)
        {
            var schema = new PostgreSqlWriteSchema(connection, UniqueSchemaName(), dropOnDispose);
            schema.Execute($"CREATE SCHEMA {schema.Name};");
            return schema;
        }

        /// <summary>
        /// Generates an internal schema identifier using only lowercase letters, digits, and underscores.
        /// </summary>
        internal static string UniqueSchemaName()
        {
            return $"test_{Guid.NewGuid():N}";
        }

        /// <summary>
        /// Creates a generated table inside this schema and returns its schema-qualified identifier.
        /// </summary>
        internal string CreateTable(string table, string columns)
        {
            var qualified = Qualified(table);
            Execute($"CREATE TABLE {qualified} ({columns});");
            return qualified;
        }

        /// <summary>
        /// Creates the employee contract table without seed rows so WriteSql is the only insert path under test.
        /// </summary>
        internal void CreateEmployees()
        {
            CreateTable(
                "employees",
                """
                id integer NOT NULL,
                name text NOT NULL,
                department text NOT NULL,
                salary numeric(18,2) NULL,
                active boolean NOT NULL,
                created_at timestamp without time zone NOT NULL,
                created_at_utc timestamp with time zone NOT NULL,
                external_id uuid NOT NULL,
                payload bytea NULL
                """);
        }

        /// <summary>
        /// Creates a qualified identifier inside this generated schema.
        /// </summary>
        internal string Qualified(string table)
        {
            return Qualified(Name, table);
        }

        /// <summary>
        /// Creates a qualified identifier from generated lowercase schema and table names.
        /// </summary>
        internal static string Qualified(string schema, string table)
        {
            ValidateIdentifier(schema);
            ValidateIdentifier(table);
            return $"{schema}.{table}";
        }

        /// <summary>
        /// Executes SQL against the caller-owned open connection without taking ownership of it.
        /// </summary>
        internal void Execute(string commandText)
        {
            using var command = connection.CreateCommand();
            command.CommandText = commandText;
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Drops the generated schema and all contained objects without owning the connection.
        /// </summary>
        public void Dispose()
        {
            if (!dropOnDispose)
            {
                return;
            }

            Execute($"DROP SCHEMA IF EXISTS {Name} CASCADE;");
        }

        /// <summary>
        /// Validates generated PostgreSQL identifiers before they are used in unquoted SQL.
        /// </summary>
        private static void ValidateIdentifier(string identifier)
        {
            if (identifier.Any(static character => (character < 'a' || character > 'z') && !char.IsDigit(character) && character != '_'))
            {
                throw new ArgumentException($"Generated PostgreSQL identifier '{identifier}' contains an unsafe character.", nameof(identifier));
            }
        }
    }
}
