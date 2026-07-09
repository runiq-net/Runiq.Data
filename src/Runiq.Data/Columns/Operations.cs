using System.Collections;
using Runiq.Data.Series;

namespace Runiq.Data;

/// <summary>
/// Provides column inspection and mutation operations for a DataFrame instance.
/// </summary>
public sealed class ColumnOperations : IEnumerable<ISeries>
{
    private readonly DataFrame dataFrame;

    internal ColumnOperations(DataFrame dataFrame)
    {
        this.dataFrame = dataFrame;
    }

    /// <summary>
    /// Gets the column at the specified schema ordinal without mutating the owning DataFrame.
    /// </summary>
    /// <param name="index">The zero-based column index to read.</param>
    /// <returns>The column stored at the requested ordinal.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="index"/> is negative or outside the DataFrame column range.
    /// </exception>
    public ISeries this[int index] => dataFrame.ColumnSeries[index];

    /// <summary>
    /// Appends a validated column to the current DataFrame.
    /// </summary>
    /// <typeparam name="T">The CLR type of each value in the new column.</typeparam>
    /// <param name="name">The canonical name of the column to append.</param>
    /// <param name="values">The values to snapshot into the new column.</param>
    /// <remarks>
    /// This operation mutates the owning DataFrame by adding the new column after existing
    /// columns. Existing rows, existing columns, and column order are preserved. Validation fails
    /// fast before mutation when the name is invalid, conflicts with an existing column, when
    /// <paramref name="values"/> is <see langword="null"/> or a string, or when the value count
    /// does not match the current row count.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the name is invalid or duplicated, when values are a string, or when the value
    /// count does not match the DataFrame row count.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> or <paramref name="values"/> is <see langword="null"/>.
    /// </exception>
    public void Add<T>(string name, IEnumerable<T> values)
    {
        dataFrame.AddColumnCore(name, values);
    }

    /// <summary>
    /// Removes a validated column from the current DataFrame.
    /// </summary>
    /// <param name="name">The column name to remove, matched case-insensitively.</param>
    /// <remarks>
    /// This operation mutates the owning DataFrame by removing one column while preserving row
    /// count, row order, and the relative order of all remaining columns. Validation fails fast
    /// when the name is invalid, the column is missing, or removing it would leave the DataFrame
    /// without any columns.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or whitespace, or when removing the column
    /// would leave no columns.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no matching column exists.
    /// </exception>
    public void Remove(string name)
    {
        dataFrame.RemoveColumnCore(name);
    }

    /// <summary>
    /// Renames a validated column on the current DataFrame.
    /// </summary>
    /// <param name="oldName">The existing column name to rename, matched case-insensitively.</param>
    /// <param name="newName">The canonical column name to store after the rename.</param>
    /// <remarks>
    /// This operation mutates the owning DataFrame while preserving row count, column count,
    /// column order, values, data types, and nullability metadata. Validation fails fast when the
    /// source name is missing, either name is invalid, or the target name conflicts with another
    /// column. Renaming only the casing of the same column is allowed.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when either name is empty or whitespace, or when <paramref name="newName"/>
    /// conflicts with another column.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="oldName"/> or <paramref name="newName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when <paramref name="oldName"/> does not match an existing column.
    /// </exception>
    public void Rename(string oldName, string newName)
    {
        dataFrame.RenameColumnCore(oldName, newName);
    }

    /// <summary>
    /// Returns the current number of columns without mutating the owning DataFrame.
    /// </summary>
    /// <returns>The number of columns currently stored in the DataFrame.</returns>
    public int Count()
    {
        return dataFrame.ColumnTotalCore;
    }

    public IEnumerator<ISeries> GetEnumerator()
    {
        return dataFrame.ColumnSeries.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}


