using Runiq.Data.Series;

namespace Runiq.Data;

/// <summary>
/// Represents the condition stage of a DataFrame relational join.
/// </summary>
/// <remarks>
/// Instances are created by <see cref="DataFrame.InnerJoin(DataFrame)"/>,
/// <see cref="DataFrame.LeftJoin(DataFrame)"/>, <see cref="DataFrame.RightJoin(DataFrame)"/>,
/// and <see cref="DataFrame.FullJoin(DataFrame)"/>. The builder only stores the selected join
/// type and right DataFrame until an <c>On</c> overload supplies the key definition. Calling
/// <c>On</c> executes the join immediately, returns a new <see cref="DataFrame"/>, and never
/// mutates either source DataFrame.
/// </remarks>
public sealed class DataFrameJoin
{
    private readonly DataFrame left;
    private readonly DataFrame right;
    private readonly JoinKind kind;

    internal DataFrameJoin(DataFrame left, DataFrame right, JoinKind kind)
    {
        this.left = left;
        this.right = right;
        this.kind = kind;
    }

    /// <summary>
    /// Executes the join by matching one column that has the same name on both DataFrames.
    /// </summary>
    /// <param name="columnName">The column name to use on both the left and right DataFrames.</param>
    /// <returns>
    /// A new DataFrame containing the joined rows. Source DataFrames are not mutated, and the
    /// result is a snapshot of values at execution time.
    /// </returns>
    /// <remarks>
    /// When the same-name key column appears on both sides, it is emitted once in the result at
    /// the left column position. For right-only unmatched rows in right or full joins, that
    /// shared key value is filled from the right row.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the key name is empty or whitespace, when the key is missing on either side,
    /// or when non-key column names conflict.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="columnName"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when a requested key column does not exist.</exception>
    public DataFrame On(string columnName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
        return DataFrameJoinExecutor.Execute(left, right, kind, [(columnName, columnName)]);
    }

    /// <summary>
    /// Executes the join by matching one left column to one right column.
    /// </summary>
    /// <param name="leftColumn">The join key column name in the left DataFrame.</param>
    /// <param name="rightColumn">The join key column name in the right DataFrame.</param>
    /// <returns>
    /// A new DataFrame containing the joined rows. Source DataFrames are not mutated, and the
    /// result is a snapshot of values at execution time.
    /// </returns>
    /// <remarks>
    /// Different key names keep both key columns in result column order. If both key names refer
    /// to the same column name, the shared key is emitted once according to same-name key rules.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when a key name is empty or whitespace, when a key is missing on either side, or
    /// when non-key column names conflict.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when either key name is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when a requested key column does not exist.</exception>
    public DataFrame On(string leftColumn, string rightColumn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leftColumn);
        ArgumentException.ThrowIfNullOrWhiteSpace(rightColumn);
        return DataFrameJoinExecutor.Execute(left, right, kind, [(leftColumn, rightColumn)]);
    }

    /// <summary>
    /// Executes the join by matching composite keys with the same column names on both DataFrames.
    /// </summary>
    /// <param name="columnNames">
    /// The ordered key column names to use on both the left and right DataFrames.
    /// </param>
    /// <returns>
    /// A new DataFrame containing the joined rows. Source DataFrames are not mutated, and the
    /// result is a snapshot of values at execution time.
    /// </returns>
    /// <remarks>
    /// All key parts must match for rows to join. If any key part is null, that row is treated
    /// as unmatched. Same-name key columns are emitted once in the result.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when no key columns are supplied, a key name is empty or whitespace, a key is
    /// missing on either side, or non-key column names conflict.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="columnNames"/> or an entry is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when a requested key column does not exist.</exception>
    public DataFrame On(params string[] columnNames)
    {
        ArgumentNullException.ThrowIfNull(columnNames);
        if (columnNames.Length == 0)
        {
            throw new ArgumentException("At least one join key column must be supplied.", nameof(columnNames));
        }

        var keys = new (string LeftColumn, string RightColumn)[columnNames.Length];
        for (var index = 0; index < columnNames.Length; index++)
        {
            var columnName = columnNames[index];
            ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
            keys[index] = (columnName, columnName);
        }

        return DataFrameJoinExecutor.Execute(left, right, kind, keys);
    }

    /// <summary>
    /// Executes the join by matching an ordered composite key whose column names can differ by side.
    /// </summary>
    /// <param name="column">The first key pair where the tuple is <c>(LeftColumn, RightColumn)</c>.</param>
    /// <param name="additionalColumns">Additional ordered key pairs where each tuple is <c>(LeftColumn, RightColumn)</c>.</param>
    /// <returns>
    /// A new DataFrame containing the joined rows. Source DataFrames are not mutated, and the
    /// result is a snapshot of values at execution time.
    /// </returns>
    /// <remarks>
    /// All key parts must match for rows to join. If any key part is null, that row is treated
    /// as unmatched. Same-name key columns are emitted once in the result; differently named key
    /// columns are both kept.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when a key name is empty or whitespace, a key is missing on either side, or
    /// non-key column names conflict.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="additionalColumns"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when a requested key column does not exist.</exception>
    public DataFrame On(
        (string LeftColumn, string RightColumn) column,
        params (string LeftColumn, string RightColumn)[] additionalColumns)
    {
        ArgumentNullException.ThrowIfNull(additionalColumns);

        var columns = new (string LeftColumn, string RightColumn)[additionalColumns.Length + 1];
        columns[0] = column;
        for (var index = 0; index < additionalColumns.Length; index++)
        {
            columns[index + 1] = additionalColumns[index];
        }

        for (var index = 0; index < columns.Length; index++)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(columns[index].LeftColumn);
            ArgumentException.ThrowIfNullOrWhiteSpace(columns[index].RightColumn);
        }

        return DataFrameJoinExecutor.Execute(left, right, kind, columns);
    }
}

internal static class DataFrameJoinExecutor
{
    internal static DataFrame Execute(
        DataFrame left,
        DataFrame right,
        JoinKind kind,
        IReadOnlyList<(string LeftColumn, string RightColumn)> keyPairs)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ArgumentNullException.ThrowIfNull(keyPairs);

        if (keyPairs.Count == 0)
        {
            throw new ArgumentException("At least one join key must be supplied.", nameof(keyPairs));
        }

        var plan = JoinPlan.Create(left, right, keyPairs);
        var rightLookup = BuildLookup(plan.RightKeyColumns, right.Rows.Count());
        var matchedRightRows = new bool[right.Rows.Count()];
        var outputRows = new List<OutputRow>();

        if (kind is JoinKind.Inner or JoinKind.Left or JoinKind.Full)
        {
            for (var leftRow = 0; leftRow < left.Rows.Count(); leftRow++)
            {
                if (TryCreateKey(plan.LeftKeyColumns, leftRow, out var key) && rightLookup.TryGetValue(key, out var rightRows))
                {
                    foreach (var rightRow in rightRows)
                    {
                        outputRows.Add(new OutputRow(leftRow, rightRow));
                        matchedRightRows[rightRow] = true;
                    }
                }
                else if (kind is JoinKind.Left or JoinKind.Full)
                {
                    outputRows.Add(new OutputRow(leftRow, null));
                }
            }
        }
        else
        {
            var leftLookup = BuildLookup(plan.LeftKeyColumns, left.Rows.Count());
            for (var rightRow = 0; rightRow < right.Rows.Count(); rightRow++)
            {
                if (TryCreateKey(plan.RightKeyColumns, rightRow, out var key) && leftLookup.TryGetValue(key, out var leftRows))
                {
                    foreach (var leftRow in leftRows)
                    {
                        outputRows.Add(new OutputRow(leftRow, rightRow));
                    }
                }
                else
                {
                    outputRows.Add(new OutputRow(null, rightRow));
                }
            }
        }

        if (kind == JoinKind.Full)
        {
            for (var rightRow = 0; rightRow < right.Rows.Count(); rightRow++)
            {
                if (!matchedRightRows[rightRow])
                {
                    outputRows.Add(new OutputRow(null, rightRow));
                }
            }
        }

        var resultColumns = plan.ResultColumns
            .Select(column => CreateResultSeries(column, kind, outputRows))
            .ToArray();

        return DataFrame.CreateFromSeries(resultColumns);
    }

    private static Dictionary<JoinKey, List<int>> BuildLookup(IReadOnlyList<ISeries> keyColumns, int rowCount)
    {
        var lookup = new Dictionary<JoinKey, List<int>>();
        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            if (!TryCreateKey(keyColumns, rowIndex, out var key))
            {
                continue;
            }

            if (!lookup.TryGetValue(key, out var rows))
            {
                rows = [];
                lookup.Add(key, rows);
            }

            rows.Add(rowIndex);
        }

        return lookup;
    }

    private static bool TryCreateKey(IReadOnlyList<ISeries> keyColumns, int rowIndex, out JoinKey key)
    {
        var values = new object[keyColumns.Count];
        for (var index = 0; index < keyColumns.Count; index++)
        {
            var value = keyColumns[index].GetValue(rowIndex);
            if (value is null)
            {
                key = default;
                return false;
            }

            ValidateJoinKeyValue(keyColumns[index], value);
            values[index] = value;
        }

        key = new JoinKey(values);
        return true;
    }

    private static void ValidateJoinKeyValue(ISeries column, object value)
    {
        var valueType = value.GetType();
        var declaredType = Nullable.GetUnderlyingType(column.DataType) ?? column.DataType;
        if (declaredType != typeof(object) && !declaredType.IsInstanceOfType(value))
        {
            throw new ArgumentException(
                $"Join key column '{column.Name}' contains value type '{valueType}' that does not match declared data type '{column.DataType}'.");
        }

        if (IsSupportedKeyType(valueType))
        {
            return;
        }

        throw new ArgumentException(
            $"Join key column '{column.Name}' contains value type '{valueType}' that cannot be compared safely for joins.");
    }

    private static bool IsSupportedKeyType(Type valueType)
    {
        valueType = Nullable.GetUnderlyingType(valueType) ?? valueType;
        if (valueType.IsEnum || valueType == typeof(string) || valueType == typeof(bool) ||
            valueType == typeof(char) || valueType == typeof(byte) || valueType == typeof(sbyte) ||
            valueType == typeof(short) || valueType == typeof(ushort) || valueType == typeof(int) ||
            valueType == typeof(uint) || valueType == typeof(long) || valueType == typeof(ulong) ||
            valueType == typeof(float) || valueType == typeof(double) || valueType == typeof(decimal) ||
            valueType == typeof(DateTime) || valueType == typeof(DateTimeOffset) ||
            valueType == typeof(DateOnly) || valueType == typeof(TimeOnly) || valueType == typeof(Guid))
        {
            return true;
        }

        return valueType
            .GetInterfaces()
            .Any(interfaceType => interfaceType.IsGenericType &&
                interfaceType.GetGenericTypeDefinition() == typeof(IEquatable<>) &&
                interfaceType.GetGenericArguments()[0] == valueType);
    }

    private static ISeries CreateResultSeries(ResultColumn column, JoinKind kind, IReadOnlyList<OutputRow> rows)
    {
        var dataType = GetJoinResultType(column, kind);
        var values = new object?[rows.Count];

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var outputRow = rows[rowIndex];
            if (column.Side == JoinSide.Left)
            {
                if (outputRow.LeftRow is int leftRow)
                {
                    values[rowIndex] = column.Source.GetValue(leftRow);
                }
                else if (column.SharedRightKey is not null && outputRow.RightRow is int rightRow)
                {
                    values[rowIndex] = column.SharedRightKey.GetValue(rightRow);
                }
            }
            else if (outputRow.RightRow is int rightRow)
            {
                values[rowIndex] = column.Source.GetValue(rightRow);
            }
        }

        return DataFrame.CreateSeriesFromValues(column.Name, dataType, values);
    }

    private static Type GetJoinResultType(ResultColumn column, JoinKind kind)
    {
        var sourceType = column.Source.DataType;
        if (kind == JoinKind.Inner)
        {
            return sourceType;
        }

        if (column.SharedRightKey is not null)
        {
            if (kind == JoinKind.Right)
            {
                return column.SharedRightKey.DataType;
            }

            if (kind == JoinKind.Full)
            {
                return ToNullableValueType(sourceType);
            }

            return sourceType;
        }

        if ((kind == JoinKind.Left && column.Side == JoinSide.Right) ||
            (kind == JoinKind.Right && column.Side == JoinSide.Left) ||
            kind == JoinKind.Full)
        {
            return ToNullableValueType(sourceType);
        }

        return sourceType;
    }

    private static Type ToNullableValueType(Type sourceType)
    {
        if (!sourceType.IsValueType || Nullable.GetUnderlyingType(sourceType) is not null)
        {
            return sourceType;
        }

        return typeof(Nullable<>).MakeGenericType(sourceType);
    }

    private readonly record struct OutputRow(int? LeftRow, int? RightRow);

    private readonly record struct JoinKey(object[] Values)
    {
        public bool Equals(JoinKey other)
        {
            if (Values.Length != other.Values.Length)
            {
                return false;
            }

            for (var index = 0; index < Values.Length; index++)
            {
                if (!EqualityComparer<object>.Default.Equals(Values[index], other.Values[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var value in Values)
            {
                hash.Add(value);
            }

            return hash.ToHashCode();
        }
    }

    private sealed class JoinPlan
    {
        private JoinPlan(IReadOnlyList<ISeries> leftKeyColumns, IReadOnlyList<ISeries> rightKeyColumns, IReadOnlyList<ResultColumn> resultColumns)
        {
            LeftKeyColumns = leftKeyColumns;
            RightKeyColumns = rightKeyColumns;
            ResultColumns = resultColumns;
        }

        internal IReadOnlyList<ISeries> LeftKeyColumns { get; }

        internal IReadOnlyList<ISeries> RightKeyColumns { get; }

        internal IReadOnlyList<ResultColumn> ResultColumns { get; }

        internal static JoinPlan Create(DataFrame left, DataFrame right, IReadOnlyList<(string LeftColumn, string RightColumn)> keyPairs)
        {
            var leftKeyColumns = new ISeries[keyPairs.Count];
            var rightKeyColumns = new ISeries[keyPairs.Count];
            var sharedKeyRightColumns = new Dictionary<string, ISeries>(StringComparer.OrdinalIgnoreCase);
            var leftKeyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rightSharedKeyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < keyPairs.Count; index++)
            {
                var (leftColumnName, rightColumnName) = keyPairs[index];
                ArgumentException.ThrowIfNullOrWhiteSpace(leftColumnName);
                ArgumentException.ThrowIfNullOrWhiteSpace(rightColumnName);

                var leftColumn = left.GetColumn(leftColumnName);
                var rightColumn = right.GetColumn(rightColumnName);
                leftKeyColumns[index] = leftColumn;
                rightKeyColumns[index] = rightColumn;

                if (!leftKeyNames.Add(leftColumn.Name))
                {
                    throw new ArgumentException($"Left join key column '{leftColumn.Name}' was supplied more than once.", nameof(keyPairs));
                }

                if (StringComparer.OrdinalIgnoreCase.Equals(leftColumn.Name, rightColumn.Name))
                {
                    if (!rightSharedKeyNames.Add(rightColumn.Name))
                    {
                        throw new ArgumentException($"Right join key column '{rightColumn.Name}' was supplied more than once.", nameof(keyPairs));
                    }

                    sharedKeyRightColumns[leftColumn.Name] = rightColumn;
                }
            }

            var resultColumns = new List<ResultColumn>();
            foreach (var column in left.ColumnSeries)
            {
                sharedKeyRightColumns.TryGetValue(column.Name, out var sharedRightKey);
                resultColumns.Add(new ResultColumn(column.Name, column, JoinSide.Left, sharedRightKey));
            }

            foreach (var rightColumn in right.ColumnSeries)
            {
                if (sharedKeyRightColumns.ContainsKey(rightColumn.Name))
                {
                    continue;
                }

                var conflictsWithLeft = left.ColumnSeries.Any(leftColumn => StringComparer.OrdinalIgnoreCase.Equals(leftColumn.Name, rightColumn.Name));
                if (conflictsWithLeft)
                {
                    throw new ArgumentException($"Column '{rightColumn.Name}' exists in both joined DataFrames and is not a shared join key.");
                }

                resultColumns.Add(new ResultColumn(rightColumn.Name, rightColumn, JoinSide.Right, null));
            }

            return new JoinPlan(leftKeyColumns, rightKeyColumns, resultColumns);
        }
    }

    private enum JoinSide
    {
        Left,
        Right
    }

    private sealed record ResultColumn(string Name, ISeries Source, JoinSide Side, ISeries? SharedRightKey);
}
