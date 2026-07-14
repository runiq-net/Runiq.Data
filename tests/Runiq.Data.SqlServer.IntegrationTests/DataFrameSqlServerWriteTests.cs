using System.Collections.ObjectModel;
using System.Data;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Runiq.Data.IO;
using Runiq.Data.Schema;
using Runiq.Data.Series;

namespace Runiq.Data.SqlServer.IntegrationTests;

/// <summary>
/// Verifies SQL Write append, transaction, ownership, type-mapping, and failure contracts against a shared SQL Server container.
/// </summary>
/// <remarks>
/// The SQL Server collection owns one Testcontainers lifecycle for this class and the read
/// contract tests. Each test creates generated identifiers containing only letters, digits, and
/// underscores, then removes the objects in a finally block so tests never depend on execution order.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public sealed class DataFrameSqlServerWriteTests(SqlServerContainerFixture fixture)
{
    // Verifies default append behavior, column-order INSERT binding, null mapping, and DataFrame immutability.
    [Fact]
    public void WriteSql_WithDefaultOptions_AppendsEmployeesAndDoesNotMutateDataFrame()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateEmployeesTable(connection);
        var df = CreateEmployeesDataFrame();
        var snapshot = Snapshot(df);

        try
        {
            df.WriteSql(connection, table);

            AssertEmployees(connection, table);
            AssertDataFrameSnapshot(df, snapshot);
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies SQL Write preserves existing rows and only appends new rows to the destination table.
    [Fact]
    public void WriteSql_WithExistingRows_PreservesRowsAndAppends()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(connection, "ExistingRows", "Id INT NOT NULL, Name NVARCHAR(100) NOT NULL");

        try
        {
            ExecuteNonQuery(connection, $"INSERT INTO {table} (Id, Name) VALUES (10, N'Existing');");

            global::Runiq.Data.DataFrame.Create(new { Id = new[] { 11, 12 }, Name = new[] { "NewA", "NewB" } })
                .WriteSql(connection, table);

            var rows = QueryRows(connection, $"SELECT Id, Name FROM {table} ORDER BY Id");
            Assert.Equal([[10, "Existing"], [11, "NewA"], [12, "NewB"]], rows);
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies the internally owned transaction rolls back all DataFrame rows after a real SQL Server constraint failure.
    [Fact]
    public void WriteSql_WithInternalTransactionFailure_RollsBackAtomicallyAndKeepsConnectionUsable()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(connection, "AtomicRows", "Id INT NOT NULL PRIMARY KEY, Name NVARCHAR(100) NOT NULL");

        try
        {
            ExecuteNonQuery(connection, $"INSERT INTO {table} (Id, Name) VALUES (1, N'Original');");
            var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 2, 3, 1 }, Name = new[] { "A", "B", "Duplicate" } });

            var exception = Assert.Throws<SqlException>(() => df.WriteSql(connection, table));

            Assert.Contains("PRIMARY", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal([[1, "Original"]], QueryRows(connection, $"SELECT Id, Name FROM {table} ORDER BY Id"));
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal(1, CountRows(connection, table));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies a successful internal transaction commits rows that are visible from a separate SQL Server connection.
    [Fact]
    public void WriteSql_WithInternalTransactionSuccess_CommitsRowsVisibleToNewConnection()
    {
        string table;
        using (var setupConnection = fixture.CreateConnection())
        {
            setupConnection.Open();
            table = CreateSimpleTable(setupConnection, "CommittedRows", "Id INT NOT NULL, Name NVARCHAR(100) NOT NULL");
            global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1, 2 }, Name = new[] { "One", "Two" } })
                .WriteSql(setupConnection, table);
        }

        using var verificationConnection = fixture.CreateConnection();
        verificationConnection.Open();
        try
        {
            Assert.Equal([[1, "One"], [2, "Two"]], QueryRows(verificationConnection, $"SELECT Id, Name FROM {table} ORDER BY Id"));
        }
        finally
        {
            DropTable(verificationConnection, table);
        }
    }

    // Verifies external transactions remain caller-owned and can be rolled back after SQL Write returns.
    [Fact]
    public void WriteSql_WithExternalTransaction_AllowsCallerRollback()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(connection, "ExternalRollback", "Id INT NOT NULL, Name NVARCHAR(100) NOT NULL");

        try
        {
            using var transaction = connection.BeginTransaction();
            global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1, 2 }, Name = new[] { "One", "Two" } })
                .WriteSql(connection, table, new SqlWriteOptions { Transaction = transaction });

            Assert.Equal(2, CountRows(connection, transaction, table));
            transaction.Rollback();
            Assert.Equal(0, CountRows(connection, table));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies external transaction commits are performed only by the caller and persist to a new connection.
    [Fact]
    public void WriteSql_WithExternalTransaction_AllowsCallerCommit()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(connection, "ExternalCommit", "Id INT NOT NULL, Name NVARCHAR(100) NOT NULL");

        try
        {
            using (var transaction = connection.BeginTransaction())
            {
                global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 }, Name = new[] { "Committed" } })
                    .WriteSql(connection, table, new SqlWriteOptions { Transaction = transaction });
                transaction.Commit();
            }

            using var verificationConnection = fixture.CreateConnection();
            verificationConnection.Open();
            Assert.Equal([[1, "Committed"]], QueryRows(verificationConnection, $"SELECT Id, Name FROM {table}"));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies a failed external transaction is not committed, rolled back, or disposed by Runiq.Data.
    [Fact]
    public void WriteSql_WithExternalTransactionFailure_LeavesTransactionForCallerCleanup()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(connection, "ExternalFailure", "Id INT NOT NULL PRIMARY KEY, Name NVARCHAR(100) NOT NULL");

        try
        {
            using var transaction = connection.BeginTransaction();
            ExecuteNonQuery(connection, transaction, $"INSERT INTO {table} (Id, Name) VALUES (1, N'Original');");
            var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 2, 1 }, Name = new[] { "BeforeFailure", "Duplicate" } });

            Assert.Throws<SqlException>(() => df.WriteSql(connection, table, new SqlWriteOptions { Transaction = transaction }));

            Assert.Equal((short)1, ReadScalar<short>(connection, transaction, "SELECT XACT_STATE();"));
            transaction.Rollback();
            Assert.Equal(0, CountRows(connection, table));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies a caller-opened SqlConnection remains open and reusable after SQL Write completes.
    [Fact]
    public void WriteSql_WithOpenConnection_LeavesConnectionOpenAndReusable()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(connection, "OpenConnection", "Id INT NOT NULL");

        try
        {
            global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 } }).WriteSql(connection, table);

            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal(1, CountRows(connection, table));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies a caller-closed SqlConnection is opened temporarily, restored to Closed, and remains reusable.
    [Fact]
    public void WriteSql_WithClosedConnection_RestoresClosedStateAndRemainsReusable()
    {
        string table;
        using (var setupConnection = fixture.CreateConnection())
        {
            setupConnection.Open();
            table = CreateSimpleTable(setupConnection, "ClosedConnection", "Id INT NOT NULL");
        }

        using var connection = fixture.CreateConnection();
        try
        {
            global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1, 2 } }).WriteSql(connection, table);

            Assert.Equal(ConnectionState.Closed, connection.State);
            connection.Open();
            Assert.Equal(2, CountRows(connection, table));
        }
        finally
        {
            EnsureOpen(connection);
            DropTable(connection, table);
        }
    }

    // Verifies missing-table provider diagnostics are preserved and closed connection ownership is restored after failure.
    [Fact]
    public void WriteSql_WithMissingTable_ThrowsSqlExceptionAndRestoresConnection()
    {
        var table = UniqueName("MissingEmployees");
        using var connection = fixture.CreateConnection();

        var exception = Assert.Throws<SqlException>(() => global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 } }).WriteSql(connection, table));

        Assert.Contains(table, exception.Message, StringComparison.Ordinal);
        Assert.Equal(ConnectionState.Closed, connection.State);
        connection.Open();
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    // Verifies SQL Server constraint failures propagate SqlException and internal rollback leaves no partial append.
    [Fact]
    public void WriteSql_WithConstraintFailure_ThrowsSqlExceptionRollsBackAndConnectionIsReusable()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(connection, "ConstraintFailure", "Id INT NOT NULL PRIMARY KEY");

        try
        {
            var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1, 2, 1 } });

            Assert.Throws<SqlException>(() => df.WriteSql(connection, table));

            Assert.Equal(0, CountRows(connection, table));
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal(0, ReadScalar<int>(connection, $"SELECT COUNT(*) FROM {table};"));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies a two-part dbo table identifier is preserved and works on SQL Server.
    [Fact]
    public void WriteSql_WithSchemaQualifiedTableName_AppendsRows()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var name = UniqueName("Employees");
        var table = $"dbo.{name}";

        try
        {
            ExecuteNonQuery(connection, $"CREATE TABLE {table} (Id INT NOT NULL, Name NVARCHAR(100) NOT NULL);");

            global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 }, Name = new[] { "Schema" } }).WriteSql(connection, table);

            Assert.Equal([[1, "Schema"]], QueryRows(connection, $"SELECT Id, Name FROM {table}"));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies unsafe table identifiers are rejected before provider execution and cannot affect a real destination table.
    [Fact]
    public void WriteSql_WithInvalidIdentifier_RejectsBeforeProviderCallAndLeavesTableUnchanged()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(connection, "SafeEmployees", "Id INT NOT NULL");

        try
        {
            var exception = Assert.ThrowsAny<ArgumentException>(() => global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 } })
                .WriteSql(connection, "Employees;DROP_TABLE"));

            Assert.Contains("simple SQL identifiers", exception.Message, StringComparison.Ordinal);
            Assert.Equal(0, CountRows(connection, table));
            Assert.Equal(ConnectionState.Open, connection.State);
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies an empty DataFrame is a no-op even when the valid table name does not exist and the connection is closed.
    [Fact]
    public void WriteSql_WithEmptyDataFrame_IsNoOpWithoutOpeningConnectionOrCheckingTable()
    {
        using var connection = fixture.CreateConnection();
        var missingTable = UniqueName("MissingEmpty");
        var df = global::Runiq.Data.DataFrame.Create(new { Id = Array.Empty<int>() });

        df.WriteSql(connection, missingTable);

        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    // Verifies SQL NULL and explicit-null default behavior against real SQL Server columns.
    [Fact]
    public void WriteSql_WithNullValues_WritesSqlNullAndDoesNotApplyColumnDefault()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(
            connection,
            "NullDefaults",
            "Id INT NOT NULL, OptionalText NVARCHAR(100) NULL DEFAULT N'default-value'");

        try
        {
            var df = CreateDataFrame("Id", typeof(int), [1], "OptionalText", typeof(string), [null]);

            df.WriteSql(connection, table);

            Assert.Equal(DBNull.Value, ReadScalar<object>(connection, $"SELECT OptionalText FROM {table} WHERE Id = 1;"));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies string-looking values and Unicode text are preserved as NVARCHAR without parsing or formula handling.
    [Fact]
    public void WriteSql_WithStringValues_PreservesExactText()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(connection, "StringValues", "Id INT NOT NULL, Value NVARCHAR(100) NOT NULL");

        try
        {
            var values = new[] { "123", "true", "null", "2026-07-14", "=SUM(A1:A2)", "Ay\u015fe" };
            global::Runiq.Data.DataFrame.Create(new { Id = Enumerable.Range(1, values.Length).ToArray(), Value = values }).WriteSql(connection, table);

            Assert.Equal(values, QueryColumn<string>(connection, $"SELECT Value FROM {table} ORDER BY Id"));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies char values are written as single-character text values.
    [Fact]
    public void WriteSql_WithCharValue_WritesSingleCharacterText()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(connection, "CharValues", "Value NCHAR(1) NOT NULL");

        try
        {
            global::Runiq.Data.DataFrame.Create(new { Value = new[] { 'Z' } }).WriteSql(connection, table);

            Assert.Equal("Z", ReadScalar<string>(connection, $"SELECT Value FROM {table};"));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies signed numeric, boolean, floating-point, decimal, and byte mappings against SQL Server native columns.
    [Fact]
    public void WriteSql_WithSignedNumericAndBooleanValues_WritesExpectedSqlServerValues()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(
            connection,
            "SignedTypes",
            """
            BoolValue BIT NOT NULL,
            ByteValue TINYINT NOT NULL,
            ShortValue SMALLINT NOT NULL,
            IntValue INT NOT NULL,
            LongValue BIGINT NOT NULL,
            DecimalValue DECIMAL(18,2) NOT NULL,
            FloatValue REAL NOT NULL,
            DoubleValue FLOAT NOT NULL
            """);

        try
        {
            global::Runiq.Data.DataFrame.Create(new
            {
                BoolValue = new[] { true, false },
                ByteValue = new byte[] { 7, 8 },
                ShortValue = new short[] { -123, 456 },
                IntValue = new[] { -123456, 654321 },
                LongValue = new[] { -9000000000L, 9000000000L },
                DecimalValue = new[] { 12345.67m, 76543.21m },
                FloatValue = new[] { 1.25f, 2.5f },
                DoubleValue = new[] { 3.125d, 6.25d }
            }).WriteSql(connection, table);

            var rows = QueryRows(connection, $"SELECT BoolValue, ByteValue, ShortValue, IntValue, LongValue, DecimalValue, FloatValue, DoubleValue FROM {table} ORDER BY IntValue");
            Assert.Equal(true, rows[0][0]);
            Assert.Equal((byte)7, rows[0][1]);
            Assert.Equal((short)-123, rows[0][2]);
            Assert.Equal(-123456, rows[0][3]);
            Assert.Equal(-9000000000L, rows[0][4]);
            Assert.Equal(12345.67m, rows[0][5]);
            Assert.InRange((float)rows[0][6]!, 1.2499f, 1.2501f);
            Assert.InRange((double)rows[0][7]!, 3.124999d, 3.125001d);
            Assert.Equal(false, rows[1][0]);
            Assert.Equal((byte)8, rows[1][1]);
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies DateTime, DateTimeOffset, DateOnly, TimeOnly, and TimeSpan mappings with SQL Server precision rules.
    [Fact]
    public void WriteSql_WithDateAndTimeValues_PreservesProviderValuesWithinSqlServerPrecision()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(
            connection,
            "DateTimeTypes",
            """
            DateTimeValue DATETIME2(7) NOT NULL,
            DateTimeOffsetValue DATETIMEOFFSET(7) NOT NULL,
            DateOnlyValue DATE NOT NULL,
            TimeOnlyValue TIME(7) NOT NULL,
            TimeSpanValue TIME(7) NOT NULL
            """);
        var dateTime = new DateTime(2026, 7, 14, 10, 15, 30, 123, DateTimeKind.Unspecified).AddTicks(4567);
        var dateTimeOffset = new DateTimeOffset(2026, 7, 14, 10, 15, 30, 123, TimeSpan.FromHours(3)).AddTicks(4567);
        var dateOnly = new DateOnly(2026, 7, 14);
        var timeOnly = new TimeOnly(10, 15, 30, 123).Add(TimeSpan.FromTicks(4567));
        var timeSpan = new TimeSpan(10, 15, 30).Add(TimeSpan.FromMilliseconds(123)).Add(TimeSpan.FromTicks(4567));

        try
        {
            global::Runiq.Data.DataFrame.Create(new
            {
                DateTimeValue = new[] { dateTime },
                DateTimeOffsetValue = new[] { dateTimeOffset },
                DateOnlyValue = new[] { dateOnly },
                TimeOnlyValue = new[] { timeOnly },
                TimeSpanValue = new[] { timeSpan }
            }).WriteSql(connection, table);

            var row = Assert.Single(QueryRows(connection, $"SELECT DateTimeValue, DateTimeOffsetValue, DateOnlyValue, TimeOnlyValue, TimeSpanValue FROM {table};"));
            Assert.Equal(dateTime, row[0]);
            Assert.Equal(dateTimeOffset, row[1]);
            Assert.Equal(dateOnly.ToDateTime(TimeOnly.MinValue), row[2]);
            Assert.Equal(timeOnly.ToTimeSpan(), (TimeSpan)row[3]!);
            Assert.Equal(timeSpan, (TimeSpan)row[4]!);
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies Guid and byte-array values are written without changing identity or binary sequence.
    [Fact]
    public void WriteSql_WithGuidAndBinaryValues_PreservesValues()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(connection, "GuidBinary", "Id UNIQUEIDENTIFIER NOT NULL, Payload VARBINARY(MAX) NOT NULL");
        var guid = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var bytes = new byte[] { 1, 2, 3, 4, 5 };

        try
        {
            global::Runiq.Data.DataFrame.Create(new { Id = new[] { guid }, Payload = new[] { bytes } }).WriteSql(connection, table);
            bytes[0] = 9;

            var row = Assert.Single(QueryRows(connection, $"SELECT Id, Payload FROM {table};"));
            Assert.Equal(guid, row[0]);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, (byte[])row[1]!);
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies unsigned numeric DbType mappings either work with SQL Server columns or expose real provider incompatibility.
    [Fact]
    public void WriteSql_WithUnsignedNumericValues_VerifiesSqlServerProviderBehavior()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(
            connection,
            "UnsignedTypes",
            "SByteValue SMALLINT NOT NULL, UInt16Value INT NOT NULL, UInt32Value BIGINT NOT NULL, UInt64Value DECIMAL(20,0) NOT NULL");

        try
        {
            global::Runiq.Data.DataFrame.Create(new
            {
                SByteValue = new sbyte[] { -8 },
                UInt16Value = new ushort[] { 65000 },
                UInt32Value = new uint[] { 4000000000u },
                UInt64Value = new ulong[] { 18446744073709551615UL }
            }).WriteSql(connection, table);

            var row = Assert.Single(QueryRows(connection, $"SELECT SByteValue, UInt16Value, UInt32Value, UInt64Value FROM {table};"));
            Assert.Equal((short)-8, row[0]);
            Assert.Equal(65000, row[1]);
            Assert.Equal(4000000000L, row[2]);
            Assert.Equal(18446744073709551615m, row[3]);
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies non-finite double values are rejected before partial SQL Server writes can remain.
    [Fact]
    public void WriteSql_WithNonFiniteDoubleValues_RejectsBeforePartialAppend()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(connection, "NonFinite", "Id INT NOT NULL, Value FLOAT NOT NULL");

        try
        {
            var df = global::Runiq.Data.DataFrame.Create(new
            {
                Id = new[] { 1, 2, 3 },
                Value = new[] { 1.0d, double.NaN, double.PositiveInfinity }
            });

            var exception = Assert.Throws<ArgumentException>(() => df.WriteSql(connection, table));

            Assert.Contains("non-finite", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, CountRows(connection, table));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies unsupported runtime values fail with diagnostics and without using ToString fallback.
    [Fact]
    public void WriteSql_WithUnsupportedRuntimeValue_RejectsBeforeDatabaseMutation()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(connection, "UnsupportedValue", "Id INT NOT NULL, Metadata NVARCHAR(100) NOT NULL");

        try
        {
            var df = CreateRawDataFrame([new TestSeries("Id", typeof(int), [1, 2]), new TestSeries("Metadata", typeof(string), ["ok", new UnsupportedSqlValue()])]);

            var exception = Assert.Throws<ArgumentException>(() => df.WriteSql(connection, table));

            Assert.Contains("Metadata", exception.Message, StringComparison.Ordinal);
            Assert.Contains("row 1", exception.Message, StringComparison.Ordinal);
            Assert.Contains(typeof(UnsupportedSqlValue).FullName!, exception.Message, StringComparison.Ordinal);
            Assert.Equal(0, CountRows(connection, table));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies SQL Write and SQL Read round-trip stable SQL Server values without fragile provider representation assumptions.
    [Fact]
    public void WriteSql_ThenReadSql_RoundTripsStableSqlServerValues()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(
            connection,
            "RoundTrip",
            """
            TextValue NVARCHAR(100) NULL,
            BoolValue BIT NOT NULL,
            IntValue INT NOT NULL,
            LongValue BIGINT NOT NULL,
            DecimalValue DECIMAL(18,2) NOT NULL,
            DoubleValue FLOAT NOT NULL,
            DateTimeValue DATETIME2(7) NOT NULL,
            DateTimeOffsetValue DATETIMEOFFSET(7) NOT NULL,
            GuidValue UNIQUEIDENTIFIER NOT NULL,
            BinaryValue VARBINARY(MAX) NULL
            """);
        var dateTime = new DateTime(2026, 7, 14, 8, 0, 0, DateTimeKind.Unspecified);
        var dateTimeOffset = new DateTimeOffset(2026, 7, 14, 8, 0, 0, TimeSpan.FromHours(3));
        var guid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var payload = new byte[] { 5, 4, 3, 2, 1 };

        try
        {
            CreateDataFrame(
                [
                    Series<string?>.Create("TextValue", ["text", null]),
                    Series<bool>.Create("BoolValue", [true, false]),
                    Series<int>.Create("IntValue", [1, 2]),
                    Series<long>.Create("LongValue", [10L, 20L]),
                    Series<decimal>.Create("DecimalValue", [12.34m, 56.78m]),
                    Series<double>.Create("DoubleValue", [1.5d, 2.5d]),
                    Series<DateTime>.Create("DateTimeValue", [dateTime, dateTime.AddDays(1)]),
                    Series<DateTimeOffset>.Create("DateTimeOffsetValue", [dateTimeOffset, dateTimeOffset.AddDays(1)]),
                    Series<Guid>.Create("GuidValue", [guid, guid]),
                    Series<byte[]?>.Create("BinaryValue", [payload, null])
                ]).WriteSql(connection, table);

            var df = global::Runiq.Data.DataFrame.ReadSql(
                connection,
                $"SELECT TextValue, BoolValue, IntValue, LongValue, DecimalValue, DoubleValue, DateTimeValue, DateTimeOffsetValue, GuidValue, BinaryValue FROM {table} ORDER BY IntValue");

            Assert.Equal(["text", null], Values(df, "TextValue"));
            Assert.Equal([true, false], Values(df, "BoolValue"));
            Assert.Equal([1, 2], Values(df, "IntValue"));
            Assert.Equal([10L, 20L], Values(df, "LongValue"));
            Assert.Equal([12.34m, 56.78m], Values(df, "DecimalValue"));
            Assert.Equal([1.5d, 2.5d], Values(df, "DoubleValue"));
            Assert.Equal([dateTime, dateTime.AddDays(1)], Values(df, "DateTimeValue"));
            Assert.Equal([dateTimeOffset, dateTimeOffset.AddDays(1)], Values(df, "DateTimeOffsetValue"));
            Assert.Equal([guid, guid], Values(df, "GuidValue"));
            Assert.Equal(payload, (byte[])df["BinaryValue"].GetValue(0)!);
            Assert.Null(df["BinaryValue"].GetValue(1));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies DataFrame row order is the insert execution order by checking ordered identities and null transitions.
    [Fact]
    public void WriteSql_WithRowOrderSensitiveValues_BindsEachRowIndependently()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(connection, "RowOrder", "Id INT NOT NULL, Value NVARCHAR(100) NULL");

        try
        {
            var df = CreateDataFrame("Id", typeof(int), [3, 1, 2], "Value", typeof(string), ["third", null, "second"]);

            df.WriteSql(connection, table);

            Assert.Equal([[1, DBNull.Value], [2, "second"], [3, "third"]], QueryRows(connection, $"SELECT Id, Value FROM {table} ORDER BY Id"));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    // Verifies SQL Server defaults apply only for destination columns omitted from the DataFrame.
    [Fact]
    public void WriteSql_WhenDestinationColumnIsOmitted_AppliesDatabaseDefault()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        var table = CreateSimpleTable(connection, "Defaults", "Id INT NOT NULL, Name NVARCHAR(100) NOT NULL, CreatedBy NVARCHAR(50) NOT NULL DEFAULT N'system'");

        try
        {
            global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 }, Name = new[] { "Defaulted" } }).WriteSql(connection, table);

            Assert.Equal("system", ReadScalar<string>(connection, $"SELECT CreatedBy FROM {table} WHERE Id = 1;"));
        }
        finally
        {
            DropTable(connection, table);
        }
    }

    /// <summary>
    /// Creates the employee contract table without seed rows so WriteSql is the only insert path under test.
    /// </summary>
    private static string CreateEmployeesTable(SqlConnection connection)
    {
        return CreateSimpleTable(
            connection,
            "Employees",
            """
            Id INT NOT NULL,
            Name NVARCHAR(100) NOT NULL,
            Department NVARCHAR(100) NOT NULL,
            Salary DECIMAL(18,2) NULL,
            Active BIT NOT NULL,
            CreatedAt DATETIME2 NOT NULL,
            ExternalId UNIQUEIDENTIFIER NOT NULL,
            Payload VARBINARY(MAX) NULL
            """);
    }

    /// <summary>
    /// Creates the representative employee DataFrame used by append and mutation tests.
    /// </summary>
    private static global::Runiq.Data.DataFrame CreateEmployeesDataFrame()
    {
        return CreateDataFrame(
            [
                Series<int>.Create("Id", [1, 2, 3]),
                Series<string>.Create("Name", ["Ali", "Ay\u015fe", "Mehmet"]),
                Series<string>.Create("Department", ["Engineering", "Finance", "Engineering"]),
                Series<decimal?>.Create("Salary", [125000.50m, 110000.00m, null]),
                Series<bool>.Create("Active", [true, true, false]),
                Series<DateTime>.Create("CreatedAt", [new DateTime(2026, 7, 14, 9, 30, 0), new DateTime(2026, 7, 15, 9, 30, 0), new DateTime(2026, 7, 16, 9, 30, 0)]),
                Series<Guid>.Create("ExternalId", [Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222"), Guid.Parse("33333333-3333-3333-3333-333333333333")]),
                Series<byte[]?>.Create("Payload", [new byte[] { 1, 2, 3, 4 }, new byte[] { 5, 6, 7, 8 }, null])
            ]);
    }

    /// <summary>
    /// Asserts employee values through a real SELECT ordered by Id because SQL Server does not guarantee natural row order.
    /// </summary>
    private static void AssertEmployees(SqlConnection connection, string table)
    {
        var rows = QueryRows(connection, $"SELECT Id, Name, Department, Salary, Active, CreatedAt, ExternalId, Payload FROM {table} ORDER BY Id");
        Assert.Equal(3, rows.Count);
        Assert.Equal([1, "Ali", "Engineering", 125000.50m, true, new DateTime(2026, 7, 14, 9, 30, 0), Guid.Parse("11111111-1111-1111-1111-111111111111"), new byte[] { 1, 2, 3, 4 }], rows[0]);
        Assert.Equal([2, "Ay\u015fe", "Finance", 110000.00m, true, new DateTime(2026, 7, 15, 9, 30, 0), Guid.Parse("22222222-2222-2222-2222-222222222222"), new byte[] { 5, 6, 7, 8 }], rows[1]);
        Assert.Equal(3, rows[2][0]);
        Assert.Equal(DBNull.Value, rows[2][3]);
        Assert.Equal(DBNull.Value, rows[2][7]);
    }

    /// <summary>
    /// Creates a generated table name and executes SQL Server DDL on a caller-owned open connection.
    /// </summary>
    private static string CreateSimpleTable(SqlConnection connection, string prefix, string columns)
    {
        var table = UniqueName(prefix);
        ExecuteNonQuery(connection, $"CREATE TABLE {table} ({columns});");
        return table;
    }

    /// <summary>
    /// Executes command text against a caller-owned open connection without taking ownership of it.
    /// </summary>
    private static void ExecuteNonQuery(SqlConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Executes command text inside a caller-owned transaction that must remain usable by the test.
    /// </summary>
    private static void ExecuteNonQuery(SqlConnection connection, SqlTransaction transaction, string commandText)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Drops a generated table; cleanup runs in finally blocks so object artifacts do not survive successful or failed tests.
    /// </summary>
    private static void DropTable(SqlConnection connection, string table)
    {
        ExecuteNonQuery(connection, $"DROP TABLE IF EXISTS {table};");
    }

    /// <summary>
    /// Ensures a caller-owned connection is open before cleanup after closed-connection ownership tests.
    /// </summary>
    private static void EnsureOpen(SqlConnection connection)
    {
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }
    }

    /// <summary>
    /// Counts rows outside an external transaction to verify committed or rolled-back database state.
    /// </summary>
    private static int CountRows(SqlConnection connection, string table)
    {
        return ReadScalar<int>(connection, $"SELECT COUNT(*) FROM {table};");
    }

    /// <summary>
    /// Counts rows inside the supplied caller-owned transaction without committing or rolling it back.
    /// </summary>
    private static int CountRows(SqlConnection connection, SqlTransaction transaction, string table)
    {
        return ReadScalar<int>(connection, transaction, $"SELECT COUNT(*) FROM {table};");
    }

    /// <summary>
    /// Reads a scalar value from SQL Server for precise contract assertions.
    /// </summary>
    private static T ReadScalar<T>(SqlConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return (T)command.ExecuteScalar()!;
    }

    /// <summary>
    /// Reads a scalar value within an external transaction while leaving transaction ownership with the caller.
    /// </summary>
    private static T ReadScalar<T>(SqlConnection connection, SqlTransaction transaction, string commandText)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        return (T)command.ExecuteScalar()!;
    }

    /// <summary>
    /// Reads one SQL Server column into a typed array for mapping assertions.
    /// </summary>
    private static T[] QueryColumn<T>(SqlConnection connection, string commandText)
    {
        return QueryRows(connection, commandText).Select(row => (T)row[0]!).ToArray();
    }

    /// <summary>
    /// Reads SQL Server rows as object arrays while preserving provider CLR values and DBNull markers.
    /// </summary>
    private static List<object?[]> QueryRows(SqlConnection connection, string commandText)
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
        return CreateDataFrame(
            [
                CreateSeries(firstName, firstType, firstValues),
                CreateSeries(secondName, secondType, secondValues)
            ]);
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
    /// Generates a simple SQL identifier containing only letters, digits, and underscores.
    /// </summary>
    private static string UniqueName(string prefix)
    {
        return $"{prefix}_{Guid.NewGuid():N}";
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
}
