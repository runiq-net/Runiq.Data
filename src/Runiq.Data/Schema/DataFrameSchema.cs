using System.Collections.ObjectModel;

namespace Runiq.Data.Schema;

/// <summary>
/// Describes the ordered set of columns that define a DataFrame shape.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DataFrameSchema"/> is the schema contract for a DataFrame in Runiq.Data. It
/// preserves column order, enforces case-insensitive name uniqueness, and provides name-based
/// lookup without storing any row or column data.
/// </para>
/// <para>
/// Creation normalizes column ordinals to match the supplied order. This keeps the schema's
/// positional invariant explicit even when callers provide reusable column definitions.
/// </para>
/// </remarks>
public sealed class DataFrameSchema
{
    private readonly IReadOnlyDictionary<string, ColumnSchema> columnsByName;

    private DataFrameSchema(ReadOnlyCollection<ColumnSchema> columns)
    {
        Columns = columns;
        Count = columns.Count;
        columnsByName = columns.ToDictionary(static column => column.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the columns in schema order.
    /// </summary>
    public IReadOnlyList<ColumnSchema> Columns { get; }

    /// <summary>
    /// Gets the number of columns in the schema.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Creates a validated immutable DataFrame schema from an ordered set of columns.
    /// </summary>
    /// <param name="columns">The ordered column definitions that make up the schema.</param>
    /// <returns>A schema whose column ordinals match the supplied order.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when no columns are provided, when a column item is <see langword="null"/>, or
    /// when duplicate column names are found using case-insensitive comparison.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columns"/> is <see langword="null"/>.
    /// </exception>
    public static DataFrameSchema Create(params ColumnSchema[] columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        if (columns.Length == 0)
        {
            throw new ArgumentException("A DataFrame schema must contain at least one column.", nameof(columns));
        }

        var normalizedColumns = new ColumnSchema[columns.Length];
        var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < columns.Length; index++)
        {
            var column = columns[index];
            if (column is null)
            {
                throw new ArgumentException("A DataFrame schema cannot contain null columns.", nameof(columns));
            }

            if (!columnNames.Add(column.Name))
            {
                throw new ArgumentException($"A column named '{column.Name}' already exists in the schema.", nameof(columns));
            }

            normalizedColumns[index] = column.Ordinal == index
                ? column
                : ColumnSchema.Create(column.Name, column.DataType, column.IsNullable, index);
        }

        return new DataFrameSchema(Array.AsReadOnly(normalizedColumns));
    }

    /// <summary>
    /// Gets the column with the specified name.
    /// </summary>
    /// <param name="name">The column name to find.</param>
    /// <returns>The matching column schema.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or contains only whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no column exists with the specified name.
    /// </exception>
    public ColumnSchema GetColumn(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (columnsByName.TryGetValue(name, out var column))
        {
            return column;
        }

        throw new KeyNotFoundException($"No column named '{name}' exists in the schema.");
    }

    /// <summary>
    /// Determines whether the schema contains a column with the specified name.
    /// </summary>
    /// <param name="name">The column name to check.</param>
    /// <returns>
    /// <see langword="true"/> when a matching column exists; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or contains only whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> is <see langword="null"/>.
    /// </exception>
    public bool ContainsColumn(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return columnsByName.ContainsKey(name);
    }
}
