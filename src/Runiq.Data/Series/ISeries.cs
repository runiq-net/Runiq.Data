namespace Runiq.Data.Series;

/// <summary>
/// Represents a read-only DataFrame column without exposing its generic value type.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ISeries"/> lets a DataFrame store columns with different CLR element types while
/// keeping a small public contract for column metadata and row value access.
/// </para>
/// <para>
/// Implementations are expected to be immutable from the public API and to preserve value order.
/// </para>
/// </remarks>
public interface ISeries
{
    /// <summary>
    /// Gets the column name used for DataFrame lookup and schema creation.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the number of values stored in the column.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the CLR type represented by values in the column.
    /// </summary>
    Type DataType { get; }

    /// <summary>
    /// Gets a value indicating whether the column's CLR type permits null values.
    /// </summary>
    bool IsNullable { get; }

    /// <summary>
    /// Gets the value at the specified zero-based row index as an object.
    /// </summary>
    /// <param name="index">The zero-based row index to read.</param>
    /// <returns>The value stored at the specified position.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="index"/> is outside the available row range.
    /// </exception>
    object? GetValue(int index);
}
