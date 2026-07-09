namespace Runiq.Data;

/// <summary>
/// Represents a read-only row view used when evaluating DataFrame filter predicates.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DataFrameFilterRow"/> is separate from <see cref="DataFrameRow"/> so direct row
/// access can continue returning raw object values while filtering predicates receive
/// <see cref="CellValue"/> instances that support natural comparison operators.
/// </para>
/// <para>
/// Column lookup follows DataFrame row access semantics, including case-insensitive names and
/// fail-fast validation for missing or invalid columns.
/// </para>
/// </remarks>
public sealed class DataFrameFilterRow
{
    private readonly DataFrame dataFrame;
    private readonly int rowIndex;

    internal DataFrameFilterRow(DataFrame dataFrame, int rowIndex)
    {
        this.dataFrame = dataFrame;
        this.rowIndex = rowIndex;
    }

    /// <summary>
    /// Gets a comparable cell value for the specified column in this filter row.
    /// </summary>
    /// <param name="columnName">The column name to read using DataFrame lookup semantics.</param>
    /// <returns>
    /// A <see cref="CellValue"/> that exposes the stored value and supports fail-fast filtering
    /// comparisons for supported primitive types.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or contains only whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no matching column exists.
    /// </exception>
    public CellValue this[string columnName]
    {
        get
        {
            var column = dataFrame.GetColumn(columnName);
            return new CellValue(column.Name, rowIndex, column.GetValue(rowIndex));
        }
    }
}
