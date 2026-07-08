namespace Runiq.Data.Series;

/// <summary>
/// Represents an immutable, ordered, strongly typed sequence of values.
/// </summary>
/// <typeparam name="T">The CLR type of each value in the series.</typeparam>
/// <remarks>
/// <para>
/// <see cref="Series{T}"/> is the foundational typed value container for Runiq.Data. Future
/// DataFrame columns can use a series to hold their ordered values while preserving the column
/// name, CLR data type, and simple nullability metadata.
/// </para>
/// <para>
/// Creation materializes the supplied values into an internal snapshot, so later changes to the
/// source collection cannot change the series. Nullability is intentionally simple: reference
/// types and <see cref="Nullable{T}"/> value types are reported as nullable, while non-nullable
/// value types are reported as not nullable.
/// </para>
/// </remarks>
public sealed class Series<T>
{
    private readonly T[] values;

    private Series(string name, T[] values)
    {
        Name = name;
        this.values = values;
        Values = Array.AsReadOnly(values);
        Count = values.Length;
        DataType = typeof(T);
        IsNullable = !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) is not null;
    }

    /// <summary>
    /// Gets the series name used to identify the value sequence.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the number of values stored in the series.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets the CLR type represented by values in the series.
    /// </summary>
    public Type DataType { get; }

    /// <summary>
    /// Gets a value indicating whether the series type permits null values by simple CLR rules.
    /// </summary>
    public bool IsNullable { get; }

    /// <summary>
    /// Gets the immutable values in their original order.
    /// </summary>
    public IReadOnlyList<T> Values { get; }

    /// <summary>
    /// Gets the value at the specified zero-based row index.
    /// </summary>
    /// <param name="index">The zero-based row index to read.</param>
    /// <returns>The value stored at the specified position.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="index"/> is less than zero or greater than or equal to
    /// <see cref="Count"/>.
    /// </exception>
    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Series index must be within the value range.");
            }

            return values[index];
        }
    }

    /// <summary>
    /// Creates a validated immutable series from the supplied ordered values.
    /// </summary>
    /// <param name="name">The non-empty name of the series.</param>
    /// <param name="values">The ordered values to snapshot into the series.</param>
    /// <returns>A series containing a read-only snapshot of the supplied values.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or contains only whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> or <paramref name="values"/> is <see langword="null"/>.
    /// </exception>
    public static Series<T> Create(string name, IEnumerable<T> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(values);

        return new Series<T>(name, values.ToArray());
    }
}
