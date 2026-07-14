using System.Data;
using Npgsql;

namespace Runiq.Data.PostgreSql.IntegrationTests;

/// <summary>
/// Verifies SQL Read contracts against a real PostgreSQL server and the Npgsql ADO.NET provider.
/// </summary>
[Collection(PostgreSqlCollection.Name)]
public sealed class DataFramePostgreSqlReadTests(PostgreSqlContainerFixture fixture)
{
    // Verifies that the DbConnection overload reads a real PostgreSQL result set with schema, nulls, and row order.
    [Fact]
    public void ReadSql_WithPostgreSqlConnectionAndCommandText_ReadsEmployees()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlTestSchema.Create(connection);
        schema.CreateEmployees();

        var df = global::Runiq.Data.DataFrame.ReadSql(
            connection,
            $"""
            SELECT id, name, department, salary, active, created_at, created_at_utc, external_id, payload
            FROM {schema.Employees}
            ORDER BY id
            """);

        Assert.Equal(9, df.Schema.Count);
        Assert.Equal(["id", "name", "department", "salary", "active", "created_at", "created_at_utc", "external_id", "payload"], ColumnNames(df));
        Assert.Equal([1, 2, 3], Values(df, "id"));
        Assert.Equal(["Ali", "Ayse", "Mehmet"], Values(df, "name"));
        Assert.Equal(["Engineering", "Finance", "Engineering"], Values(df, "department"));
        Assert.Equal(125000.50m, df["salary"].GetValue(0));
        Assert.Null(df["salary"].GetValue(2));
        Assert.Equal([true, true, false], Values(df, "active"));
        Assert.Equal(new DateTime(2026, 7, 14, 9, 30, 0), df["created_at"].GetValue(0));
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), df["external_id"].GetValue(0));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, (byte[])df["payload"].GetValue(0)!);
    }

    // Verifies that the DbCommand overload leaves a consumer-owned NpgsqlCommand reusable after reading.
    [Fact]
    public void ReadSql_WithNpgsqlCommand_ReadsRowsAndLeavesCommandUsable()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlTestSchema.Create(connection);
        schema.CreateEmployees();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT id, name FROM {schema.Employees} ORDER BY id";

        var df = global::Runiq.Data.DataFrame.ReadSql(command);
        var scalar = command.ExecuteScalar();

        Assert.Equal([1, 2, 3], Values(df, "id"));
        Assert.Equal(1, scalar);
        Assert.Equal($"SELECT id, name FROM {schema.Employees} ORDER BY id", command.CommandText);
        Assert.Same(connection, command.Connection);
    }

    // Verifies that real NpgsqlParameter usage filters rows and preserves mutable command state.
    [Fact]
    public void ReadSql_WithNpgsqlParameter_PreservesCommandState()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlTestSchema.Create(connection);
        schema.CreateEmployees();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT id, name, salary
            FROM {schema.Employees}
            WHERE department = @department
            ORDER BY id
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 77;
        var parameter = command.Parameters.AddWithValue("@department", "Engineering");

        var df = global::Runiq.Data.DataFrame.ReadSql(command);

        Assert.Equal([1, 3], Values(df, "id"));
        Assert.Equal(["Ali", "Mehmet"], Values(df, "name"));
        Assert.Single(command.Parameters);
        Assert.Same(parameter, command.Parameters[0]);
        Assert.Equal("Engineering", command.Parameters["@department"].Value);
        Assert.Equal(CommandType.Text, command.CommandType);
        Assert.Equal(77, command.CommandTimeout);
        Assert.Same(connection, command.Connection);
    }

    // Verifies PostgreSQL primitive mappings returned by Npgsql without Runiq.Data conversions.
    [Fact]
    public void ReadSql_WithPostgreSqlPrimitiveTypes_PreservesProviderClrTypes()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlTestSchema.Create(connection);
        schema.Execute(
            $"""
            CREATE TABLE {schema.Qualified("type_values")} (
                smallint_value smallint NOT NULL,
                integer_value integer NOT NULL,
                bigint_value bigint NOT NULL,
                boolean_value boolean NOT NULL,
                numeric_value numeric(18,2) NOT NULL,
                real_value real NOT NULL,
                double_value double precision NOT NULL,
                text_value text NOT NULL,
                varchar_value varchar(100) NOT NULL,
                timestamp_value timestamp without time zone NOT NULL,
                timestamptz_value timestamp with time zone NOT NULL,
                date_value date NOT NULL,
                time_value time without time zone NOT NULL,
                interval_value interval NOT NULL,
                uuid_value uuid NOT NULL,
                bytea_value bytea NOT NULL
            );

            INSERT INTO {schema.Qualified("type_values")} VALUES (
                CAST(42 AS smallint),
                420000,
                9000000000,
                true,
                12345.67,
                CAST(1.25 AS real),
                CAST(2.5 AS double precision),
                '123',
                'true',
                TIMESTAMP '2026-07-14 10:15:30',
                TIMESTAMPTZ '2026-07-14 10:15:30+03',
                DATE '2026-07-14',
                TIME '10:15:30',
                INTERVAL '2 hours 3 minutes',
                '44444444-4444-4444-4444-444444444444',
                decode('01020304', 'hex')
            );
            """);

        var df = global::Runiq.Data.DataFrame.ReadSql(connection, $"SELECT * FROM {schema.Qualified("type_values")}");

        Assert.IsType<short>(df["smallint_value"].GetValue(0));
        Assert.IsType<int>(df["integer_value"].GetValue(0));
        Assert.IsType<long>(df["bigint_value"].GetValue(0));
        Assert.IsType<bool>(df["boolean_value"].GetValue(0));
        Assert.IsType<decimal>(df["numeric_value"].GetValue(0));
        Assert.IsType<float>(df["real_value"].GetValue(0));
        Assert.IsType<double>(df["double_value"].GetValue(0));
        Assert.IsType<string>(df["text_value"].GetValue(0));
        Assert.IsType<string>(df["varchar_value"].GetValue(0));
        Assert.IsType<DateTime>(df["timestamp_value"].GetValue(0));
        Assert.IsType<DateTime>(df["timestamptz_value"].GetValue(0));
        Assert.IsType<DateOnly>(df["date_value"].GetValue(0));
        Assert.IsType<TimeOnly>(df["time_value"].GetValue(0));
        Assert.IsType<TimeSpan>(df["interval_value"].GetValue(0));
        Assert.IsType<Guid>(df["uuid_value"].GetValue(0));
        Assert.IsType<byte[]>(df["bytea_value"].GetValue(0));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, (byte[])df["bytea_value"].GetValue(0)!);
    }

    // Verifies that PostgreSQL text and varchar values are not parsed into numbers, booleans, nulls, or dates.
    [Fact]
    public void ReadSql_WithPostgreSqlTextValues_DoesNotParseStrings()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlTestSchema.Create(connection);
        schema.Execute(
            $"""
            CREATE TABLE {schema.Qualified("text_values")} (text_value text NOT NULL, varchar_value varchar(100) NOT NULL);
            INSERT INTO {schema.Qualified("text_values")} VALUES
                ('123', '123'),
                ('true', 'true'),
                ('null', 'null'),
                ('2026-07-14', '2026-07-14');
            """);

        var df = global::Runiq.Data.DataFrame.ReadSql(connection, $"SELECT text_value, varchar_value FROM {schema.Qualified("text_values")} ORDER BY text_value");

        Assert.Equal(["123", "2026-07-14", "null", "true"], Values(df, "text_value"));
        Assert.Equal(["123", "2026-07-14", "null", "true"], Values(df, "varchar_value"));
        Assert.All(Enumerable.Range(0, df["text_value"].Count), row => Assert.IsType<string>(df["text_value"].GetValue(row)));
        Assert.All(Enumerable.Range(0, df["varchar_value"].Count), row => Assert.IsType<string>(df["varchar_value"].GetValue(row)));
    }

    // Verifies Npgsql timestamp contracts: timestamp is unspecified DateTime and timestamptz is UTC DateTime.
    [Fact]
    public void ReadSql_WithPostgreSqlTimestamps_PreservesNpgsqlDateTimeBehavior()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlTestSchema.Create(connection);
        schema.Execute(
            $"""
            CREATE TABLE {schema.Qualified("timestamps")} (
                timestamp_value timestamp without time zone NOT NULL,
                timestamptz_value timestamp with time zone NOT NULL
            );
            INSERT INTO {schema.Qualified("timestamps")} VALUES (
                TIMESTAMP '2026-07-14 10:15:30',
                TIMESTAMPTZ '2026-07-14 10:15:30+03'
            );
            """);

        var df = global::Runiq.Data.DataFrame.ReadSql(connection, $"SELECT timestamp_value, timestamptz_value FROM {schema.Qualified("timestamps")}");
        var timestamp = Assert.IsType<DateTime>(df["timestamp_value"].GetValue(0));
        var timestampWithTimeZone = Assert.IsType<DateTime>(df["timestamptz_value"].GetValue(0));

        Assert.Equal(new DateTime(2026, 7, 14, 10, 15, 30, DateTimeKind.Unspecified), timestamp);
        Assert.Equal(DateTimeKind.Unspecified, timestamp.Kind);
        Assert.Equal(new DateTime(2026, 7, 14, 7, 15, 30, DateTimeKind.Utc), timestampWithTimeZone);
        Assert.Equal(DateTimeKind.Utc, timestampWithTimeZone.Kind);
    }

    // Verifies that SQL NULL values become DataFrame null values instead of DBNull.Value.
    [Fact]
    public void ReadSql_WithPostgreSqlNull_MapsToNull()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlTestSchema.Create(connection);
        schema.CreateEmployees();

        var df = global::Runiq.Data.DataFrame.ReadSql(connection, $"SELECT salary, payload FROM {schema.Employees} WHERE id = 3");

        Assert.Null(df["salary"].GetValue(0));
        Assert.Null(df["payload"].GetValue(0));
        Assert.NotSame(DBNull.Value, df["salary"].GetValue(0));
    }

    // Verifies that an empty PostgreSQL result preserves metadata-derived schema and column order.
    [Fact]
    public void ReadSql_WithPostgreSqlEmptyResult_PreservesSchema()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlTestSchema.Create(connection);
        schema.CreateEmployees();

        var df = global::Runiq.Data.DataFrame.ReadSql(connection, $"SELECT id, name, salary FROM {schema.Employees} WHERE false");

        Assert.Equal(0, df["id"].Count);
        Assert.Equal(3, df.Schema.Count);
        Assert.Equal(["id", "name", "salary"], ColumnNames(df));
    }

    // Verifies quoted alias casing because PostgreSQL folds unquoted identifiers to lowercase.
    [Fact]
    public void ReadSql_WithPostgreSqlQuotedAliases_PreservesAliasCasing()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlTestSchema.Create(connection);
        schema.CreateEmployees();

        var df = global::Runiq.Data.DataFrame.ReadSql(
            connection,
            $"""
            SELECT id AS "EmployeeID", name AS "employeeName"
            FROM {schema.Employees}
            WHERE id = 1
            """);

        Assert.Equal(["EmployeeID", "employeeName"], ColumnNames(df));
    }

    // Verifies that projected PostgreSQL column order is preserved exactly as provider metadata reports it.
    [Fact]
    public void ReadSql_WithPostgreSqlProjectionOrder_PreservesColumnOrder()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlTestSchema.Create(connection);
        schema.CreateEmployees();

        var df = global::Runiq.Data.DataFrame.ReadSql(connection, $"SELECT name, active, id FROM {schema.Employees} ORDER BY id");

        Assert.Equal(["name", "active", "id"], ColumnNames(df));
    }

    // Verifies that ORDER BY row order is preserved and Runiq.Data does not apply its own sorting.
    [Fact]
    public void ReadSql_WithPostgreSqlOrderByDescending_PreservesRowOrder()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlTestSchema.Create(connection);
        schema.CreateEmployees();

        var df = global::Runiq.Data.DataFrame.ReadSql(connection, $"SELECT id FROM {schema.Employees} ORDER BY id DESC");

        Assert.Equal([3, 2, 1], Values(df, "id"));
    }

    // Verifies observed Npgsql duplicate-name metadata and Runiq.Data fail-fast behavior when duplicates are reported.
    [Fact]
    public void ReadSql_WithPostgreSqlDuplicateColumns_RejectsDuplicateProviderNames()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlTestSchema.Create(connection);
        schema.CreateEmployees();
        string[] providerNames;
        using (var metadataCommand = connection.CreateCommand())
        {
            metadataCommand.CommandText = $"SELECT id, id FROM {schema.Employees}";
            using var reader = metadataCommand.ExecuteReader();
            providerNames = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        }

        var exception = Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadSql(connection, $"SELECT id, id FROM {schema.Employees}"));

        Assert.NotEqual(providerNames.Distinct(StringComparer.OrdinalIgnoreCase).Count(), providerNames.Length);
        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Verifies that PostgreSQL expression columns are read by alias with decimal provider mapping.
    [Fact]
    public void ReadSql_WithPostgreSqlExpressionColumn_ReadsAliasAndDecimalValue()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlTestSchema.Create(connection);
        schema.CreateEmployees();

        var df = global::Runiq.Data.DataFrame.ReadSql(
            connection,
            $"""
            SELECT
                id,
                salary * CAST(1.10 AS numeric(18,2)) AS adjusted_salary
            FROM {schema.Employees}
            WHERE salary IS NOT NULL
            ORDER BY id
            """);

        Assert.Equal(["id", "adjusted_salary"], ColumnNames(df));
        Assert.Equal([1, 2], Values(df, "id"));
        Assert.IsType<decimal>(df["adjusted_salary"].GetValue(0));
        Assert.Equal(137500.5500m, df["adjusted_salary"].GetValue(0));
    }

    // Verifies that a caller-opened NpgsqlConnection remains open and usable after SQL Read completes.
    [Fact]
    public void ReadSql_WithOpenPostgreSqlConnection_LeavesConnectionOpen()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlTestSchema.Create(connection);
        schema.CreateEmployees();

        global::Runiq.Data.DataFrame.ReadSql(connection, $"SELECT id FROM {schema.Employees} WHERE id = 1");

        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Equal(3, schema.CountRows("employees"));
    }

    // Verifies that a closed NpgsqlConnection is opened temporarily, restored to Closed, and remains reusable.
    [Fact]
    public void ReadSql_WithClosedPostgreSqlConnection_RestoresClosedStateAndRemainsReusable()
    {
        string schemaName;
        using (var setupConnection = fixture.CreateConnection())
        {
            setupConnection.Open();
            using var setupSchema = PostgreSqlTestSchema.Create(setupConnection, dropOnDispose: false);
            setupSchema.CreateEmployees();
            schemaName = setupSchema.Name;
        }

        using var connection = fixture.CreateConnection();
        try
        {
            var employees = PostgreSqlTestSchema.Qualified(schemaName, "employees");
            var df = global::Runiq.Data.DataFrame.ReadSql(connection, $"SELECT id FROM {employees} ORDER BY id");

            Assert.Equal([1, 2, 3], Values(df, "id"));
            Assert.Equal(ConnectionState.Closed, connection.State);
            connection.Open();
            using var cleanupSchema = new PostgreSqlTestSchema(connection, schemaName);
            Assert.Equal(3, cleanupSchema.CountRows("employees"));
        }
        finally
        {
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            using var cleanupSchema = new PostgreSqlTestSchema(connection, schemaName);
        }
    }

    // Verifies provider exception propagation and ownership restoration for invalid SQL on open and closed connections.
    [Fact]
    public void ReadSql_WithInvalidPostgreSqlQuery_PreservesConnectionAndCommandOwnership()
    {
        using var openConnection = fixture.CreateConnection();
        openConnection.Open();
        using var schema = PostgreSqlTestSchema.Create(openConnection);
        schema.CreateEmployees();
        using var command = openConnection.CreateCommand();
        command.CommandText = $"SELECT missing_column FROM {schema.Employees}";

        Assert.Throws<PostgresException>(() => global::Runiq.Data.DataFrame.ReadSql(command));
        Assert.Equal(ConnectionState.Open, openConnection.State);
        command.CommandText = $"SELECT COUNT(*) FROM {schema.Employees}";
        Assert.Equal(3L, command.ExecuteScalar());

        using var closedConnection = fixture.CreateConnection();
        Assert.Throws<PostgresException>(() => global::Runiq.Data.DataFrame.ReadSql(closedConnection, $"SELECT missing_column FROM {schema.Employees}"));
        Assert.Equal(ConnectionState.Closed, closedConnection.State);
        closedConnection.Open();
        using var verification = new PostgreSqlTestSchema(closedConnection, schema.Name, dropOnDispose: false);
        Assert.Equal(3, verification.CountRows("employees"));
    }

    // Verifies that real Npgsql multiple result sets are rejected and reader cleanup preserves connection ownership.
    [Fact]
    public void ReadSql_WithPostgreSqlMultipleResultSets_Throws()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();
        using var schema = PostgreSqlTestSchema.Create(connection);
        schema.CreateEmployees();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT id FROM {schema.Employees};
            SELECT name FROM {schema.Employees};
            """;

        var exception = Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadSql(command));

        Assert.Contains("multiple result sets", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ConnectionState.Open, connection.State);
        command.CommandText = $"SELECT COUNT(*) FROM {schema.Employees}";
        Assert.Equal(3L, command.ExecuteScalar());
    }

    // Verifies that PostgreSQL int arrays are rejected without string fallback and with row-level diagnostics.
    [Fact]
    public void ReadSql_WithPostgreSqlArray_RejectsUnsupportedProviderType()
    {
        using var connection = fixture.CreateConnection();
        connection.Open();

        var exception = Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadSql(connection, "SELECT ARRAY[1, 2, 3] AS numbers"));

        Assert.Contains("numbers", exception.Message, StringComparison.Ordinal);
        Assert.Contains("row 0", exception.Message, StringComparison.Ordinal);
        Assert.Contains("System.Int32[]", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reads DataFrame column names in provider ordinal order.
    /// </summary>
    private static string[] ColumnNames(global::Runiq.Data.DataFrame df)
    {
        return df.Schema.Columns.Select(column => column.Name).ToArray();
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
    /// Owns one generated PostgreSQL schema and drops it with CASCADE during cleanup.
    /// </summary>
    private sealed class PostgreSqlTestSchema : IDisposable
    {
        private readonly NpgsqlConnection connection;
        private readonly bool dropOnDispose;

        /// <summary>
        /// Wraps an existing generated schema name without taking ownership of the connection.
        /// </summary>
        internal PostgreSqlTestSchema(NpgsqlConnection connection, string name, bool dropOnDispose = true)
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
        /// Gets the qualified employees table identifier for generated test SQL.
        /// </summary>
        internal string Employees => Qualified("employees");

        /// <summary>
        /// Creates a new safe schema name and initializes it on the caller-owned open connection.
        /// </summary>
        internal static PostgreSqlTestSchema Create(NpgsqlConnection connection, bool dropOnDispose = true)
        {
            var schema = new PostgreSqlTestSchema(connection, UniqueSchemaName(), dropOnDispose);
            schema.Execute($"CREATE SCHEMA {QuoteIdentifier(schema.Name)};");
            return schema;
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
            return $"{QuoteIdentifier(schema)}.{QuoteIdentifier(table)}";
        }

        /// <summary>
        /// Creates representative employee rows with PostgreSQL value types used by contract tests.
        /// </summary>
        internal void CreateEmployees()
        {
            Execute(
                $"""
                CREATE TABLE {Employees} (
                    id integer NOT NULL,
                    name text NOT NULL,
                    department text NOT NULL,
                    salary numeric(18,2) NULL,
                    active boolean NOT NULL,
                    created_at timestamp without time zone NOT NULL,
                    created_at_utc timestamp with time zone NOT NULL,
                    external_id uuid NOT NULL,
                    payload bytea NULL
                );

                INSERT INTO {Employees} (id, name, department, salary, active, created_at, created_at_utc, external_id, payload) VALUES
                    (1, 'Ali', 'Engineering', 125000.50, true, TIMESTAMP '2026-07-14 09:30:00', TIMESTAMPTZ '2026-07-14 09:30:00+00', '11111111-1111-1111-1111-111111111111', decode('01020304', 'hex')),
                    (2, 'Ayse', 'Finance', 110000.00, true, TIMESTAMP '2026-07-15 09:30:00', TIMESTAMPTZ '2026-07-15 09:30:00+00', '22222222-2222-2222-2222-222222222222', decode('05060708', 'hex')),
                    (3, 'Mehmet', 'Engineering', NULL, false, TIMESTAMP '2026-07-16 09:30:00', TIMESTAMPTZ '2026-07-16 09:30:00+00', '33333333-3333-3333-3333-333333333333', NULL);
                """);
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
        /// Counts rows in a generated table to verify connection reuse after SQL Read.
        /// </summary>
        internal int CountRows(string table)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {Qualified(table)}";
            return Convert.ToInt32(command.ExecuteScalar());
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

            Execute($"DROP SCHEMA IF EXISTS {QuoteIdentifier(Name)} CASCADE;");
        }

        /// <summary>
        /// Generates an internal schema identifier using only lowercase letters, digits, and underscores.
        /// </summary>
        private static string UniqueSchemaName()
        {
            return $"test_{Guid.NewGuid():N}";
        }

        /// <summary>
        /// Quotes a generated PostgreSQL identifier after enforcing the safe internal identifier format.
        /// </summary>
        private static string QuoteIdentifier(string identifier)
        {
            if (identifier.Any(static character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
            {
                throw new ArgumentException($"Generated PostgreSQL identifier '{identifier}' contains an unsafe character.", nameof(identifier));
            }

            return $"""
                "{identifier}"
                """.Trim();
        }
    }
}
