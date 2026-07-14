using Runiq.Data.Series;

namespace Runiq.Data.Windowing;

/// <summary>
/// Builds a non-mutating window definition for a DataFrame.
/// </summary>
/// <remarks>
/// Instances are created by <see cref="Runiq.Data.DataFrame.Window"/>. The builder records
/// partition and ordering columns only; terminal methods compute aligned series and never mutate
/// the source DataFrame.
/// </remarks>
public sealed class WindowBuilder
{
    private readonly Runiq.Data.DataFrame source;
    private readonly string[] partitionColumnNames;
    private readonly OrderingDescriptor[] orderingDescriptors;

    internal WindowBuilder(Runiq.Data.DataFrame source)
        : this(source, [], [])
    {
    }

    private WindowBuilder(
        Runiq.Data.DataFrame source,
        string[] partitionColumnNames,
        OrderingDescriptor[] orderingDescriptors)
    {
        this.source = source;
        this.partitionColumnNames = partitionColumnNames;
        this.orderingDescriptors = orderingDescriptors;
    }

    /// <summary>
    /// Adds partition columns to the window definition.
    /// </summary>
    /// <param name="columnNames">The existing columns that define independent row-number scopes.</param>
    /// <returns>A new builder containing the supplied partition definition.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when no partition column is supplied, a column name is empty or whitespace, a column
    /// name is repeated, or a named column does not exist.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnNames"/> or one of its values is <see langword="null"/>.
    /// </exception>
    public WindowBuilder PartitionBy(params string[] columnNames)
    {
        ArgumentNullException.ThrowIfNull(columnNames);
        if (columnNames.Length == 0)
        {
            throw new ArgumentException("At least one partition column must be supplied.", nameof(columnNames));
        }

        var resolvedNames = ResolveDistinctColumnNames(columnNames, nameof(columnNames));
        return new WindowBuilder(source, resolvedNames, orderingDescriptors);
    }

    /// <summary>
    /// Adds the primary ascending ordering column to the window definition.
    /// </summary>
    /// <param name="columnName">The existing comparable column that determines row-number order.</param>
    /// <returns>A builder that can accept additional ordering columns or compute row numbers.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or whitespace, duplicated in the
    /// ordering definition, or cannot be compared using the DataFrame sorting contract.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when the named column does not exist.</exception>
    public OrderedWindowBuilder OrderBy(string columnName)
    {
        return AddPrimaryOrdering(columnName, descending: false);
    }

    /// <summary>
    /// Adds the primary descending ordering column to the window definition.
    /// </summary>
    /// <param name="columnName">The existing comparable column that determines row-number order.</param>
    /// <returns>A builder that can accept additional ordering columns or compute row numbers.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or whitespace, duplicated in the
    /// ordering definition, or cannot be compared using the DataFrame sorting contract.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when the named column does not exist.</exception>
    public OrderedWindowBuilder OrderByDescending(string columnName)
    {
        return AddPrimaryOrdering(columnName, descending: true);
    }

    internal OrderedWindowBuilder AddOrdering(string columnName, bool descending)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

        var column = source.GetColumn(columnName);
        Runiq.Data.DataFrame.ValidateSortableColumnCore(column);
        if (orderingDescriptors.Any(descriptor => StringComparer.OrdinalIgnoreCase.Equals(descriptor.ColumnName, column.Name)))
        {
            throw new ArgumentException($"Ordering column '{columnName}' was selected more than once.", nameof(columnName));
        }

        var updatedDescriptors = orderingDescriptors
            .Append(new OrderingDescriptor(column.Name, descending))
            .ToArray();

        return new OrderedWindowBuilder(source, partitionColumnNames, updatedDescriptors);
    }

    private OrderedWindowBuilder AddPrimaryOrdering(string columnName, bool descending)
    {
        if (orderingDescriptors.Length != 0)
        {
            throw new ArgumentException("Primary ordering has already been configured.", nameof(columnName));
        }

        return AddOrdering(columnName, descending);
    }

    private string[] ResolveDistinctColumnNames(string[] columnNames, string parameterName)
    {
        var selectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolvedNames = new string[columnNames.Length];

        for (var index = 0; index < columnNames.Length; index++)
        {
            var columnName = columnNames[index];
            ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
            var column = source.GetColumn(columnName);

            if (!selectedNames.Add(column.Name))
            {
                throw new ArgumentException($"Column '{columnName}' was selected more than once.", parameterName);
            }

            resolvedNames[index] = column.Name;
        }

        return resolvedNames;
    }
}

/// <summary>
/// Builds terminal calculations for a window definition that has at least one ordering column.
/// </summary>
/// <remarks>
/// This type is returned only after a primary ordering has been configured, which keeps
/// <see cref="RowNumber"/> unavailable for unordered window definitions in normal fluent usage.
/// </remarks>
public sealed class OrderedWindowBuilder
{
    private readonly Runiq.Data.DataFrame source;
    private readonly string[] partitionColumnNames;
    private readonly OrderingDescriptor[] orderingDescriptors;

    internal OrderedWindowBuilder(
        Runiq.Data.DataFrame source,
        string[] partitionColumnNames,
        OrderingDescriptor[] orderingDescriptors)
    {
        this.source = source;
        this.partitionColumnNames = partitionColumnNames;
        this.orderingDescriptors = orderingDescriptors;
    }

    /// <summary>
    /// Adds an ascending ordering column after the primary ordering column.
    /// </summary>
    /// <param name="columnName">The existing comparable column to append to the ordering definition.</param>
    /// <returns>A new ordered builder containing the appended ordering column.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or whitespace, duplicated in the
    /// ordering definition, or cannot be compared using the DataFrame sorting contract.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when the named column does not exist.</exception>
    public OrderedWindowBuilder ThenBy(string columnName)
    {
        return AddOrdering(columnName, descending: false);
    }

    /// <summary>
    /// Adds a descending ordering column after the primary ordering column.
    /// </summary>
    /// <param name="columnName">The existing comparable column to append to the ordering definition.</param>
    /// <returns>A new ordered builder containing the appended ordering column.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or whitespace, duplicated in the
    /// ordering definition, or cannot be compared using the DataFrame sorting contract.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when the named column does not exist.</exception>
    public OrderedWindowBuilder ThenByDescending(string columnName)
    {
        return AddOrdering(columnName, descending: true);
    }

    /// <summary>
    /// Computes one-based row numbers within each partition and returns them aligned to source rows.
    /// </summary>
    /// <returns>
    /// A strongly typed integer series that can be added to the source DataFrame through
    /// <see cref="Runiq.Data.DataFrame.Columns"/>.
    /// </returns>
    /// <remarks>
    /// Partitions are scoped by the configured partition columns, or by the entire DataFrame
    /// when no partition is configured. Sorting is stable: rows with equal ordering values keep
    /// their original source-row order as the final tie-breaker.
    /// </remarks>
    public Series<int> RowNumber()
    {
        var partitionColumns = ResolvePartitionColumns();
        var orderingColumns = ResolveOrderingColumns();
        var sortedRowsByPartition = BuildSortedRowsByPartition(partitionColumns, orderingColumns);
        var rowNumbers = new int[source.Rows.Count()];

        foreach (var partitionRows in sortedRowsByPartition)
        {
            for (var index = 0; index < partitionRows.Count; index++)
            {
                rowNumbers[partitionRows[index]] = index + 1;
            }
        }

        return Series<int>.Create("RowNumber", rowNumbers);
    }

    /// <summary>
    /// Computes SQL-style rank values within each partition and returns them aligned to source rows.
    /// </summary>
    /// <returns>
    /// A strongly typed integer series that can be added to the source DataFrame through
    /// <see cref="Runiq.Data.DataFrame.Columns"/>.
    /// </returns>
    /// <remarks>
    /// Equal values across all configured ordering columns receive the same rank. Later values
    /// leave gaps after ties, matching SQL <c>RANK()</c> semantics.
    /// </remarks>
    public Series<int> Rank()
    {
        return BuildRankSeries("Rank", dense: false);
    }

    /// <summary>
    /// Computes SQL-style dense rank values within each partition and returns them aligned to source rows.
    /// </summary>
    /// <returns>
    /// A strongly typed integer series that can be added to the source DataFrame through
    /// <see cref="Runiq.Data.DataFrame.Columns"/>.
    /// </returns>
    /// <remarks>
    /// Equal values across all configured ordering columns receive the same rank. Later values
    /// do not leave gaps after ties, matching SQL <c>DENSE_RANK()</c> semantics.
    /// </remarks>
    public Series<int> DenseRank()
    {
        return BuildRankSeries("DenseRank", dense: true);
    }

    /// <summary>
    /// Returns values from an earlier row in the same ordered partition.
    /// </summary>
    /// <param name="column">The existing source column whose values will be shifted.</param>
    /// <param name="offset">The positive number of ordered rows to look behind the current row.</param>
    /// <returns>
    /// A series aligned to the source DataFrame rows. Rows without an earlier row at the requested
    /// offset contain <see langword="null"/>.
    /// </returns>
    /// <remarks>
    /// The result preserves the source column type for reference and nullable value types. For
    /// non-nullable value types, the result uses the corresponding nullable value type so partition
    /// boundary rows can carry nulls.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="column"/> is empty or whitespace, or when
    /// <paramref name="offset"/> is zero or negative.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="column"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when the named column does not exist.</exception>
    public ISeries Lag(string column, int offset = 1)
    {
        return BuildShiftSeries(column, offset, lookAhead: false);
    }

    /// <summary>
    /// Returns values from a later row in the same ordered partition.
    /// </summary>
    /// <param name="column">The existing source column whose values will be shifted.</param>
    /// <param name="offset">The positive number of ordered rows to look ahead from the current row.</param>
    /// <returns>
    /// A series aligned to the source DataFrame rows. Rows without a later row at the requested
    /// offset contain <see langword="null"/>.
    /// </returns>
    /// <remarks>
    /// The result preserves the source column type for reference and nullable value types. For
    /// non-nullable value types, the result uses the corresponding nullable value type so partition
    /// boundary rows can carry nulls.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="column"/> is empty or whitespace, or when
    /// <paramref name="offset"/> is zero or negative.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="column"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when the named column does not exist.</exception>
    public ISeries Lead(string column, int offset = 1)
    {
        return BuildShiftSeries(column, offset, lookAhead: true);
    }

    /// <summary>
    /// Returns the first value from the configured ordered partition for every row in that partition.
    /// </summary>
    /// <param name="column">The existing source column whose ordered first value will be returned.</param>
    /// <returns>
    /// A series aligned to the source DataFrame rows and using the same CLR value type as the
    /// source column.
    /// </returns>
    /// <remarks>
    /// Null target values are not skipped. The value from the first physical row after the
    /// configured ordering is copied to every source row in the partition.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="column"/> is empty or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="column"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when the named column does not exist.</exception>
    public ISeries FirstValue(string column)
    {
        return BuildBoundaryValueSeries(column, useLastValue: false);
    }

    /// <summary>
    /// Returns the last value from the configured ordered partition for every row in that partition.
    /// </summary>
    /// <param name="column">The existing source column whose ordered last value will be returned.</param>
    /// <returns>
    /// A series aligned to the source DataFrame rows and using the same CLR value type as the
    /// source column.
    /// </returns>
    /// <remarks>
    /// The calculation always uses the full partition after ordering, equivalent to an unbounded
    /// SQL window frame. Null target values are not skipped.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="column"/> is empty or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="column"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when the named column does not exist.</exception>
    public ISeries LastValue(string column)
    {
        return BuildBoundaryValueSeries(column, useLastValue: true);
    }

    private OrderedWindowBuilder AddOrdering(string columnName, bool descending)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

        var column = source.GetColumn(columnName);
        Runiq.Data.DataFrame.ValidateSortableColumnCore(column);
        if (orderingDescriptors.Any(descriptor => StringComparer.OrdinalIgnoreCase.Equals(descriptor.ColumnName, column.Name)))
        {
            throw new ArgumentException($"Ordering column '{columnName}' was selected more than once.", nameof(columnName));
        }

        var updatedDescriptors = orderingDescriptors
            .Append(new OrderingDescriptor(column.Name, descending))
            .ToArray();

        return new OrderedWindowBuilder(source, partitionColumnNames, updatedDescriptors);
    }

    private IReadOnlyList<List<int>> BuildSortedRowsByPartition(
        IReadOnlyList<ISeries> partitionColumns,
        IReadOnlyList<ResolvedOrderingDescriptor> orderingColumns)
    {
        var rowsByPartition = new Dictionary<WindowPartitionKey, List<int>>(WindowPartitionKeyComparer.Instance);
        var orderedPartitions = new List<List<int>>();

        for (var rowIndex = 0; rowIndex < source.Rows.Count(); rowIndex++)
        {
            var partitionKey = CreatePartitionKey(partitionColumns, rowIndex);
            if (!rowsByPartition.TryGetValue(partitionKey, out var rows))
            {
                rows = [];
                rowsByPartition.Add(partitionKey, rows);
                orderedPartitions.Add(rows);
            }

            rows.Add(rowIndex);
        }

        foreach (var rows in orderedPartitions)
        {
            // Array.Sort is not guaranteed to be stable, so source row index remains the final
            // comparison key to preserve existing order when every configured ordering value ties.
            rows.Sort((leftIndex, rightIndex) => CompareRows(orderingColumns, leftIndex, rightIndex));
        }

        return orderedPartitions;
    }

    /// <summary>
    /// Builds rank values from the same partition ordering used by RowNumber while preserving
    /// source-row alignment in the returned series.
    /// </summary>
    private Series<int> BuildRankSeries(string seriesName, bool dense)
    {
        var partitionColumns = ResolvePartitionColumns();
        var orderingColumns = ResolveOrderingColumns();
        var sortedRowsByPartition = BuildSortedRowsByPartition(partitionColumns, orderingColumns);
        var ranks = new int[source.Rows.Count()];

        foreach (var partitionRows in sortedRowsByPartition)
        {
            var currentRank = 0;
            var denseRank = 0;
            var previousRowIndex = -1;

            for (var index = 0; index < partitionRows.Count; index++)
            {
                var rowIndex = partitionRows[index];
                if (index == 0 || CompareOrderingKeys(orderingColumns, previousRowIndex, rowIndex) != 0)
                {
                    currentRank = index + 1;
                    denseRank++;
                }

                ranks[rowIndex] = dense ? denseRank : currentRank;
                previousRowIndex = rowIndex;
            }
        }

        return Series<int>.Create(seriesName, ranks);
    }

    /// <summary>
    /// Builds Lag or Lead values from the existing ordered partition rows without involving the
    /// shifted target column in ordering decisions.
    /// </summary>
    private ISeries BuildShiftSeries(string columnName, int offset, bool lookAhead)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
        if (offset <= 0)
        {
            throw new ArgumentException("Window shift offset must be greater than zero.", nameof(offset));
        }

        var targetColumn = source.GetColumn(columnName);
        var partitionColumns = ResolvePartitionColumns();
        var orderingColumns = ResolveOrderingColumns();
        var sortedRowsByPartition = BuildSortedRowsByPartition(partitionColumns, orderingColumns);
        var resultValues = new object?[source.Rows.Count()];

        foreach (var partitionRows in sortedRowsByPartition)
        {
            for (var index = 0; index < partitionRows.Count; index++)
            {
                var sourceValueIndex = lookAhead ? index + offset : index - offset;
                if (sourceValueIndex >= 0 && sourceValueIndex < partitionRows.Count)
                {
                    resultValues[partitionRows[index]] = targetColumn.GetValue(partitionRows[sourceValueIndex]);
                }
            }
        }

        return Runiq.Data.DataFrame.CreateSeriesFromValues(
            targetColumn.Name,
            GetShiftResultType(targetColumn.DataType),
            resultValues);
    }

    /// <summary>
    /// Builds FirstValue or LastValue results from ordered partition boundary rows while preserving
    /// source-row alignment and the source column type.
    /// </summary>
    private ISeries BuildBoundaryValueSeries(string columnName, bool useLastValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

        var targetColumn = source.GetColumn(columnName);
        var partitionColumns = ResolvePartitionColumns();
        var orderingColumns = ResolveOrderingColumns();
        var sortedRowsByPartition = BuildSortedRowsByPartition(partitionColumns, orderingColumns);
        var resultValues = new object?[source.Rows.Count()];

        foreach (var partitionRows in sortedRowsByPartition)
        {
            var sourceValueIndex = useLastValue ? partitionRows.Count - 1 : 0;
            var boundaryValue = targetColumn.GetValue(partitionRows[sourceValueIndex]);

            foreach (var rowIndex in partitionRows)
            {
                resultValues[rowIndex] = boundaryValue;
            }
        }

        return Runiq.Data.DataFrame.CreateSeriesFromValues(targetColumn.Name, targetColumn.DataType, resultValues);
    }

    private ISeries[] ResolvePartitionColumns()
    {
        return partitionColumnNames
            .Select(source.GetColumn)
            .ToArray();
    }

    private ResolvedOrderingDescriptor[] ResolveOrderingColumns()
    {
        return orderingDescriptors
            .Select(descriptor => new ResolvedOrderingDescriptor(source.GetColumn(descriptor.ColumnName), descriptor.Descending))
            .ToArray();
    }

    /// <summary>
    /// Selects a result type that preserves target values while allowing boundary nulls.
    /// </summary>
    private static Type GetShiftResultType(Type sourceType)
    {
        if (!sourceType.IsValueType || Nullable.GetUnderlyingType(sourceType) is not null)
        {
            return sourceType;
        }

        return typeof(Nullable<>).MakeGenericType(sourceType);
    }

    private static WindowPartitionKey CreatePartitionKey(IReadOnlyList<ISeries> partitionColumns, int rowIndex)
    {
        var values = new object?[partitionColumns.Count];
        for (var index = 0; index < partitionColumns.Count; index++)
        {
            values[index] = partitionColumns[index].GetValue(rowIndex);
        }

        return new WindowPartitionKey(values);
    }

    private static int CompareRows(
        IReadOnlyList<ResolvedOrderingDescriptor> orderingColumns,
        int leftIndex,
        int rightIndex)
    {
        var comparison = CompareOrderingKeys(orderingColumns, leftIndex, rightIndex);
        if (comparison != 0)
        {
            return comparison;
        }

        return leftIndex.CompareTo(rightIndex);
    }

    /// <summary>
    /// Compares only configured ordering values so rank ties are not affected by the stable
    /// source-row tie-breaker used during sorting.
    /// </summary>
    private static int CompareOrderingKeys(
        IReadOnlyList<ResolvedOrderingDescriptor> orderingColumns,
        int leftIndex,
        int rightIndex)
    {
        foreach (var descriptor in orderingColumns)
        {
            var comparison = Runiq.Data.DataFrame.CompareSortValuesCore(descriptor.Column, leftIndex, rightIndex);
            if (comparison != 0)
            {
                return descriptor.Descending ? -comparison : comparison;
            }
        }

        return 0;
    }
}

/// <summary>
/// Stores an ordering clause as a definition only, without capturing source column values.
/// </summary>
internal readonly record struct OrderingDescriptor(string ColumnName, bool Descending);

/// <summary>
/// Stores source columns resolved at execution time for row comparison.
/// </summary>
internal readonly record struct ResolvedOrderingDescriptor(ISeries Column, bool Descending);

/// <summary>
/// Represents the partition values for one source row, including null values.
/// </summary>
internal sealed class WindowPartitionKey
{
    internal WindowPartitionKey(object?[] values)
    {
        Values = values;
    }

    internal object?[] Values { get; }
}

/// <summary>
/// Compares window partition keys using default cell equality so null keys remain valid.
/// </summary>
internal sealed class WindowPartitionKeyComparer : IEqualityComparer<WindowPartitionKey>
{
    internal static readonly WindowPartitionKeyComparer Instance = new();

    private WindowPartitionKeyComparer()
    {
    }

    public bool Equals(WindowPartitionKey? left, WindowPartitionKey? right)
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
            if (!EqualityComparer<object?>.Default.Equals(left.Values[index], right.Values[index]))
            {
                return false;
            }
        }

        return true;
    }

    public int GetHashCode(WindowPartitionKey key)
    {
        var hash = new HashCode();
        foreach (var value in key.Values)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }
}
