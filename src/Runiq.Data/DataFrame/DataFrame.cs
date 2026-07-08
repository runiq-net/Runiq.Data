using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using Runiq.Data.Schema;
using Runiq.Data.Series;

namespace Runiq.Data;

/// <summary>
/// Represents an immutable, column-oriented table of named, equally sized series.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DataFrame"/> is the primary consumer-facing object model for tabular data in
/// Runiq.Data. The simplest creation path is <see cref="Create(object)"/>, where public readable
/// properties on an anonymous or simple object become DataFrame columns.
/// </para>
/// <para>
/// A DataFrame owns snapshots of the supplied column values, preserves column order, requires
/// all columns to have the same row count, and performs column lookup using case-insensitive
/// names for convenience.
/// </para>
/// </remarks>
public sealed class DataFrame
{
    private static readonly MethodInfo CreateSeriesMethod = typeof(DataFrame)
        .GetMethod(nameof(CreateSeries), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly IReadOnlyDictionary<string, ISeries> columnsByName;

    private DataFrame(DataFrameSchema schema, ReadOnlyCollection<ISeries> columns)
    {
        Schema = schema;
        Columns = columns;
        ColumnCount = columns.Count;
        RowCount = columns.Count == 0 ? 0 : columns[0].Count;
        columnsByName = columns.ToDictionary(static column => column.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the schema inferred from the DataFrame columns or supplied during schema-first creation.
    /// </summary>
    public DataFrameSchema Schema { get; }

    /// <summary>
    /// Gets the DataFrame columns in schema order.
    /// </summary>
    public IReadOnlyList<ISeries> Columns { get; }

    /// <summary>
    /// Gets the number of rows in each DataFrame column.
    /// </summary>
    public int RowCount { get; }

    /// <summary>
    /// Gets the number of columns in the DataFrame.
    /// </summary>
    public int ColumnCount { get; }

    /// <summary>
    /// Gets the column with the specified name using case-insensitive lookup.
    /// </summary>
    /// <param name="columnName">The column name to find.</param>
    /// <returns>The matching column.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or contains only whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no matching column exists.
    /// </exception>
    public ISeries this[string columnName] => GetColumn(columnName);

    /// <summary>
    /// Creates an immutable DataFrame from public readable properties on an object.
    /// </summary>
    /// <param name="columns">
    /// An anonymous or simple object whose public readable properties provide column names and
    /// enumerable column values.
    /// </param>
    /// <returns>A DataFrame containing read-only snapshots of the supplied values.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the object has no public readable properties, a property value is invalid, a
    /// duplicate column name is detected, or column row counts do not match.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columns"/> is <see langword="null"/>.
    /// </exception>
    public static DataFrame Create(object columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        var columnSeries = CreateSeriesFromObject(columns);
        var schema = DataFrameSchema.Create(columnSeries
            .Select((column, ordinal) => ColumnSchema.Create(column.Name, column.DataType, column.IsNullable, ordinal))
            .ToArray());

        return new DataFrame(schema, Array.AsReadOnly(columnSeries));
    }

    /// <summary>
    /// Creates an immutable DataFrame from an object while validating against an expected schema.
    /// </summary>
    /// <param name="schema">The expected schema that controls final column order.</param>
    /// <param name="columns">
    /// An anonymous or simple object whose public readable properties provide column values.
    /// </param>
    /// <returns>A DataFrame whose columns are ordered and validated according to <paramref name="schema"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when object columns do not exactly match the schema, data types are incompatible,
    /// nullability is incompatible, or row counts do not match.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="schema"/> or <paramref name="columns"/> is <see langword="null"/>.
    /// </exception>
    public static DataFrame Create(DataFrameSchema schema, object columns)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(columns);

        var columnSeries = CreateSeriesFromObject(columns);
        var objectColumnsByName = columnSeries.ToDictionary(static column => column.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var column in columnSeries)
        {
            if (!schema.ContainsColumn(column.Name))
            {
                throw new ArgumentException($"Column '{column.Name}' does not exist in the expected schema.", nameof(columns));
            }
        }

        var orderedColumns = new ISeries[schema.Columns.Count];
        foreach (var expectedColumn in schema.Columns)
        {
            if (!objectColumnsByName.TryGetValue(expectedColumn.Name, out var actualColumn))
            {
                throw new ArgumentException($"Expected schema column '{expectedColumn.Name}' was not supplied.", nameof(columns));
            }

            if (actualColumn.DataType != expectedColumn.DataType)
            {
                throw new ArgumentException(
                    $"Column '{expectedColumn.Name}' has data type '{actualColumn.DataType}' but the schema expects '{expectedColumn.DataType}'.",
                    nameof(columns));
            }

            if (!expectedColumn.IsNullable && actualColumn.IsNullable)
            {
                throw new ArgumentException(
                    $"Column '{expectedColumn.Name}' is nullable but the schema requires non-null values.",
                    nameof(columns));
            }

            orderedColumns[expectedColumn.Ordinal] = CreateSeriesWithName(expectedColumn.Name, actualColumn);
        }

        ValidateRowCounts(orderedColumns);

        return new DataFrame(schema, Array.AsReadOnly(orderedColumns));
    }

    /// <summary>
    /// Projects the DataFrame to a new immutable DataFrame containing only the requested columns.
    /// </summary>
    /// <param name="columnNames">
    /// The column names to keep, in the exact order they should appear in the result.
    /// </param>
    /// <returns>
    /// A new DataFrame whose rows are unchanged and whose columns and schema are limited to the
    /// selected columns.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when no column names are supplied, a column name is empty or whitespace, or a column
    /// name is selected more than once using case-insensitive comparison.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnNames"/> or one of its entries is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when a requested column does not exist.
    /// </exception>
    public DataFrame Select(params string[] columnNames)
    {
        ArgumentNullException.ThrowIfNull(columnNames);

        if (columnNames.Length == 0)
        {
            throw new ArgumentException("At least one column name must be selected.", nameof(columnNames));
        }

        var selectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selectedColumns = new ISeries[columnNames.Length];
        var selectedSchemaColumns = new ColumnSchema[columnNames.Length];

        for (var index = 0; index < columnNames.Length; index++)
        {
            var columnName = columnNames[index];
            ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

            if (!selectedNames.Add(columnName))
            {
                throw new ArgumentException($"Column '{columnName}' was selected more than once.", nameof(columnNames));
            }

            var column = GetColumn(columnName);
            var schemaColumn = Schema.GetColumn(column.Name);

            selectedColumns[index] = column;
            selectedSchemaColumns[index] = ColumnSchema.Create(
                schemaColumn.Name,
                schemaColumn.DataType,
                schemaColumn.IsNullable,
                index);
        }

        var schema = DataFrameSchema.Create(selectedSchemaColumns);
        return new DataFrame(schema, Array.AsReadOnly(selectedColumns));
    }

    /// <summary>
    /// Projects the DataFrame to a new immutable DataFrame without the requested columns.
    /// </summary>
    /// <param name="columnNames">The column names to remove from the result.</param>
    /// <returns>
    /// A new DataFrame whose rows are unchanged and whose columns and schema contain every
    /// source column except the requested drops, preserving the remaining source order.
    /// </returns>
    /// <remarks>
    /// Column lookup is case-insensitive, but remaining columns keep their original source names.
    /// Missing columns are rejected by default instead of being ignored.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when no column names are supplied, a column name is empty or whitespace, a column
    /// name is dropped more than once using case-insensitive comparison, or all columns would be
    /// removed.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnNames"/> or one of its entries is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when a requested drop column does not exist.
    /// </exception>
    public DataFrame Drop(params string[] columnNames)
    {
        ArgumentNullException.ThrowIfNull(columnNames);

        if (columnNames.Length == 0)
        {
            throw new ArgumentException("At least one column name must be dropped.", nameof(columnNames));
        }

        var droppedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var columnName in columnNames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

            if (!droppedNames.Add(columnName))
            {
                throw new ArgumentException($"Column '{columnName}' was dropped more than once.", nameof(columnNames));
            }

            if (!columnsByName.ContainsKey(columnName))
            {
                throw new KeyNotFoundException($"Column '{columnName}' does not exist in the DataFrame.");
            }
        }

        if (droppedNames.Count == ColumnCount)
        {
            throw new ArgumentException("Dropping all columns is not supported because a DataFrame schema must contain at least one column.", nameof(columnNames));
        }

        var remainingColumns = Columns
            .Where(column => !droppedNames.Contains(column.Name))
            .ToArray();
        var remainingSchemaColumns = remainingColumns
            .Select((column, index) =>
            {
                var schemaColumn = Schema.GetColumn(column.Name);
                return ColumnSchema.Create(schemaColumn.Name, schemaColumn.DataType, schemaColumn.IsNullable, index);
            })
            .ToArray();

        var schema = DataFrameSchema.Create(remainingSchemaColumns);
        return new DataFrame(schema, Array.AsReadOnly(remainingColumns));
    }

    /// <summary>
    /// Renames one column and returns a new immutable DataFrame with the same rows, values, and
    /// column order.
    /// </summary>
    /// <param name="currentName">The existing column name to rename, matched case-insensitively.</param>
    /// <param name="newName">The canonical column name to use in the returned DataFrame.</param>
    /// <returns>
    /// A new DataFrame whose schema and column collection contain <paramref name="newName"/> in
    /// the source column's original position.
    /// </returns>
    /// <remarks>
    /// The source DataFrame is not modified. Missing source columns are rejected, and target names
    /// that conflict with another existing column are rejected using case-insensitive comparison.
    /// Renaming only the casing of the same column is allowed and updates the canonical name.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when either name is empty or contains only whitespace, or when
    /// <paramref name="newName"/> conflicts with another existing column.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="currentName"/> or <paramref name="newName"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when <paramref name="currentName"/> does not match an existing column.
    /// </exception>
    public DataFrame RenameColumn(string currentName, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        var sourceColumn = GetColumn(currentName);
        if (columnsByName.TryGetValue(newName, out var conflictingColumn) && !ReferenceEquals(sourceColumn, conflictingColumn))
        {
            throw new ArgumentException($"Column '{newName}' conflicts with an existing DataFrame column.", nameof(newName));
        }

        var sourceSchemaColumn = Schema.GetColumn(sourceColumn.Name);
        var renamedColumns = Columns
            .Select(column => ReferenceEquals(column, sourceColumn) ? CreateSeriesWithName(newName, column) : column)
            .ToArray();
        var renamedSchemaColumns = Schema.Columns
            .Select(column => column.Ordinal == sourceSchemaColumn.Ordinal
                ? ColumnSchema.Create(newName, column.DataType, column.IsNullable, column.Ordinal)
                : column)
            .ToArray();

        var schema = DataFrameSchema.Create(renamedSchemaColumns);
        return new DataFrame(schema, Array.AsReadOnly(renamedColumns));
    }

    /// <summary>
    /// Gets the column with the specified name using case-insensitive lookup.
    /// </summary>
    /// <param name="columnName">The column name to find.</param>
    /// <returns>The matching column.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or contains only whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no matching column exists.
    /// </exception>
    public ISeries GetColumn(string columnName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

        if (columnsByName.TryGetValue(columnName, out var column))
        {
            return column;
        }

        throw new KeyNotFoundException($"No column named '{columnName}' exists in the DataFrame.");
    }

    /// <summary>
    /// Determines whether the DataFrame contains a column with the specified name.
    /// </summary>
    /// <param name="columnName">The column name to check.</param>
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
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

        return columnsByName.ContainsKey(columnName);
    }

    private static ISeries[] CreateSeriesFromObject(object columns)
    {
        var properties = columns.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetMethod is not null && property.GetMethod.GetParameters().Length == 0)
            .ToArray();

        if (properties.Length == 0)
        {
            throw new ArgumentException("A DataFrame requires at least one public readable column property.", nameof(columns));
        }

        var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var series = new ISeries[properties.Length];

        for (var index = 0; index < properties.Length; index++)
        {
            var property = properties[index];
            if (!columnNames.Add(property.Name))
            {
                throw new ArgumentException($"A column named '{property.Name}' has already been supplied.", nameof(columns));
            }

            var value = property.GetValue(columns);
            series[index] = CreateColumnSeries(property.Name, value);
        }

        ValidateRowCounts(series);

        return series;
    }

    private static ISeries CreateColumnSeries(string name, object? value)
    {
        if (value is null)
        {
            throw new ArgumentException($"Column '{name}' cannot be null.", nameof(value));
        }

        if (value is string)
        {
            throw new ArgumentException($"Column '{name}' must be an enumerable value collection, not a string.", nameof(value));
        }

        if (value is not IEnumerable enumerable)
        {
            throw new ArgumentException($"Column '{name}' must be an enumerable value collection.", nameof(value));
        }

        var elementType = GetEnumerableElementType(value.GetType());
        if (elementType is null)
        {
            throw new ArgumentException($"Column '{name}' must implement IEnumerable<T> so the element type can be inferred.", nameof(value));
        }

        var snapshotValues = enumerable.Cast<object?>().ToArray();
        var snapshot = Array.CreateInstance(elementType, snapshotValues.Length);
        var index = 0;
        foreach (var item in snapshotValues)
        {
            snapshot.SetValue(item, index);
            index++;
        }

        var genericCreateMethod = CreateSeriesMethod.MakeGenericMethod(elementType);
        return (ISeries)genericCreateMethod.Invoke(null, [name, snapshot])!;
    }

    private static Type? GetEnumerableElementType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return type.GetGenericArguments()[0];
        }

        return type
            .GetInterfaces()
            .Where(static interfaceType => interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(static interfaceType => interfaceType.GetGenericArguments()[0])
            .FirstOrDefault();
    }

    private static ISeries CreateSeries<T>(string name, IEnumerable<T> values)
    {
        return Series<T>.Create(name, values);
    }

    private static ISeries CreateSeriesWithName(string name, ISeries column)
    {
        if (column.Name == name)
        {
            return column;
        }

        var values = Array.CreateInstance(column.DataType, column.Count);
        for (var index = 0; index < column.Count; index++)
        {
            values.SetValue(column.GetValue(index), index);
        }

        var genericCreateMethod = CreateSeriesMethod.MakeGenericMethod(column.DataType);
        return (ISeries)genericCreateMethod.Invoke(null, [name, values])!;
    }

    private static void ValidateRowCounts(IReadOnlyList<ISeries> columns)
    {
        var expectedRowCount = columns[0].Count;
        foreach (var column in columns)
        {
            if (column.Count != expectedRowCount)
            {
                throw new ArgumentException(
                    $"Column '{column.Name}' contains {column.Count} values, but the DataFrame expects {expectedRowCount} values.");
            }
        }
    }
}
