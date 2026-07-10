using Runiq.Data.Series;

namespace Runiq.Data;

/// <summary>
/// Represents a reusable snapshot of a DataFrame grouped by one or more key columns.
/// </summary>
/// <remarks>
/// Instances are created by <see cref="DataFrame.GroupBy(string[])"/>. Grouped aggregations
/// return new DataFrame instances, preserve the first-seen group order from the snapshot, can be
/// called repeatedly, and do not mutate the original source DataFrame. The constructor is not
/// public; consumer code obtains instances through <see cref="DataFrame.GroupBy(string[])"/>.
/// </remarks>
public sealed class GroupedDataFrame
{
    private readonly DataFrame source;
    private readonly string[] keyColumnNames;

    internal GroupedDataFrame(DataFrame source, string[] keyColumnNames)
    {
        this.source = source;
        this.keyColumnNames = keyColumnNames;
    }

    /// <summary>
    /// Sums a numeric column independently for each group and returns a new DataFrame.
    /// </summary>
    /// <param name="columnName">The numeric column to aggregate.</param>
    /// <returns>
    /// A new DataFrame containing the group key columns followed by a generated
    /// <c>{ColumnName}_Sum</c> result column. The result column type follows the DataFrame-level
    /// numeric sum contract: small integers produce <see cref="int"/>, while <see cref="uint"/>,
    /// <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/>, and
    /// <see cref="decimal"/> preserve their corresponding result types.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or whitespace, the source snapshot is
    /// empty, a group key or aggregate value is null, the aggregate column is not numeric, or the
    /// generated result column name conflicts with a group key. The source snapshot is not mutated.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when no matching aggregate column exists.</exception>
    /// <exception cref="OverflowException">Thrown when checked integer or decimal addition overflows.</exception>
    public DataFrame Sum(string columnName)
    {
        var aggregateColumn = ResolveAggregateColumn(columnName);
        var resultColumnName = CreateResultColumnName(aggregateColumn.Name, "Sum");
        ValidateResultColumnName(resultColumnName);
        var groups = BuildGroups();
        var resultType = DataFrame.GetSumResultType(aggregateColumn);

        return CreateAggregationResult(
            aggregateColumn,
            resultColumnName,
            resultType,
            groups,
            group => DataFrame.SumColumn(aggregateColumn, group.RowIndexes));
    }

    /// <summary>
    /// Averages a numeric column independently for each group as <see cref="double"/> values and returns a new DataFrame.
    /// </summary>
    /// <param name="columnName">The numeric column to aggregate.</param>
    /// <returns>
    /// A new DataFrame containing the group key columns followed by a generated
    /// <c>{ColumnName}_Average</c> result column whose aggregate values are <see cref="double"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or whitespace, the source snapshot is
    /// empty, a group key or aggregate value is null, the aggregate column is not numeric, or the
    /// generated result column name conflicts with a group key. The source snapshot is not mutated.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when no matching aggregate column exists.</exception>
    /// <exception cref="OverflowException">Thrown when checked integer or decimal addition overflows before division.</exception>
    public DataFrame Average(string columnName)
    {
        var aggregateColumn = ResolveAggregateColumn(columnName);
        var resultColumnName = CreateResultColumnName(aggregateColumn.Name, "Average");
        ValidateResultColumnName(resultColumnName);
        var groups = BuildGroups();
        DataFrame.GetNumericAggregationType(aggregateColumn);

        return CreateAggregationResult(
            aggregateColumn,
            resultColumnName,
            typeof(double),
            groups,
            group => DataFrame.AverageColumn(aggregateColumn, group.RowIndexes));
    }

    /// <summary>
    /// Finds the minimum comparable value independently for each group and returns a new DataFrame.
    /// </summary>
    /// <param name="columnName">The comparable column to aggregate.</param>
    /// <returns>
    /// A new DataFrame containing the group key columns followed by a generated
    /// <c>{ColumnName}_Min</c> result column whose aggregate values preserve the source column type.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or whitespace, the source snapshot is
    /// empty, a group key or aggregate value is null, values cannot be compared, or the generated
    /// result column name conflicts with a group key. The source snapshot is not mutated.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when no matching aggregate column exists.</exception>
    public DataFrame Min(string columnName)
    {
        var aggregateColumn = ResolveAggregateColumn(columnName);
        var resultColumnName = CreateResultColumnName(aggregateColumn.Name, "Min");
        ValidateResultColumnName(resultColumnName);
        var groups = BuildGroups();

        return CreateAggregationResult(
            aggregateColumn,
            resultColumnName,
            aggregateColumn.DataType,
            groups,
            group => DataFrame.MinOrMaxColumn(aggregateColumn, group.RowIndexes, findMaximum: false));
    }

    /// <summary>
    /// Finds the maximum comparable value independently for each group and returns a new DataFrame.
    /// </summary>
    /// <param name="columnName">The comparable column to aggregate.</param>
    /// <returns>
    /// A new DataFrame containing the group key columns followed by a generated
    /// <c>{ColumnName}_Max</c> result column whose aggregate values preserve the source column type.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or whitespace, the source snapshot is
    /// empty, a group key or aggregate value is null, values cannot be compared, or the generated
    /// result column name conflicts with a group key. The source snapshot is not mutated.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when no matching aggregate column exists.</exception>
    public DataFrame Max(string columnName)
    {
        var aggregateColumn = ResolveAggregateColumn(columnName);
        var resultColumnName = CreateResultColumnName(aggregateColumn.Name, "Max");
        ValidateResultColumnName(resultColumnName);
        var groups = BuildGroups();

        return CreateAggregationResult(
            aggregateColumn,
            resultColumnName,
            aggregateColumn.DataType,
            groups,
            group => DataFrame.MinOrMaxColumn(aggregateColumn, group.RowIndexes, findMaximum: true));
    }

    private ISeries ResolveAggregateColumn(string columnName)
    {
        return source.GetColumn(columnName);
    }

    private IReadOnlyList<GroupBucket> BuildGroups()
    {
        if (source.RowTotalCore == 0)
        {
            throw new ArgumentException("The grouped DataFrame snapshot contains no rows to aggregate.");
        }

        var keyColumns = keyColumnNames
            .Select(source.GetColumn)
            .ToArray();
        var bucketsByKey = new Dictionary<GroupKey, GroupBucket>(GroupKeyComparer.Instance);
        var orderedBuckets = new List<GroupBucket>();

        for (var rowIndex = 0; rowIndex < source.RowTotalCore; rowIndex++)
        {
            var values = new object[keyColumns.Length];
            for (var keyIndex = 0; keyIndex < keyColumns.Length; keyIndex++)
            {
                var value = keyColumns[keyIndex].GetValue(rowIndex);
                if (value is null)
                {
                    throw new ArgumentException(
                        $"Group key column '{keyColumns[keyIndex].Name}' contains null values, which are not supported for grouping.");
                }

                values[keyIndex] = value;
            }

            var key = new GroupKey(values);
            if (!bucketsByKey.TryGetValue(key, out var bucket))
            {
                bucket = new GroupBucket(key);
                bucketsByKey.Add(key, bucket);
                orderedBuckets.Add(bucket);
            }

            bucket.RowIndexes.Add(rowIndex);
        }

        return orderedBuckets;
    }

    private DataFrame CreateAggregationResult(
        ISeries aggregateColumn,
        string resultColumnName,
        Type resultColumnType,
        IReadOnlyList<GroupBucket> groups,
        Func<GroupBucket, object?> aggregate)
    {
        var resultColumns = new List<ISeries>(keyColumnNames.Length + 1);
        var keyColumns = keyColumnNames
            .Select(source.GetColumn)
            .ToArray();

        for (var keyIndex = 0; keyIndex < keyColumns.Length; keyIndex++)
        {
            var values = groups.Select(group => group.Key.Values[keyIndex]);
            resultColumns.Add(DataFrame.CreateSeriesFromValues(keyColumns[keyIndex].Name, keyColumns[keyIndex].DataType, values));
        }

        var aggregateValues = groups.Select(aggregate);
        resultColumns.Add(DataFrame.CreateSeriesFromValues(resultColumnName, resultColumnType, aggregateValues));

        return DataFrame.CreateFromSeries(resultColumns);
    }

    private void ValidateResultColumnName(string resultColumnName)
    {
        if (keyColumnNames.Contains(resultColumnName, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Generated result column '{resultColumnName}' conflicts with an existing group key column.");
        }
    }

    private static string CreateResultColumnName(string columnName, string operationName)
    {
        return $"{columnName}_{operationName}";
    }

    private sealed class GroupBucket
    {
        internal GroupBucket(GroupKey key)
        {
            Key = key;
        }

        internal GroupKey Key { get; }

        internal List<int> RowIndexes { get; } = [];
    }

    private sealed class GroupKey
    {
        internal GroupKey(object[] values)
        {
            Values = values;
        }

        internal object[] Values { get; }
    }

    private sealed class GroupKeyComparer : IEqualityComparer<GroupKey>
    {
        internal static readonly GroupKeyComparer Instance = new();

        private GroupKeyComparer()
        {
        }

        public bool Equals(GroupKey? left, GroupKey? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null || left.Values.Length != right.Values.Length)
            {
                return false;
            }

            for (var index = 0; index < left.Values.Length; index++)
            {
                if (!EqualityComparer<object>.Default.Equals(left.Values[index], right.Values[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(GroupKey key)
        {
            var hash = new HashCode();
            foreach (var value in key.Values)
            {
                hash.Add(value);
            }

            return hash.ToHashCode();
        }
    }
}
