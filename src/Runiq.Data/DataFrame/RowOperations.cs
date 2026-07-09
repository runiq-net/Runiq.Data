namespace Runiq.Data;

/// <summary>
/// Provides row mutation operations for a DataFrame instance.
/// </summary>
public sealed class DataFrameRows
{
    private readonly DataFrame dataFrame;

    internal DataFrameRows(DataFrame dataFrame)
    {
        this.dataFrame = dataFrame;
    }

    /// <summary>
    /// Appends a validated row to the end of the current DataFrame.
    /// </summary>
    /// <param name="row">
    /// An anonymous or simple object whose public readable properties exactly match the current
    /// DataFrame columns by name.
    /// </param>
    /// <remarks>
    /// This operation mutates the owning DataFrame by adding the supplied row after the existing
    /// last row. The DataFrame schema, column order, column types, nullability metadata, and
    /// existing rows are preserved. Validation fails fast when <paramref name="row"/> is
    /// <see langword="null"/>, when any existing column is missing, when extra properties are
    /// supplied, or when a value is incompatible with the target column type and nullability
    /// contract.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the row does not exactly match the DataFrame schema or contains incompatible values.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="row"/> is <see langword="null"/>.
    /// </exception>
    public void Add(object row)
    {
        dataFrame.AppendRowCore(row);
    }

    /// <summary>
    /// Replaces the full row at the specified zero-based index on the current DataFrame.
    /// </summary>
    /// <param name="index">The zero-based row index to replace.</param>
    /// <param name="row">
    /// An anonymous or simple object whose public readable properties exactly match the current
    /// DataFrame columns by name.
    /// </param>
    /// <remarks>
    /// This operation mutates the owning DataFrame with a complete row replacement. Row count,
    /// column count, schema, column order, column types, nullability metadata, and rows outside
    /// <paramref name="index"/> are preserved. Validation fails fast when
    /// <paramref name="index"/> is outside the current row range, when <paramref name="row"/> is
    /// <see langword="null"/>, when any existing column is missing, when extra properties are
    /// supplied, or when a value is incompatible with the target column type.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the row does not exactly match the DataFrame schema or contains incompatible values.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="row"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="index"/> is negative or outside the DataFrame row range.
    /// </exception>
    public void Update(int index, object row)
    {
        dataFrame.ReplaceRowCore(index, row);
    }

    /// <summary>
    /// Removes the row at the specified zero-based index from the current DataFrame.
    /// </summary>
    /// <param name="index">The zero-based row index to remove.</param>
    /// <remarks>
    /// This operation mutates the owning DataFrame by removing one row and preserving the
    /// DataFrame schema, column count, column order, column types, and nullability metadata.
    /// Remaining rows keep their relative order. Validation fails fast when
    /// <paramref name="index"/> is negative or equal to or greater than the current row count.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="index"/> is negative or outside the DataFrame row range.
    /// </exception>
    public void Remove(int index)
    {
        dataFrame.DeleteRowCore(index);
    }

    /// <summary>
    /// Returns the current number of rows without mutating the owning DataFrame.
    /// </summary>
    /// <remarks>
    /// The returned value reflects prior row mutations performed through this facade and column
    /// mutations performed through <see cref="DataFrame.Columns"/>.
    /// </remarks>
    /// <returns>The number of rows currently stored in the DataFrame.</returns>
    public int Count()
    {
        return dataFrame.RowTotalCore;
    }
}


