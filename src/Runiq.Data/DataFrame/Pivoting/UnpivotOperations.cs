using System.Collections;
using Runiq.Data.Series;

namespace Runiq.Data;

/// <summary>
/// Builds unpivoted DataFrames while preserving source row order and DataFrame type safety.
/// </summary>
internal static class UnpivotOperations
{
    /// <summary>
    /// Creates a new DataFrame by turning selected value columns into variable and value rows.
    /// </summary>
    internal static DataFrame Create(
        DataFrame source,
        IReadOnlyList<string> idColumns,
        IReadOnlyList<string> valueColumns,
        string variableColumnName,
        string valueColumnName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(idColumns);
        ArgumentNullException.ThrowIfNull(valueColumns);
        ArgumentException.ThrowIfNullOrWhiteSpace(variableColumnName);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueColumnName);

        if (string.Equals(variableColumnName, valueColumnName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Unpivot output variable and value column names must be different.", nameof(valueColumnName));
        }

        var resolvedIdColumns = ResolveColumns(source, idColumns, nameof(idColumns), allowEmpty: true);
        var resolvedValueColumns = ResolveColumns(source, valueColumns, nameof(valueColumns), allowEmpty: false);
        ValidateRoleSeparation(resolvedIdColumns, resolvedValueColumns);
        ValidateOutputColumnName(source, variableColumnName, nameof(variableColumnName));
        ValidateOutputColumnName(source, valueColumnName, nameof(valueColumnName));

        var resultValueType = ResolveValueType(resolvedValueColumns);
        var resultColumns = new List<ISeries>(resolvedIdColumns.Count + 2);
        foreach (var idColumn in resolvedIdColumns)
        {
            resultColumns.Add(DataFrame.CreateSeriesFromValues(
                idColumn.Name,
                idColumn.DataType,
                RepeatIdentifierValues(idColumn, resolvedValueColumns.Count)));
        }

        resultColumns.Add(DataFrame.CreateSeriesFromValues(
            variableColumnName,
            typeof(string),
            CreateVariableValues(resolvedValueColumns, source.RowTotalCore)));
        resultColumns.Add(DataFrame.CreateSeriesFromValues(
            valueColumnName,
            resultValueType,
            CreateValueValues(resolvedValueColumns, resultValueType)));

        return DataFrame.CreateFromSeries(resultColumns);
    }

    /// <summary>
    /// Resolves source columns while preserving caller order and rejecting ambiguous selections.
    /// </summary>
    private static IReadOnlyList<ISeries> ResolveColumns(
        DataFrame source,
        IReadOnlyList<string> columnNames,
        string parameterName,
        bool allowEmpty)
    {
        if (!allowEmpty && columnNames.Count == 0)
        {
            throw new ArgumentException("Unpivot requires at least one value column.", parameterName);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolved = new ISeries[columnNames.Count];
        for (var index = 0; index < columnNames.Count; index++)
        {
            var columnName = columnNames[index];
            ArgumentException.ThrowIfNullOrWhiteSpace(columnName, parameterName);
            if (!seen.Add(columnName))
            {
                throw new ArgumentException($"Column '{columnName}' was supplied more than once.", parameterName);
            }

            resolved[index] = source.GetColumn(columnName);
        }

        return resolved;
    }

    /// <summary>
    /// Rejects columns assigned to both identifier and value roles before any result is built.
    /// </summary>
    private static void ValidateRoleSeparation(IReadOnlyList<ISeries> idColumns, IReadOnlyList<ISeries> valueColumns)
    {
        var idColumnNames = new HashSet<string>(idColumns.Select(static column => column.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var valueColumn in valueColumns)
        {
            if (idColumnNames.Contains(valueColumn.Name))
            {
                throw new ArgumentException($"Column '{valueColumn.Name}' cannot be both an id column and a value column.");
            }
        }
    }

    /// <summary>
    /// Ensures generated output columns cannot hide or replace existing source columns.
    /// </summary>
    private static void ValidateOutputColumnName(DataFrame source, string columnName, string parameterName)
    {
        if (source.HasColumn(columnName))
        {
            throw new ArgumentException($"Unpivot output column name '{columnName}' conflicts with an existing source column.", parameterName);
        }
    }

    /// <summary>
    /// Resolves the common value column type without string fallback for incompatible values.
    /// </summary>
    private static Type ResolveValueType(IReadOnlyList<ISeries> valueColumns)
    {
        var hasNull = false;
        var valueKinds = new HashSet<UnpivotValueKind>();
        foreach (var column in valueColumns)
        {
            var columnType = Nullable.GetUnderlyingType(column.DataType) ?? column.DataType;
            if (column.IsNullable)
            {
                hasNull = true;
            }

            var kind = GetSupportedValueKind(column.Name, columnType);
            valueKinds.Add(kind);
        }

        var resolvedType = ResolveNonNullableValueType(valueColumns, valueKinds);
        return hasNull && resolvedType.IsValueType ? typeof(Nullable<>).MakeGenericType(resolvedType) : resolvedType;
    }

    /// <summary>
    /// Converts supported value kinds to a single non-null result type.
    /// </summary>
    private static Type ResolveNonNullableValueType(IReadOnlyList<ISeries> valueColumns, HashSet<UnpivotValueKind> valueKinds)
    {
        if (valueKinds.All(static kind => kind is UnpivotValueKind.Int or UnpivotValueKind.Long or UnpivotValueKind.Decimal or UnpivotValueKind.Double))
        {
            return ResolveNumericType(valueKinds);
        }

        if (valueKinds.Count == 1)
        {
            var singleKind = valueKinds.Single();
            return singleKind switch
            {
                UnpivotValueKind.String => typeof(string),
                UnpivotValueKind.Boolean => typeof(bool),
                UnpivotValueKind.DateTime => typeof(DateTime),
                UnpivotValueKind.DateTimeOffset => typeof(DateTimeOffset),
                UnpivotValueKind.TimeSpan => typeof(TimeSpan),
                UnpivotValueKind.Guid => typeof(Guid),
                _ => throw new ArgumentException($"Unpivot value columns contain unsupported value kind '{singleKind}'.")
            };
        }

        var columnTypes = string.Join(", ", valueColumns.Select(static column => $"'{column.Name}' ({column.DataType})"));
        throw new ArgumentException($"Unpivot value columns contain incompatible data types: {columnTypes}.");
    }

    /// <summary>
    /// Applies the same conservative numeric widening used by DataFrame JSON inference.
    /// </summary>
    private static Type ResolveNumericType(HashSet<UnpivotValueKind> valueKinds)
    {
        if (valueKinds.Contains(UnpivotValueKind.Double))
        {
            return typeof(double);
        }

        if (valueKinds.Contains(UnpivotValueKind.Decimal))
        {
            return typeof(decimal);
        }

        if (valueKinds.Contains(UnpivotValueKind.Long))
        {
            return typeof(long);
        }

        return typeof(int);
    }

    /// <summary>
    /// Classifies supported value column types and rejects complex object-like values.
    /// </summary>
    private static UnpivotValueKind GetSupportedValueKind(string columnName, Type type)
    {
        if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) ||
            type == typeof(ushort) || type == typeof(int) || type == typeof(uint))
        {
            return UnpivotValueKind.Int;
        }

        if (type == typeof(long) || type == typeof(ulong))
        {
            return UnpivotValueKind.Long;
        }

        if (type == typeof(float) || type == typeof(double))
        {
            return UnpivotValueKind.Double;
        }

        if (type == typeof(decimal))
        {
            return UnpivotValueKind.Decimal;
        }

        if (type == typeof(string))
        {
            return UnpivotValueKind.String;
        }

        if (type == typeof(bool))
        {
            return UnpivotValueKind.Boolean;
        }

        if (type == typeof(DateTime))
        {
            return UnpivotValueKind.DateTime;
        }

        if (type == typeof(DateTimeOffset))
        {
            return UnpivotValueKind.DateTimeOffset;
        }

        if (type == typeof(TimeSpan))
        {
            return UnpivotValueKind.TimeSpan;
        }

        if (type == typeof(Guid))
        {
            return UnpivotValueKind.Guid;
        }

        if (type == typeof(object) || typeof(IEnumerable).IsAssignableFrom(type) || typeof(IDictionary).IsAssignableFrom(type))
        {
            throw new ArgumentException($"Unpivot value column '{columnName}' has unsupported complex data type '{type}'.");
        }

        throw new ArgumentException($"Unpivot value column '{columnName}' has unsupported data type '{type}'.");
    }

    /// <summary>
    /// Repeats each identifier column once for every value column group.
    /// </summary>
    private static IEnumerable<object?> RepeatIdentifierValues(ISeries idColumn, int valueColumnCount)
    {
        for (var valueColumnIndex = 0; valueColumnIndex < valueColumnCount; valueColumnIndex++)
        {
            for (var rowIndex = 0; rowIndex < idColumn.Count; rowIndex++)
            {
                yield return idColumn.GetValue(rowIndex);
            }
        }
    }

    /// <summary>
    /// Emits the original source value column names as variable values without normalization.
    /// </summary>
    private static IEnumerable<object?> CreateVariableValues(IReadOnlyList<ISeries> valueColumns, int rowCount)
    {
        foreach (var valueColumn in valueColumns)
        {
            for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                yield return valueColumn.Name;
            }
        }
    }

    /// <summary>
    /// Converts value cells to the resolved result type while preserving nulls.
    /// </summary>
    private static IEnumerable<object?> CreateValueValues(IReadOnlyList<ISeries> valueColumns, Type resultType)
    {
        var targetType = Nullable.GetUnderlyingType(resultType) ?? resultType;
        foreach (var valueColumn in valueColumns)
        {
            for (var rowIndex = 0; rowIndex < valueColumn.Count; rowIndex++)
            {
                var value = valueColumn.GetValue(rowIndex);
                yield return value is null ? null : ConvertValue(valueColumn, value, targetType);
            }
        }
    }

    /// <summary>
    /// Performs only explicit safe numeric widening and exact same-kind value preservation.
    /// </summary>
    private static object ConvertValue(ISeries column, object value, Type targetType)
    {
        if (targetType == typeof(int))
        {
            return value switch
            {
                byte byteValue => (int)byteValue,
                sbyte sbyteValue => (int)sbyteValue,
                short shortValue => (int)shortValue,
                ushort ushortValue => (int)ushortValue,
                int intValue => intValue,
                uint uintValue when uintValue <= int.MaxValue => (int)uintValue,
                _ => throw CreateIncompatibleValueException(column, value, targetType)
            };
        }

        if (targetType == typeof(long))
        {
            return value switch
            {
                byte byteValue => (long)byteValue,
                sbyte sbyteValue => (long)sbyteValue,
                short shortValue => (long)shortValue,
                ushort ushortValue => (long)ushortValue,
                int intValue => (long)intValue,
                uint uintValue => (long)uintValue,
                long longValue => longValue,
                ulong ulongValue when ulongValue <= long.MaxValue => (long)ulongValue,
                _ => throw CreateIncompatibleValueException(column, value, targetType)
            };
        }

        if (targetType == typeof(decimal))
        {
            return value switch
            {
                byte byteValue => (decimal)byteValue,
                sbyte sbyteValue => (decimal)sbyteValue,
                short shortValue => (decimal)shortValue,
                ushort ushortValue => (decimal)ushortValue,
                int intValue => (decimal)intValue,
                uint uintValue => (decimal)uintValue,
                long longValue => (decimal)longValue,
                ulong ulongValue => (decimal)ulongValue,
                decimal decimalValue => decimalValue,
                _ => throw CreateIncompatibleValueException(column, value, targetType)
            };
        }

        if (targetType == typeof(double))
        {
            return value switch
            {
                byte byteValue => (double)byteValue,
                sbyte sbyteValue => (double)sbyteValue,
                short shortValue => (double)shortValue,
                ushort ushortValue => (double)ushortValue,
                int intValue => (double)intValue,
                uint uintValue => (double)uintValue,
                long longValue => (double)longValue,
                ulong ulongValue => (double)ulongValue,
                float floatValue => (double)floatValue,
                double doubleValue => doubleValue,
                decimal decimalValue => (double)decimalValue,
                _ => throw CreateIncompatibleValueException(column, value, targetType)
            };
        }

        if (value.GetType() == targetType)
        {
            return value;
        }

        throw CreateIncompatibleValueException(column, value, targetType);
    }

    private static ArgumentException CreateIncompatibleValueException(ISeries column, object value, Type targetType)
    {
        return new ArgumentException(
            $"Unpivot value column '{column.Name}' contains value type '{value.GetType()}' that cannot be represented as '{targetType}'.");
    }

    /// <summary>
    /// Represents the supported value families used to resolve a common unpivot value type.
    /// </summary>
    private enum UnpivotValueKind
    {
        /// <summary>
        /// Integral values that can be represented as Int32 when each runtime value is in range.
        /// </summary>
        Int,

        /// <summary>
        /// Integral values that require Int64 when each runtime value is in range.
        /// </summary>
        Long,

        /// <summary>
        /// Decimal values and integral values that can be widened to Decimal.
        /// </summary>
        Decimal,

        /// <summary>
        /// Floating-point values and numeric values widened to Double.
        /// </summary>
        Double,

        /// <summary>
        /// String values without fallback conversion from other kinds.
        /// </summary>
        String,

        /// <summary>
        /// Boolean values without fallback conversion from other kinds.
        /// </summary>
        Boolean,

        /// <summary>
        /// DateTime values without fallback conversion from other date/time kinds.
        /// </summary>
        DateTime,

        /// <summary>
        /// DateTimeOffset values without fallback conversion from other date/time kinds.
        /// </summary>
        DateTimeOffset,

        /// <summary>
        /// TimeSpan values without fallback conversion from other kinds.
        /// </summary>
        TimeSpan,

        /// <summary>
        /// Guid values without fallback conversion from strings.
        /// </summary>
        Guid
    }
}
