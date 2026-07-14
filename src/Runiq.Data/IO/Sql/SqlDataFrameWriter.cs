using System.Data;
using System.Data.Common;
using Runiq.Data.Series;

namespace Runiq.Data.IO;

/// <summary>
/// Appends DataFrame rows to an existing SQL table through provider-independent ADO.NET objects.
/// </summary>
internal static class SqlDataFrameWriter
{
    private static readonly Dictionary<Type, DbType> TypeMappings = new()
    {
        [typeof(string)] = DbType.String,
        [typeof(char)] = DbType.String,
        [typeof(bool)] = DbType.Boolean,
        [typeof(byte)] = DbType.Byte,
        [typeof(sbyte)] = DbType.Int16,
        [typeof(short)] = DbType.Int16,
        [typeof(ushort)] = DbType.Int32,
        [typeof(int)] = DbType.Int32,
        [typeof(uint)] = DbType.Int64,
        [typeof(long)] = DbType.Int64,
        [typeof(ulong)] = DbType.Decimal,
        [typeof(float)] = DbType.Single,
        [typeof(double)] = DbType.Double,
        [typeof(decimal)] = DbType.Decimal,
        [typeof(DateTime)] = DbType.DateTime2,
        [typeof(DateTimeOffset)] = DbType.DateTimeOffset,
        [typeof(DateOnly)] = DbType.Date,
        [typeof(TimeOnly)] = DbType.Time,
        [typeof(TimeSpan)] = DbType.Time,
        [typeof(Guid)] = DbType.Guid,
        [typeof(byte[])] = DbType.Binary
    };

    /// <summary>
    /// Validates SQL Write inputs and appends DataFrame rows to the destination table.
    /// </summary>
    /// <param name="dataFrame">The DataFrame whose current rows and columns are written without mutation.</param>
    /// <param name="connection">The caller-owned connection used for provider command creation and execution.</param>
    /// <param name="tableName">The validated one-part or two-part destination table identifier.</param>
    /// <param name="options">The caller-supplied transaction and timeout options.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when identifiers, column types, or runtime values are not supported by SQL Write.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when connection, transaction, or affected-row-count contracts are violated.
    /// </exception>
    internal static void Write(DataFrame dataFrame, DbConnection connection, string tableName, SqlWriteOptions options)
    {
        ArgumentNullException.ThrowIfNull(dataFrame);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(options);

        ValidateCommandTimeout(options.CommandTimeout);
        ValidateTableIdentifier(tableName);
        ValidateColumns(dataFrame.ColumnSeries);
        ValidateTransaction(connection, options.Transaction);

        if (dataFrame.RowTotalCore == 0)
        {
            return;
        }

        WriteRows(dataFrame, connection, tableName, options);
    }

    private static void ValidateCommandTimeout(int? commandTimeout)
    {
        if (commandTimeout <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SqlWriteOptions.CommandTimeout), commandTimeout, "SQL Write command timeout must be greater than zero.");
        }
    }

    private static void ValidateColumns(IReadOnlyList<ISeries> columns)
    {
        if (columns.Count == 0)
        {
            throw new ArgumentException("SQL Write requires at least one DataFrame column.");
        }

        foreach (var column in columns)
        {
            ValidateColumnIdentifier(column.Name);
            ResolveDbType(column.DataType, column.Name);
        }
    }

    private static void ValidateTransaction(DbConnection connection, DbTransaction? transaction)
    {
        if (transaction is null)
        {
            return;
        }

        if (transaction.Connection is null)
        {
            throw new InvalidOperationException("SQL Write external transaction must have an associated connection.");
        }

        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new InvalidOperationException("SQL Write external transaction must belong to the supplied connection instance.");
        }

        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("SQL Write requires an open connection when an external transaction is supplied.");
        }
    }

    private static void WriteRows(DataFrame dataFrame, DbConnection connection, string tableName, SqlWriteOptions options)
    {
        var openedHere = false;
        ValidateConnectionState(connection.State);
        if (connection.State == ConnectionState.Closed)
        {
            // The connection belongs to the caller. SQL Write opens it only for this operation
            // and restores the original closed state in the finally block on success or failure.
            connection.Open();
            openedHere = true;
        }

        try
        {
            if (options.Transaction is null)
            {
                WriteRowsWithInternalTransaction(dataFrame, connection, tableName, options.CommandTimeout);
            }
            else
            {
                WriteRowsWithTransaction(dataFrame, connection, tableName, options.CommandTimeout, options.Transaction);
            }
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

        throw new InvalidOperationException($"SQL Write requires an open or closed connection, but the connection state is '{state}'.");
    }

    private static void WriteRowsWithInternalTransaction(
        DataFrame dataFrame,
        DbConnection connection,
        string tableName,
        int? commandTimeout)
    {
        using var transaction = connection.BeginTransaction();
        try
        {
            WriteRowsWithTransaction(dataFrame, connection, tableName, commandTimeout, transaction);
            transaction.Commit();
        }
        catch
        {
            // Rollback is best-effort cleanup for an internally owned transaction. If rollback
            // itself fails, the original write exception remains the failure reported to callers.
            try
            {
                transaction.Rollback();
            }
            catch
            {
            }

            throw;
        }
    }

    private static void WriteRowsWithTransaction(
        DataFrame dataFrame,
        DbConnection connection,
        string tableName,
        int? commandTimeout,
        DbTransaction transaction)
    {
        using var command = CreateInsertCommand(dataFrame.ColumnSeries, connection, tableName, commandTimeout, transaction);
        ExecuteRows(dataFrame, command);
    }

    private static DbCommand CreateInsertCommand(
        IReadOnlyList<ISeries> columns,
        DbConnection connection,
        string tableName,
        int? commandTimeout,
        DbTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        if (commandTimeout.HasValue)
        {
            command.CommandTimeout = commandTimeout.Value;
        }

        command.CommandText = CreateInsertCommandText(tableName, columns);
        for (var index = 0; index < columns.Count; index++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = CreateParameterName(index);
            parameter.DbType = ResolveDbType(columns[index].DataType, columns[index].Name);
            command.Parameters.Add(parameter);
        }

        return command;
    }

    private static string CreateInsertCommandText(string tableName, IReadOnlyList<ISeries> columns)
    {
        var columnList = string.Join(", ", columns.Select(static column => column.Name));
        var parameterList = string.Join(", ", Enumerable.Range(0, columns.Count).Select(CreateParameterName));
        return $"INSERT INTO {tableName} ({columnList}) VALUES ({parameterList})";
    }

    private static string CreateParameterName(int index)
    {
        return $"@p{index}";
    }

    private static void ExecuteRows(DataFrame dataFrame, DbCommand command)
    {
        var columns = dataFrame.ColumnSeries;
        for (var rowIndex = 0; rowIndex < dataFrame.RowTotalCore; rowIndex++)
        {
            // Parameters are created once with stable names and DbType values. Only Value is
            // replaced per row so provider commands can be reused without rebuilding SQL text.
            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                command.Parameters[columnIndex].Value = CreateParameterValue(columns[columnIndex], rowIndex);
            }

            var affectedRows = command.ExecuteNonQuery();
            if (affectedRows != 1)
            {
                throw new InvalidOperationException($"SQL Write expected one affected row but the provider reported {affectedRows} for row {rowIndex}.");
            }
        }
    }

    private static object CreateParameterValue(ISeries column, int rowIndex)
    {
        var value = column.GetValue(rowIndex);
        if (value is null)
        {
            return DBNull.Value;
        }

        if (value is DBNull)
        {
            throw new ArgumentException($"Column '{column.Name}' contains unsupported SQL Write value type '{typeof(DBNull)}' at row {rowIndex}.");
        }

        if (value is char character)
        {
            return character.ToString();
        }

        if (value is byte[] bytes)
        {
            return bytes.ToArray();
        }

        if (value is sbyte signedByte)
        {
            // SQL providers are not required to expose unsigned or SByte parameter types.
            // Widening keeps the value exact while using provider-independent DbType values.
            return (short)signedByte;
        }

        if (value is ushort unsignedShort)
        {
            return (int)unsignedShort;
        }

        if (value is uint unsignedInteger)
        {
            return (long)unsignedInteger;
        }

        if (value is ulong unsignedLong)
        {
            return (decimal)unsignedLong;
        }

        var valueType = value.GetType();
        if (!IsSupportedType(valueType) || valueType.IsEnum)
        {
            throw new ArgumentException(
                $"Column '{column.Name}' contains unsupported SQL Write value type '{valueType}' at row {rowIndex}.");
        }

        if (value is float single && !float.IsFinite(single))
        {
            throw new ArgumentException($"Column '{column.Name}' contains non-finite SQL Write value type '{valueType}' at row {rowIndex}.");
        }

        if (value is double number && !double.IsFinite(number))
        {
            throw new ArgumentException($"Column '{column.Name}' contains non-finite SQL Write value type '{valueType}' at row {rowIndex}.");
        }

        return value;
    }

    private static DbType ResolveDbType(Type dataType, string columnName)
    {
        var effectiveType = Nullable.GetUnderlyingType(dataType) ?? dataType;
        if (TypeMappings.TryGetValue(effectiveType, out var dbType))
        {
            return dbType;
        }

        throw new ArgumentException($"Column '{columnName}' has unsupported SQL Write data type '{dataType}'.");
    }

    private static bool IsSupportedType(Type valueType)
    {
        return TypeMappings.ContainsKey(Nullable.GetUnderlyingType(valueType) ?? valueType);
    }

    private static void ValidateTableIdentifier(string tableName)
    {
        var segments = tableName.Split('.');
        if (segments.Length is < 1 or > 2)
        {
            throw new ArgumentException($"Table '{tableName}' cannot be used for SQL Write because only one-part or two-part simple SQL identifiers are supported.", nameof(tableName));
        }

        foreach (var segment in segments)
        {
            ValidateIdentifierSegment(segment, $"Table '{tableName}'");
        }
    }

    private static void ValidateColumnIdentifier(string columnName)
    {
        ValidateIdentifierSegment(columnName, $"Column '{columnName}'");
    }

    private static void ValidateIdentifierSegment(string segment, string target)
    {
        if (segment.Length == 0)
        {
            throw new ArgumentException($"{target} cannot be used for SQL Write because empty SQL identifier segments are not supported.");
        }

        if (!IsIdentifierStart(segment[0]))
        {
            throw new ArgumentException($"{target} cannot be used for SQL Write because only simple SQL identifiers are supported.");
        }

        for (var index = 1; index < segment.Length; index++)
        {
            if (!IsIdentifierPart(segment[index]))
            {
                throw new ArgumentException($"{target} cannot be used for SQL Write because only simple SQL identifiers are supported.");
            }
        }
    }

    private static bool IsIdentifierStart(char character)
    {
        return character == '_' || char.IsLetter(character);
    }

    private static bool IsIdentifierPart(char character)
    {
        return character == '_' || char.IsLetterOrDigit(character);
    }
}
