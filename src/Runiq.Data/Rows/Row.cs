namespace Runiq.Data;

/// <summary>
/// Represents a read-only view over one DataFrame row.
/// </summary>
/// <remarks>
/// The row keeps DataFrame column lookup semantics, including case-insensitive names and
/// fail-fast validation for missing or invalid column names. Values are read from the selected
/// zero-based row index and cannot be mutated through this view.
/// </remarks>
public sealed class Row
{
    private readonly DataFrame dataFrame;
    private readonly int rowIndex;

    internal Row(DataFrame dataFrame, int rowIndex)
    {
        this.dataFrame = dataFrame;
        this.rowIndex = rowIndex;
    }

    /// <summary>
    /// Gets the DataFrame column names in their current schema order.
    /// </summary>
    public IReadOnlyList<string> ColumnNames => Array.AsReadOnly(dataFrame.ColumnSeries.Select(static column => column.Name).ToArray());

    /// <summary>
    /// Gets the raw value for the specified column in this row.
    /// </summary>
    /// <param name="columnName">The column name to read using DataFrame lookup semantics.</param>
    /// <returns>The value stored in the selected row for the requested column.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or contains only whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no matching column exists.
    /// </exception>
    public object? this[string columnName] => dataFrame.GetColumn(columnName).GetValue(rowIndex);

    /// <summary>
    /// Determines whether this row's DataFrame contains a column with the specified name.
    /// </summary>
    /// <param name="columnName">The column name to check using DataFrame lookup semantics.</param>
    /// <returns>
    /// <see langword="true"/> when a matching column exists; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or contains only whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    public bool HasColumn(string columnName)
    {
        return dataFrame.HasColumn(columnName);
    }

    /// <summary>
    /// Reads a non-null string value from the specified column.
    /// </summary>
    /// <param name="columnName">The column name to read using DataFrame lookup semantics.</param>
    /// <returns>The string value stored in this row.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the column name is invalid, the column value is null, or the value is not a
    /// string.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no matching column exists.
    /// </exception>
    public string String(string columnName)
    {
        return ReadRequired<string>(columnName, "string");
    }

    /// <summary>
    /// Reads a non-null integer value from the specified column.
    /// </summary>
    /// <param name="columnName">The column name to read using DataFrame lookup semantics.</param>
    /// <returns>The integer value stored in this row.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the column name is invalid, the column value is null, or the value is not an
    /// integer.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no matching column exists.
    /// </exception>
    public int Int(string columnName)
    {
        return ReadRequired<int>(columnName, "int");
    }

    /// <summary>
    /// Reads a non-null long integer value from the specified column.
    /// </summary>
    /// <param name="columnName">The column name to read using DataFrame lookup semantics.</param>
    /// <returns>The long integer value stored in this row.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the column name is invalid, the column value is null, or the value is not a
    /// long integer.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no matching column exists.
    /// </exception>
    public long Long(string columnName)
    {
        return ReadRequired<long>(columnName, "long");
    }

    /// <summary>
    /// Reads a non-null decimal value from the specified column.
    /// </summary>
    /// <param name="columnName">The column name to read using DataFrame lookup semantics.</param>
    /// <returns>The decimal value stored in this row.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the column name is invalid, the column value is null, or the value is not a
    /// decimal.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no matching column exists.
    /// </exception>
    public decimal Decimal(string columnName)
    {
        return ReadRequired<decimal>(columnName, "decimal");
    }

    /// <summary>
    /// Reads a non-null double value from the specified column.
    /// </summary>
    /// <param name="columnName">The column name to read using DataFrame lookup semantics.</param>
    /// <returns>The double value stored in this row.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the column name is invalid, the column value is null, or the value is not a
    /// double.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no matching column exists.
    /// </exception>
    public double Double(string columnName)
    {
        return ReadRequired<double>(columnName, "double");
    }

    /// <summary>
    /// Reads a non-null Boolean value from the specified column.
    /// </summary>
    /// <param name="columnName">The column name to read using DataFrame lookup semantics.</param>
    /// <returns>The Boolean value stored in this row.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the column name is invalid, the column value is null, or the value is not a
    /// Boolean.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no matching column exists.
    /// </exception>
    public bool Bool(string columnName)
    {
        return ReadRequired<bool>(columnName, "bool");
    }

    /// <summary>
    /// Reads a non-null DateTime value from the specified column.
    /// </summary>
    /// <param name="columnName">The column name to read using DataFrame lookup semantics.</param>
    /// <returns>The DateTime value stored in this row.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the column name is invalid, the column value is null, or the value is not a
    /// DateTime.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no matching column exists.
    /// </exception>
    public global::System.DateTime DateTime(string columnName)
    {
        return ReadRequired<global::System.DateTime>(columnName, "DateTime");
    }

    private T ReadRequired<T>(string columnName, string expectedTypeName)
    {
        var value = this[columnName];
        if (value is T typedValue)
        {
            return typedValue;
        }

        var actualTypeName = value is null ? "null" : value.GetType().Name;
        throw new ArgumentException(
            $"Column '{columnName}' in row {rowIndex} contains value type '{actualTypeName}' but expected '{expectedTypeName}'.",
            nameof(columnName));
    }
}

