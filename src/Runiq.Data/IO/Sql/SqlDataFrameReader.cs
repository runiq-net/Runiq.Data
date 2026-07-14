using System.Data;
using System.Data.Common;
using Runiq.Data.Series;

namespace Runiq.Data.IO;

/// <summary>
/// Converts one provider-independent ADO.NET result set into a DataFrame while preserving
/// provider column ordinals, row order, and caller ownership of database objects.
/// </summary>
internal static class SqlDataFrameReader
{
    private static readonly HashSet<Type> SupportedTypes =
    [
        typeof(string),
        typeof(char),
        typeof(bool),
        typeof(byte),
        typeof(sbyte),
        typeof(short),
        typeof(ushort),
        typeof(int),
        typeof(uint),
        typeof(long),
        typeof(ulong),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(DateOnly),
        typeof(TimeOnly),
        typeof(TimeSpan),
        typeof(Guid),
        typeof(byte[])
    ];

    internal static DataFrame Read(DbConnection connection, string commandText)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);

        using var command = connection.CreateCommand();
        command.CommandText = commandText;

        return ReadCommand(command);
    }

    internal static DataFrame Read(DbCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Connection is null)
        {
            throw new InvalidOperationException("The SQL command must have an associated connection.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(command.CommandText);

        return ReadCommand(command);
    }

    private static DataFrame ReadCommand(DbCommand command)
    {
        return ReadWithConnectionState(command);
    }

    private static DataFrame ReadWithConnectionState(DbCommand command)
    {
        var connection = command.Connection!;
        var openedHere = false;

        ValidateConnectionState(connection.State);
        if (connection.State == ConnectionState.Closed)
        {
            // The connection is caller-owned. Runiq.Data only opens it for the read and restores
            // the original closed state in the finally block, including execution failures.
            connection.Open();
            openedHere = true;
        }

        try
        {
            return ExecuteAndRead(command);
        }
        finally
        {
            if (openedHere)
            {
                connection.Close();
            }
        }
    }

    private static void ValidateConnectionState(ConnectionState state)
    {
        if (state is ConnectionState.Open or ConnectionState.Closed)
        {
            return;
        }

        throw new InvalidOperationException($"SQL Read requires an open or closed connection, but the connection state is '{state}'.");
    }

    private static DataFrame ExecuteAndRead(DbCommand command)
    {
        // The reader is produced by ExecuteReader and is owned by Runiq.Data; the command and
        // connection remain caller-owned and must not be disposed with it.
        using var reader = command.ExecuteReader();
        return ReadSingleResultSet(reader);
    }

    private static DataFrame ReadSingleResultSet(DbDataReader reader)
    {
        var columns = CreateColumnBuilders(reader);
        var rowIndex = 0;

        while (reader.Read())
        {
            for (var ordinal = 0; ordinal < columns.Length; ordinal++)
            {
                var value = ReadCellValue(reader, columns[ordinal].Name, ordinal, rowIndex);
                columns[ordinal].Add(value);
            }

            rowIndex++;
        }

        if (reader.NextResult())
        {
            throw new ArgumentException("The SQL command returned multiple result sets. SQL Read supports exactly one result set.");
        }

        return CreateDataFrame(columns);
    }

    private static SqlColumnBuilder[] CreateColumnBuilders(DbDataReader reader)
    {
        if (reader.FieldCount == 0)
        {
            throw new ArgumentException("The SQL command did not return a tabular result set.");
        }

        var columns = new SqlColumnBuilder[reader.FieldCount];
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
        {
            var name = reader.GetName(ordinal);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"SQL result column at ordinal {ordinal} has an empty or whitespace name.");
            }

            // Duplicate provider column names are rejected because DataFrame schemas require
            // unambiguous case-insensitive column access and SQL Read must not invent aliases.
            if (!names.Add(name))
            {
                throw new ArgumentException($"A column named '{name}' already exists in the SQL result set.");
            }

            // Provider metadata is still used for empty-result schema creation, but unsupported
            // runtime values are rejected while reading cells so diagnostics can include row
            // index and the actual CLR type returned by the provider.
            columns[ordinal] = new SqlColumnBuilder(name, reader.GetFieldType(ordinal));
        }

        return columns;
    }

    private static object? ReadCellValue(DbDataReader reader, string columnName, int ordinal, int rowIndex)
    {
        var value = reader.GetValue(ordinal);
        if (value == DBNull.Value)
        {
            // DBNull is converted to null so provider-specific database null markers never
            // escape into the DataFrame cell model.
            return null;
        }

        if (value is byte[] bytes)
        {
            return bytes.ToArray();
        }

        var valueType = value.GetType();
        if (!IsSupportedType(valueType))
        {
            throw new ArgumentException(
                $"Column '{columnName}' contains unsupported SQL value type '{valueType}' at row {rowIndex}.");
        }

        return value;
    }

    private static bool IsSupportedType(Type type)
    {
        return SupportedTypes.Contains(Nullable.GetUnderlyingType(type) ?? type);
    }

    private static DataFrame CreateDataFrame(IReadOnlyList<SqlColumnBuilder> columns)
    {
        var series = new ISeries[columns.Count];
        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            var dataType = column.ResolveDataType();
            series[index] = DataFrame.CreateSeriesFromValues(column.Name, dataType, column.Values);
        }

        return DataFrame.CreateFromSeries(series);
    }

    /// <summary>
    /// Accumulates one SQL column and resolves the final CLR type after rows are read.
    /// </summary>
    private sealed class SqlColumnBuilder(string name, Type metadataType)
    {
        private Type? valueType;
        private bool containsNull;

        internal string Name { get; } = name;

        internal List<object?> Values { get; } = [];

        internal void Add(object? value)
        {
            Values.Add(value);
            if (value is null)
            {
                containsNull = true;
                return;
            }

            var currentType = value.GetType();
            if (valueType is null)
            {
                valueType = currentType;
                return;
            }

            if (valueType != currentType)
            {
                throw new ArgumentException(
                    $"Column '{Name}' contains mixed SQL value types '{valueType}' and '{currentType}', which cannot be represented safely.");
            }
        }

        internal Type ResolveDataType()
        {
            var resolvedType = valueType ?? metadataType;
            if (containsNull && resolvedType.IsValueType)
            {
                return typeof(Nullable<>).MakeGenericType(resolvedType);
            }

            return resolvedType;
        }
    }
}
