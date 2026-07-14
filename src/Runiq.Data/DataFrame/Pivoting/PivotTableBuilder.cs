using Runiq.Data.Series;

namespace Runiq.Data;

/// <summary>
/// Builds a pivot table from one index column, one pivot column, and one values column.
/// </summary>
/// <remarks>
/// Instances are created by <see cref="DataFrame.PivotTable(string, string, string)"/>. The
/// builder stores only the source DataFrame and source column roles; aggregation is delayed until
/// a terminal method is called. Each terminal method returns a new DataFrame and does not mutate
/// the source DataFrame.
/// </remarks>
public sealed class PivotTableBuilder
{
    private readonly DataFrame source;
    private readonly string indexColumnName;
    private readonly string pivotColumnName;
    private readonly string valuesColumnName;

    internal PivotTableBuilder(DataFrame source, string indexColumnName, string pivotColumnName, string valuesColumnName)
    {
        this.source = source;
        this.indexColumnName = indexColumnName;
        this.pivotColumnName = pivotColumnName;
        this.valuesColumnName = valuesColumnName;
    }

    /// <summary>
    /// Sums duplicate pivot table cells using the DataFrame numeric aggregation contract.
    /// </summary>
    /// <returns>
    /// A new DataFrame whose first column is the index column and whose dynamic pivot columns
    /// contain summed values or <see langword="null"/> for missing combinations.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when pivot values cannot produce unique result column names, or when value cells
    /// are incompatible with DataFrame sum semantics.
    /// </exception>
    /// <exception cref="OverflowException">Thrown when checked integer or decimal addition overflows.</exception>
    public DataFrame Sum()
    {
        var valuesColumn = ResolveValuesColumn();
        return PivotTableOperations.Create(source, indexColumnName, pivotColumnName, valuesColumn, DataFrame.GetSumResultType(valuesColumn), static (column, rows) => DataFrame.SumColumn(column, rows));
    }

    /// <summary>
    /// Averages duplicate pivot table cells using the DataFrame numeric aggregation contract.
    /// </summary>
    /// <returns>
    /// A new DataFrame whose first column is the index column and whose dynamic pivot columns
    /// contain average values or <see langword="null"/> for missing combinations.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when pivot values cannot produce unique result column names, or when value cells
    /// are incompatible with DataFrame average semantics.
    /// </exception>
    /// <exception cref="OverflowException">Thrown when checked integer or decimal addition overflows before division.</exception>
    public DataFrame Average()
    {
        var valuesColumn = ResolveValuesColumn();
        DataFrame.GetNumericAggregationType(valuesColumn);
        return PivotTableOperations.Create(source, indexColumnName, pivotColumnName, valuesColumn, typeof(double), static (column, rows) => DataFrame.AverageColumn(column, rows));
    }

    /// <summary>
    /// Finds the minimum value for duplicate pivot table cells using the DataFrame comparison contract.
    /// </summary>
    /// <returns>
    /// A new DataFrame whose first column is the index column and whose dynamic pivot columns
    /// contain minimum values or <see langword="null"/> for missing combinations.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when pivot values cannot produce unique result column names, or when value cells
    /// are incompatible with DataFrame minimum semantics.
    /// </exception>
    public DataFrame Min()
    {
        var valuesColumn = ResolveValuesColumn();
        return PivotTableOperations.Create(source, indexColumnName, pivotColumnName, valuesColumn, valuesColumn.DataType, static (column, rows) => DataFrame.MinOrMaxColumn(column, rows, findMaximum: false));
    }

    /// <summary>
    /// Finds the maximum value for duplicate pivot table cells using the DataFrame comparison contract.
    /// </summary>
    /// <returns>
    /// A new DataFrame whose first column is the index column and whose dynamic pivot columns
    /// contain maximum values or <see langword="null"/> for missing combinations.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when pivot values cannot produce unique result column names, or when value cells
    /// are incompatible with DataFrame maximum semantics.
    /// </exception>
    public DataFrame Max()
    {
        var valuesColumn = ResolveValuesColumn();
        return PivotTableOperations.Create(source, indexColumnName, pivotColumnName, valuesColumn, valuesColumn.DataType, static (column, rows) => DataFrame.MinOrMaxColumn(column, rows, findMaximum: true));
    }

    /// <summary>
    /// Resolves the values column at execution time so terminal calls observe the current source schema.
    /// </summary>
    private ISeries ResolveValuesColumn()
    {
        return source.GetColumn(valuesColumnName);
    }
}
