using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using Runiq.Data.Schema;
using Runiq.Data.Series;

namespace Runiq.Data;

/// <summary>
/// Represents a column-oriented table of named, equally sized series.
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
/// names for convenience. Projection methods such as <see cref="Select(string[])"/> and
/// <see cref="Drop(string[])"/> return new DataFrame instances. Direct mutation methods such as
/// <see cref="AddColumn{T}(string, IEnumerable{T})"/>, <see cref="RemoveColumn(string)"/>,
/// <see cref="RenameColumn(string, string)"/>, and <see cref="AddRow(object)"/> update the
/// current instance. Call <see cref="Copy"/> first when a separate mutable branch is needed.
/// </para>
/// </remarks>
public sealed class DataFrame
{
    private static readonly MethodInfo CreateSeriesMethod = typeof(DataFrame)
        .GetMethod(nameof(CreateSeries), BindingFlags.NonPublic | BindingFlags.Static)!;

    private DataFrameSchema schema;
    private ReadOnlyCollection<ISeries> columns;
    private IReadOnlyDictionary<string, ISeries> columnsByName;
    private int rowCount;
    private int columnCount;

    private DataFrame(DataFrameSchema schema, ReadOnlyCollection<ISeries> columns)
    {
        this.schema = schema;
        this.columns = columns;
        columnCount = columns.Count;
        rowCount = columns.Count == 0 ? 0 : columns[0].Count;
        columnsByName = CreateColumnLookup(columns);
    }

    /// <summary>
    /// Gets the schema inferred from the DataFrame columns or supplied during schema-first creation.
    /// </summary>
    public DataFrameSchema Schema => schema;

    /// <summary>
    /// Gets the DataFrame columns in schema order.
    /// </summary>
    public IReadOnlyList<ISeries> Columns => columns;

    /// <summary>
    /// Gets the number of rows in each DataFrame column.
    /// </summary>
    public int RowCount => rowCount;

    /// <summary>
    /// Gets the number of columns in the DataFrame.
    /// </summary>
    public int ColumnCount => columnCount;

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
    /// Returns a read-only view over the row at the specified zero-based index.
    /// </summary>
    /// <param name="index">The zero-based row index to read.</param>
    /// <returns>A row view that exposes values from the selected row.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="index"/> is negative or outside the DataFrame row range.
    /// </exception>
    public DataFrameRow GetRow(int index)
    {
        if (index < 0 || index >= rowCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                index,
                $"Row index must be between 0 and {rowCount - 1}, but the DataFrame contains {rowCount} rows.");
        }

        return new DataFrameRow(this, index);
    }

    /// <summary>
    /// Creates a DataFrame from public readable properties on an object.
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
    /// Creates a DataFrame from an object while validating against an expected schema.
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
    /// Creates an independent DataFrame branch with the same schema and values as the current instance.
    /// </summary>
    /// <returns>
    /// A new DataFrame containing the same column order, column names, data types, nullability
    /// metadata, row order, and cell values as the current DataFrame.
    /// </returns>
    /// <remarks>
    /// The returned DataFrame owns new column snapshots, so mutating the copy with
    /// <see cref="AddColumn{T}(string, IEnumerable{T})"/>, <see cref="RemoveColumn(string)"/>,
    /// <see cref="RenameColumn(string, string)"/>, or <see cref="AddRow(object)"/> does not
    /// mutate the original instance. Mutating the original after copying likewise does not mutate
    /// the copy. Use this method when immutable-style workflows need an explicit branch before
    /// applying direct mutations.
    /// </remarks>
    public DataFrame Copy()
    {
        var copiedColumns = columns
            .Select(CloneSeries)
            .ToArray();

        return new DataFrame(schema, Array.AsReadOnly(copiedColumns));
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
    /// Returns a new DataFrame containing the first specified number of rows while preserving the current schema.
    /// </summary>
    /// <param name="count">The maximum number of rows to include.</param>
    /// <returns>
    /// A new DataFrame with up to <paramref name="count"/> rows from the beginning of the current DataFrame.
    /// The current DataFrame is not modified.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="count"/> is negative.
    /// </exception>
    public DataFrame Head(int count)
    {
        return TakeLeadingRows(count);
    }

    /// <summary>
    /// Returns a new DataFrame containing the first specified number of rows while preserving the current schema.
    /// </summary>
    /// <param name="count">The maximum number of rows to include.</param>
    /// <returns>
    /// A new DataFrame with up to <paramref name="count"/> rows from the beginning of the current DataFrame.
    /// The current DataFrame is not modified.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="count"/> is negative.
    /// </exception>
    public DataFrame Take(int count)
    {
        return TakeLeadingRows(count);
    }

    /// <summary>
    /// Filters rows into a new immutable DataFrame using a read-only row predicate.
    /// </summary>
    /// <param name="predicate">
    /// The predicate evaluated once per row. Rows whose predicate returns <see langword="true"/>
    /// are copied into the returned DataFrame.
    /// </param>
    /// <returns>
    /// A new DataFrame containing matching rows, with the same schema, column order, and row
    /// order as the source DataFrame. The source DataFrame is not modified.
    /// </returns>
    /// <remarks>
    /// Predicate evaluation uses <see cref="DataFrameFilterRow"/> views so direct
    /// <see cref="DataFrameRow"/> access can keep returning raw object values. Missing columns,
    /// invalid column names, unsupported comparisons, and exceptions thrown by the predicate are
    /// not swallowed; they fail the filter operation immediately.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="predicate"/> is <see langword="null"/>.
    /// </exception>
    public DataFrame Filter(Func<DataFrameFilterRow, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var matchingRowIndexes = new List<int>();
        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            if (predicate(new DataFrameFilterRow(this, rowIndex)))
            {
                matchingRowIndexes.Add(rowIndex);
            }
        }

        var filteredColumns = columns
            .Select(column => CreateFilteredSeries(column, matchingRowIndexes))
            .ToArray();

        return new DataFrame(schema, Array.AsReadOnly(filteredColumns));
    }

    /// <summary>
    /// Renames one column on the current DataFrame instance.
    /// </summary>
    /// <param name="currentName">The existing column name to rename, matched case-insensitively.</param>
    /// <param name="newName">The canonical column name to store after the rename.</param>
    /// <remarks>
    /// This method mutates the current DataFrame while preserving row count, column count, column
    /// order, values, data types, and nullability metadata. Missing source columns are rejected,
    /// and target names that conflict with another existing column are rejected using
    /// case-insensitive comparison. Renaming only the casing of the same column is allowed and
    /// updates the canonical name.
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
    public void RenameColumn(string currentName, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        var sourceColumn = GetColumn(currentName);
        if (columnsByName.TryGetValue(newName, out var conflictingColumn) && !ReferenceEquals(sourceColumn, conflictingColumn))
        {
            throw new ArgumentException($"Column '{newName}' conflicts with an existing DataFrame column.", nameof(newName));
        }

        var renamedColumns = columns
            .Select(column => ReferenceEquals(column, sourceColumn) ? CreateSeriesWithName(newName, column) : column)
            .ToArray();

        ReplaceState(renamedColumns);
    }

    /// <summary>
    /// Adds a column to the current DataFrame instance.
    /// </summary>
    /// <typeparam name="T">The CLR type of each value in the new column.</typeparam>
    /// <param name="name">The canonical name of the column to append.</param>
    /// <param name="values">The values to snapshot into the new column.</param>
    /// <remarks>
    /// This method mutates the current DataFrame by appending a new read-only snapshot column and
    /// updating the schema. Existing columns, row order, values, data types, and nullability
    /// metadata are preserved. Validation fails fast before mutation when the column name is
    /// invalid, conflicts with an existing column, is backed by a string instead of a value
    /// collection, or has a value count that differs from <see cref="RowCount"/>.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or whitespace, when the name conflicts with
    /// an existing column, when <paramref name="values"/> is a string, or when the value count
    /// does not match <see cref="RowCount"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> or <paramref name="values"/> is <see langword="null"/>.
    /// </exception>
    public void AddColumn<T>(string name, IEnumerable<T> values)
    {
        var column = CreateAddedColumn(name, values);
        var addedColumns = columns.Concat([column]).ToArray();

        ReplaceState(addedColumns);
    }

    /// <summary>
    /// Appends one row to the current DataFrame instance.
    /// </summary>
    /// <param name="row">
    /// An anonymous or simple object whose public readable properties exactly match the current
    /// DataFrame columns by name.
    /// </param>
    /// <remarks>
    /// This method mutates the current DataFrame by appending the row at the end while preserving
    /// the existing schema, column order, column types, nullability metadata, and existing row
    /// order. Validation fails fast when <paramref name="row"/> is <see langword="null"/>, when
    /// any existing column is missing, when extra properties are supplied, or when a value is not
    /// compatible with the target column type and nullability contract.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the row does not exactly match the DataFrame schema or contains incompatible values.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="row"/> is <see langword="null"/>.
    /// </exception>
    public void AddRow(object row)
    {
        var rowValues = CreateValidatedRowValues(row);
        var appendedColumns = columns
            .Select(column => CreateAppendedSeries(column, rowValues[column.Name]))
            .ToArray();

        columns = Array.AsReadOnly(appendedColumns);
        rowCount = appendedColumns.Length == 0 ? 0 : appendedColumns[0].Count;
        columnsByName = CreateColumnLookup(appendedColumns);
    }

    /// <summary>
    /// Removes a column from the current DataFrame instance.
    /// </summary>
    /// <param name="columnName">The name of the column to remove, matched case-insensitively.</param>
    /// <remarks>
    /// This method mutates the current DataFrame and updates the schema after removing the
    /// requested column. Missing columns are rejected instead of ignored. Removing the final
    /// remaining column is rejected because a DataFrame schema must contain at least one column.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or whitespace, or when removing the
    /// column would leave the DataFrame without any columns.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when <paramref name="columnName"/> does not match an existing column.
    /// </exception>
    public void RemoveColumn(string columnName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

        if (!columnsByName.ContainsKey(columnName))
        {
            throw new KeyNotFoundException($"Column '{columnName}' does not exist in the DataFrame.");
        }

        if (columnCount == 1)
        {
            throw new ArgumentException(
                "Removing the last remaining column is not supported because a DataFrame schema must contain at least one column.",
                nameof(columnName));
        }

        var remainingColumns = columns
            .Where(column => !StringComparer.OrdinalIgnoreCase.Equals(column.Name, columnName))
            .ToArray();

        ReplaceState(remainingColumns);
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

    /// <summary>
    /// Determines whether a column exists and stores values with the requested CLR type.
    /// </summary>
    /// <typeparam name="T">The expected CLR type of the column values.</typeparam>
    /// <param name="columnName">The column name to check using case-insensitive lookup.</param>
    /// <returns>
    /// <see langword="true"/> when a matching column exists with data type <typeparamref name="T"/>;
    /// <see langword="false"/> when the column is missing or has a different data type.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or contains only whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    public bool HasColumnType<T>(string columnName)
    {
        return HasColumnType(columnName, typeof(T));
    }

    /// <summary>
    /// Determines whether a column exists and stores values with the requested CLR type.
    /// </summary>
    /// <param name="columnName">The column name to check using case-insensitive lookup.</param>
    /// <param name="dataType">The expected CLR type of the column values.</param>
    /// <returns>
    /// <see langword="true"/> when a matching column exists with the supplied
    /// <paramref name="dataType"/>; <see langword="false"/> when the column is missing or has a
    /// different data type.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or contains only whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> or <paramref name="dataType"/> is
    /// <see langword="null"/>.
    /// </exception>
    public bool HasColumnType(string columnName, Type dataType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
        ArgumentNullException.ThrowIfNull(dataType);

        return columnsByName.TryGetValue(columnName, out var column) && column.DataType == dataType;
    }

    /// <summary>
    /// Returns a required column or throws when the DataFrame does not contain it.
    /// </summary>
    /// <param name="columnName">The column name to find using case-insensitive lookup.</param>
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
    public ISeries RequireColumn(string columnName)
    {
        return GetColumn(columnName);
    }

    /// <summary>
    /// Returns a required column after validating that its data type matches the requested CLR type.
    /// </summary>
    /// <typeparam name="T">The expected CLR type of the column values.</typeparam>
    /// <param name="columnName">The column name to find using case-insensitive lookup.</param>
    /// <returns>The matching column when it exists and has data type <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or contains only whitespace, or when the
    /// matching column exists but has a different data type.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no matching column exists.
    /// </exception>
    public ISeries RequireColumn<T>(string columnName)
    {
        var column = RequireColumn(columnName);
        var expectedType = typeof(T);
        if (column.DataType != expectedType)
        {
            throw new ArgumentException(
                $"Column '{column.Name}' has data type '{column.DataType}' but expected '{expectedType}'.",
                nameof(columnName));
        }

        return column;
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

    private static IReadOnlyDictionary<string, ISeries> CreateColumnLookup(IEnumerable<ISeries> columns)
    {
        return columns.ToDictionary(static column => column.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static ColumnSchema[] CreateSchemaColumns(IReadOnlyList<ISeries> columns)
    {
        return columns
            .Select((column, ordinal) => ColumnSchema.Create(column.Name, column.DataType, column.IsNullable, ordinal))
            .ToArray();
    }

    private ISeries CreateAddedColumn<T>(string name, IEnumerable<T> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(values);

        if (values is string)
        {
            throw new ArgumentException($"Column '{name}' must be an enumerable value collection, not a string.", nameof(values));
        }

        if (columnsByName.ContainsKey(name))
        {
            throw new ArgumentException($"Column '{name}' conflicts with an existing DataFrame column.", nameof(name));
        }

        var column = Series<T>.Create(name, values);
        if (column.Count != rowCount)
        {
            throw new ArgumentException(
                $"Column '{name}' contains {column.Count} values, but the DataFrame expects {rowCount} values.",
                nameof(values));
        }

        return column;
    }

    private void ReplaceState(ISeries[] updatedColumns)
    {
        // All DataFrame mutations flow through a complete state replacement so public
        // Columns, Schema, counts, and case-insensitive lookup stay in sync.
        var updatedSchema = DataFrameSchema.Create(CreateSchemaColumns(updatedColumns));
        schema = updatedSchema;
        columns = Array.AsReadOnly(updatedColumns);
        columnCount = updatedColumns.Length;
        rowCount = updatedColumns.Length == 0 ? 0 : updatedColumns[0].Count;
        columnsByName = CreateColumnLookup(updatedColumns);
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

    private static ISeries CloneSeries(ISeries column)
    {
        var values = Array.CreateInstance(column.DataType, column.Count);
        for (var index = 0; index < column.Count; index++)
        {
            values.SetValue(column.GetValue(index), index);
        }

        var genericCreateMethod = CreateSeriesMethod.MakeGenericMethod(column.DataType);
        return (ISeries)genericCreateMethod.Invoke(null, [column.Name, values])!;
    }

    private static ISeries CreateFilteredSeries(ISeries column, IReadOnlyList<int> rowIndexes)
    {
        var values = Array.CreateInstance(column.DataType, rowIndexes.Count);
        for (var index = 0; index < rowIndexes.Count; index++)
        {
            values.SetValue(column.GetValue(rowIndexes[index]), index);
        }

        var genericCreateMethod = CreateSeriesMethod.MakeGenericMethod(column.DataType);
        return (ISeries)genericCreateMethod.Invoke(null, [column.Name, values])!;
    }

    private static ISeries CreateAppendedSeries(ISeries column, object? appendedValue)
    {
        var values = Array.CreateInstance(column.DataType, column.Count + 1);
        for (var index = 0; index < column.Count; index++)
        {
            values.SetValue(column.GetValue(index), index);
        }

        values.SetValue(appendedValue, column.Count);

        var genericCreateMethod = CreateSeriesMethod.MakeGenericMethod(column.DataType);
        return (ISeries)genericCreateMethod.Invoke(null, [column.Name, values])!;
    }

    private IReadOnlyDictionary<string, object?> CreateValidatedRowValues(object row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var properties = row.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetMethod is not null && property.GetMethod.GetParameters().Length == 0)
            .ToArray();

        var valuesByName = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in properties)
        {
            if (!valuesByName.TryAdd(property.Name, property.GetValue(row)))
            {
                throw new ArgumentException($"Row property '{property.Name}' was supplied more than once using case-insensitive comparison.", nameof(row));
            }
        }

        foreach (var propertyName in valuesByName.Keys)
        {
            if (!columnsByName.ContainsKey(propertyName))
            {
                throw new ArgumentException($"Row property '{propertyName}' does not exist in the DataFrame schema.", nameof(row));
            }
        }

        foreach (var column in columns)
        {
            if (!valuesByName.TryGetValue(column.Name, out var value))
            {
                throw new ArgumentException($"Row is missing required DataFrame column '{column.Name}'.", nameof(row));
            }

            ValidateRowValue(column, value);
        }

        return valuesByName;
    }

    private static void ValidateRowValue(ISeries column, object? value)
    {
        if (value is null)
        {
            if (!column.IsNullable)
            {
                throw new ArgumentException($"Row value for column '{column.Name}' is null, but the column does not allow null values.");
            }

            return;
        }

        var targetType = Nullable.GetUnderlyingType(column.DataType) ?? column.DataType;
        if (!targetType.IsInstanceOfType(value))
        {
            throw new ArgumentException(
                $"Row value for column '{column.Name}' has type '{value.GetType()}' but the column expects '{column.DataType}'.");
        }
    }

    private DataFrame TakeLeadingRows(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Row limit count must be zero or greater.");
        }

        var resultRowCount = Math.Min(count, rowCount);
        var limitedColumns = columns
            .Select(column => CreateLeadingSeries(column, resultRowCount))
            .ToArray();

        return new DataFrame(schema, Array.AsReadOnly(limitedColumns));
    }

    private static ISeries CreateLeadingSeries(ISeries column, int count)
    {
        var values = Array.CreateInstance(column.DataType, count);
        for (var index = 0; index < count; index++)
        {
            values.SetValue(column.GetValue(index), index);
        }

        var genericCreateMethod = CreateSeriesMethod.MakeGenericMethod(column.DataType);
        return (ISeries)genericCreateMethod.Invoke(null, [column.Name, values])!;
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
