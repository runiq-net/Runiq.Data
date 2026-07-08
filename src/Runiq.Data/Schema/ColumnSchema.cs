namespace Runiq.Data.Schema;

/// <summary>
/// Describes the name, data type, nullability, and position of a single DataFrame column.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ColumnSchema"/> is the smallest schema unit in Runiq.Data. It captures the
/// structural contract for a column without storing column values.
/// </para>
/// <para>
/// Instances are immutable. Use <see cref="Create(string, Type, bool, int)"/> to construct
/// validated column definitions.
/// </para>
/// </remarks>
public sealed class ColumnSchema
{
    private ColumnSchema(string name, Type dataType, bool isNullable, int ordinal)
    {
        Name = name;
        DataType = dataType;
        IsNullable = isNullable;
        Ordinal = ordinal;
    }

    /// <summary>
    /// Gets the column name used for schema identity and name-based lookup.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the CLR type expected for values stored in the column.
    /// </summary>
    public Type DataType { get; }

    /// <summary>
    /// Gets a value indicating whether the column permits null values.
    /// </summary>
    public bool IsNullable { get; }

    /// <summary>
    /// Gets the zero-based position of the column within its DataFrame schema.
    /// </summary>
    public int Ordinal { get; }

    /// <summary>
    /// Creates a validated immutable column schema.
    /// </summary>
    /// <param name="name">The non-empty column name.</param>
    /// <param name="dataType">The CLR type expected for column values.</param>
    /// <param name="nullable">A value indicating whether null values are permitted.</param>
    /// <param name="ordinal">The zero-based column position.</param>
    /// <returns>A column schema containing the provided definition.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or contains only whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> or <paramref name="dataType"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="ordinal"/> is less than zero.
    /// </exception>
    public static ColumnSchema Create(string name, Type dataType, bool nullable, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(dataType);

        if (ordinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal), ordinal, "Column ordinal must be zero or greater.");
        }

        return new ColumnSchema(name, dataType, nullable, ordinal);
    }
}
