using Runiq.Data.Series;

namespace Runiq.Data;

/// <summary>
/// Creates aggregated pivot table DataFrames while sharing pivot validation and aggregation helpers.
/// </summary>
internal static class PivotTableOperations
{
    /// <summary>
    /// Creates a pivot table by grouping source row ordinals per result cell and aggregating each bucket.
    /// </summary>
    internal static DataFrame Create(
        DataFrame source,
        string indexColumnName,
        string pivotColumnName,
        ISeries valuesColumn,
        Type aggregateResultType,
        Func<ISeries, IReadOnlyList<int>, object?> aggregate)
    {
        var indexColumn = source.GetColumn(indexColumnName);
        var pivotColumn = source.GetColumn(pivotColumnName);

        if (source.RowTotalCore == 0)
        {
            return DataFrame.CreateFromSeries([DataFrame.CreateSeriesFromValues(indexColumn.Name, indexColumn.DataType, [])]);
        }

        var shape = BuildShape(source, indexColumn, pivotColumn);
        var resultColumns = new List<ISeries>(shape.PivotColumnNames.Count + 1)
        {
            DataFrame.CreateSeriesFromValues(indexColumn.Name, indexColumn.DataType, shape.IndexValues)
        };

        var resultColumnType = CreateNullableResultType(aggregateResultType);
        for (var pivotColumnOrdinal = 0; pivotColumnOrdinal < shape.PivotColumnNames.Count; pivotColumnOrdinal++)
        {
            var values = new object?[shape.IndexValues.Count];
            for (var rowOrdinal = 0; rowOrdinal < shape.IndexValues.Count; rowOrdinal++)
            {
                var rowIndexes = shape.CellRows[rowOrdinal][pivotColumnOrdinal];
                values[rowOrdinal] = rowIndexes.Count == 0 ? null : aggregate(valuesColumn, rowIndexes);
            }

            resultColumns.Add(DataFrame.CreateSeriesFromValues(shape.PivotColumnNames[pivotColumnOrdinal], resultColumnType, values));
        }

        return DataFrame.CreateFromSeries(resultColumns);
    }

    /// <summary>
    /// Builds the pivot table row and column shape before aggregation so failures are atomic.
    /// </summary>
    private static PivotTableShape BuildShape(DataFrame source, ISeries indexColumn, ISeries pivotColumn)
    {
        var rowOrdinalsByKey = new Dictionary<PivotIndexKey, int>();
        var indexValues = new List<object?>();
        var pivotColumnOrdinalsByValue = new Dictionary<object, int>();
        var pivotColumnNames = new List<string>();
        var usedResultColumnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { indexColumn.Name };
        var cellRows = new List<List<List<int>>>();

        for (var rowIndex = 0; rowIndex < source.RowTotalCore; rowIndex++)
        {
            var indexValue = indexColumn.GetValue(rowIndex);
            var indexKey = new PivotIndexKey(indexValue);
            if (!rowOrdinalsByKey.TryGetValue(indexKey, out var resultRowOrdinal))
            {
                resultRowOrdinal = indexValues.Count;
                rowOrdinalsByKey.Add(indexKey, resultRowOrdinal);
                indexValues.Add(indexValue);
                cellRows.Add(CreateEmptyCellRow(pivotColumnNames.Count));
            }

            var pivotValue = DataFrame.GetSupportedPivotColumnValue(pivotColumn, rowIndex);
            if (!pivotColumnOrdinalsByValue.TryGetValue(pivotValue, out var resultColumnOrdinal))
            {
                var resultColumnName = DataFrame.ConvertPivotValueToColumnName(pivotColumn, pivotValue);
                if (!usedResultColumnNames.Add(resultColumnName))
                {
                    throw new ArgumentException(
                        $"Pivot column '{pivotColumn.Name}' value '{pivotValue}' produces result column name '{resultColumnName}', which conflicts with another pivot result column or the index column '{indexColumn.Name}'.");
                }

                resultColumnOrdinal = pivotColumnNames.Count;
                pivotColumnOrdinalsByValue.Add(pivotValue, resultColumnOrdinal);
                pivotColumnNames.Add(resultColumnName);
                foreach (var cellRow in cellRows)
                {
                    cellRow.Add([]);
                }
            }

            cellRows[resultRowOrdinal][resultColumnOrdinal].Add(rowIndex);
        }

        return new PivotTableShape(indexValues, pivotColumnNames, cellRows);
    }

    /// <summary>
    /// Creates an empty row of aggregation buckets for the dynamic columns seen so far.
    /// </summary>
    private static List<List<int>> CreateEmptyCellRow(int columnCount)
    {
        var row = new List<List<int>>(columnCount);
        for (var index = 0; index < columnCount; index++)
        {
            row.Add([]);
        }

        return row;
    }

    /// <summary>
    /// Allows missing pivot table combinations to be represented as null without changing filled cell values.
    /// </summary>
    private static Type CreateNullableResultType(Type resultType)
    {
        var nonNullableType = Nullable.GetUnderlyingType(resultType) ?? resultType;
        return nonNullableType.IsValueType ? typeof(Nullable<>).MakeGenericType(nonNullableType) : resultType;
    }

    /// <summary>
    /// Stores the deterministic result shape and source row ordinals for each pivot table cell.
    /// </summary>
    /// <param name="IndexValues">The first-seen index values that define result row order.</param>
    /// <param name="PivotColumnNames">The first-seen, formatted pivot values that define result column order.</param>
    /// <param name="CellRows">The source row ordinals assigned to each result row and pivot column cell.</param>
    private sealed record PivotTableShape(
        IReadOnlyList<object?> IndexValues,
        IReadOnlyList<string> PivotColumnNames,
        IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> CellRows);

    /// <summary>
    /// Wraps index values so null keys use the same equality semantics as non-null keys.
    /// </summary>
    /// <param name="Value">The source index value for a pivot table row.</param>
    private readonly record struct PivotIndexKey(object? Value);
}
