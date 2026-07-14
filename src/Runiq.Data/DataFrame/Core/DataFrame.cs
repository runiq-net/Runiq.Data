using System.Collections;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Reflection;
using Runiq.Data.IO;
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
/// <see cref="Drop(string[])"/> return new DataFrame instances. Mutation operations accessed
/// through <see cref="Columns"/> and <see cref="Rows"/> update the current instance. Call
/// <see cref="Copy"/> first when a separate mutable branch is needed.
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
        Columns = new ColumnOperations(this);
        Rows = new RowOperations(this);
    }

    /// <summary>
    /// Gets the schema inferred from the DataFrame columns or supplied during schema-first creation.
    /// </summary>
    public DataFrameSchema Schema => schema;

    /// <summary>
    /// Gets the column operation facade for mutating columns on the current DataFrame instance.
    /// </summary>
    /// <remarks>
    /// Operations exposed through this facade append, remove, or rename columns in place while
    /// preserving row order and validating names, conflicts, and value counts before mutation.
    /// Call <see cref="Copy"/> first when a separate mutable branch is needed.
    /// </remarks>
    public ColumnOperations Columns { get; }

    /// <summary>
    /// Gets the row operation facade for mutating rows on the current DataFrame instance.
    /// </summary>
    /// <remarks>
    /// Operations exposed through this facade append, replace, or remove rows in place while
    /// preserving the DataFrame schema and column order. Call <see cref="Copy"/> first when a
    /// separate mutable branch is needed.
    /// </remarks>
    public RowOperations Rows { get; }

    internal IReadOnlyList<ISeries> ColumnSeries => columns;

    internal int RowTotalCore => rowCount;

    internal int ColumnTotalCore => columnCount;

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
    public Row GetRow(int index)
    {
        if (index < 0 || index >= rowCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                index,
                $"Row index must be between 0 and {rowCount - 1}, but the DataFrame contains {rowCount} rows.");
        }

        return new Row(this, index);
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

        ValidateRowTotals(orderedColumns);

        return new DataFrame(schema, Array.AsReadOnly(orderedColumns));
    }

    /// <summary>
    /// Reads a comma-delimited CSV file into a new DataFrame using inferred header behavior.
    /// </summary>
    /// <param name="path">The local file path to read.</param>
    /// <returns>
    /// A DataFrame whose column order follows the CSV header and whose column types are inferred
    /// from all non-missing values in each column.
    /// </returns>
    /// <remarks>
    /// This overload uses <see cref="CsvReadOptions"/> defaults: comma delimiter and
    /// <see cref="CsvHeaderMode.Infer"/>. With no explicit names, the first row is consumed as
    /// the header. Empty unquoted cells become missing values, quoted empty cells remain empty
    /// strings, and quoted delimiters, escaped quotes, and multiline quoted fields are supported.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="path"/> is empty or whitespace, CSV structure is malformed,
    /// header names are invalid, row field counts differ, or no usable columns can be produced.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="path"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown by the underlying file system when the file cannot be read.
    /// </exception>
    public static DataFrame ReadCsv(string path)
    {
        return ReadCsv(path, new CsvReadOptions());
    }

    /// <summary>
    /// Reads a CSV file into a new DataFrame using explicit CSV parsing options.
    /// </summary>
    /// <param name="path">The local file path to read.</param>
    /// <param name="options">
    /// The CSV options controlling delimiter, header mode, and optional column names. When
    /// <see cref="CsvReadOptions.Header"/> is <see cref="CsvHeaderMode.Infer"/>, names being
    /// supplied means the first row is treated as data; otherwise the first row is treated as a
    /// header. With <see cref="CsvHeaderMode.Present"/>, the first row is always consumed as a
    /// header and explicit names replace it. With <see cref="CsvHeaderMode.Absent"/>, the first
    /// row is always data and missing names are generated as Column1, Column2, and so on.
    /// </param>
    /// <returns>
    /// A DataFrame whose column order follows the resolved names and whose column types are
    /// inferred from all non-missing values in each column.
    /// </returns>
    /// <remarks>
    /// The supplied options object is required. Explicit names are copied and must be non-empty,
    /// unique using DataFrame's case-insensitive column-name semantics, and equal to the actual
    /// CSV column count. Numeric inference uses invariant culture and no date, enum, or Guid
    /// inference is performed.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="path"/> is empty or whitespace, options contain invalid
    /// delimiter or name values, CSV structure is malformed, row field counts differ, or no
    /// usable columns can be produced.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="path"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <see cref="CsvReadOptions.Header"/> contains an undefined enum value.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown by the underlying file system when the file cannot be read.
    /// </exception>
    public static DataFrame ReadCsv(string path, CsvReadOptions options)
    {
        return CsvDataFrameReader.Read(path, options);
    }

    /// <summary>
    /// Reads an Excel <c>.xlsx</c> workbook into a new DataFrame using inferred header behavior.
    /// </summary>
    /// <param name="path">The local workbook path to read.</param>
    /// <returns>
    /// A DataFrame whose column order follows the resolved worksheet header and whose column
    /// types are inferred from every non-blank cell in each column.
    /// </returns>
    /// <remarks>
    /// This overload uses <see cref="ExcelReadOptions"/> defaults: the first worksheet is read
    /// and <see cref="ExcelHeaderMode.Infer"/> is used. With no explicit names, the first used
    /// worksheet row is consumed as the header. The used data range is determined from cells
    /// containing values or formulas rather than formatting-only cells. Formula cells are read
    /// from cached workbook results and are not recalculated.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="path"/> is empty or whitespace, worksheet options are invalid,
    /// header names are invalid, or no usable columns can be produced.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="path"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the file extension identifies a known unsupported Excel format such as
    /// <c>.xls</c>, <c>.xlsb</c>, or <c>.ods</c>.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown by the underlying file system when the workbook cannot be read.
    /// </exception>
    public static DataFrame ReadExcel(string path)
    {
        return ReadExcel(path, new ExcelReadOptions());
    }

    /// <summary>
    /// Reads an Excel <c>.xlsx</c> workbook into a new DataFrame using explicit Excel options.
    /// </summary>
    /// <param name="path">The local workbook path to read.</param>
    /// <param name="options">
    /// The Excel options controlling worksheet selection, header mode, and optional column names.
    /// </param>
    /// <returns>
    /// A DataFrame whose column order follows the resolved names and whose column types are
    /// inferred from every non-blank cell in each column.
    /// </returns>
    /// <remarks>
    /// <see cref="ExcelReadOptions.SheetIndex"/> is zero-based. Supplying both
    /// <see cref="ExcelReadOptions.SheetName"/> and <see cref="ExcelReadOptions.SheetIndex"/>
    /// is invalid. With <see cref="ExcelHeaderMode.Infer"/>, explicit names mean the first used
    /// row is treated as data; otherwise the first used row is treated as a header. With
    /// <see cref="ExcelHeaderMode.Present"/>, the first used row is always consumed as a header
    /// and explicit names replace it. With <see cref="ExcelHeaderMode.Absent"/>, the first used
    /// row is always data and missing names are generated as Column1, Column2, and so on.
    /// Formula cells use cached workbook results and are not recalculated. Date/time cells are
    /// returned as <see cref="DateTime"/> values with <see cref="DateTimeKind.Unspecified"/>.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="path"/> is empty or whitespace, options contain invalid
    /// worksheet selectors or name values, headers are invalid, formula/error cells cannot be
    /// read safely, or no usable columns can be produced.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="path"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <see cref="ExcelReadOptions.Header"/> contains an undefined enum value.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the file extension identifies a known unsupported Excel format such as
    /// <c>.xls</c>, <c>.xlsb</c>, or <c>.ods</c>.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown by the underlying file system when the workbook cannot be read.
    /// </exception>
    public static DataFrame ReadExcel(string path, ExcelReadOptions options)
    {
        return ExcelDataFrameReader.Read(path, options);
    }

    /// <summary>
    /// Reads a JSON array of objects into a new DataFrame.
    /// </summary>
    /// <param name="path">The local JSON file path to read.</param>
    /// <returns>
    /// A DataFrame whose column order follows first property discovery and whose column types
    /// preserve native JSON primitive types.
    /// </returns>
    /// <remarks>
    /// The root JSON value must be a non-empty array and every array item must be an object.
    /// Missing properties become null values. Column order follows the first object's property
    /// order, with properties discovered in later objects appended to the end. Supported values
    /// are null, string, Boolean, integer, decimal, and double. Nested objects and arrays are
    /// rejected. Mixed primitive columns fail unless all non-null values can be represented by a
    /// safe numeric promotion.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="path"/> is empty or whitespace, the JSON root is not a
    /// non-empty array of objects, property names are invalid, nested values are encountered, or
    /// a column contains incompatible primitive types.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="path"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown by the underlying file system when the file cannot be read.
    /// </exception>
    public static DataFrame ReadJson(string path)
    {
        return JsonDataFrameReader.Read(path);
    }

    /// <summary>
    /// Reads the single tabular result set returned by a SQL command text into a new DataFrame.
    /// </summary>
    /// <param name="connection">
    /// The database connection used to create and execute the command. The connection remains
    /// caller-owned and is never disposed by this method.
    /// </param>
    /// <param name="commandText">The non-empty SQL command text to execute.</param>
    /// <returns>
    /// A DataFrame whose column order and row order match the provider result set.
    /// </returns>
    /// <remarks>
    /// If the connection is initially closed, it is opened temporarily and restored to the
    /// closed state after the operation completes, including failure paths. Provider-specific
    /// SQL types are not inferred; supported CLR values returned by <see cref="DbDataReader.GetValue(int)"/>
    /// are preserved.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="commandText"/> is empty or contains only whitespace, or when
    /// the SQL result shape or values cannot be represented as a DataFrame.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="connection"/> or <paramref name="commandText"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is in an unsupported state.
    /// </exception>
    public static DataFrame ReadSql(DbConnection connection, string commandText)
    {
        return SqlDataFrameReader.Read(connection, commandText);
    }

    /// <summary>
    /// Reads the single tabular result set returned by a database command into a new DataFrame.
    /// </summary>
    /// <param name="command">
    /// The database command to execute. The command, its connection, parameters, transaction,
    /// timeout, type, and command text remain caller-owned and are not mutated by this method.
    /// </param>
    /// <returns>
    /// A DataFrame whose column order and row order match the provider result set.
    /// </returns>
    /// <remarks>
    /// If the command connection is initially closed, it is opened temporarily and restored to
    /// the closed state after the operation completes, including failure paths. The returned
    /// reader is always disposed by Runiq.Data.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the command text is empty or whitespace, or when the SQL result shape or
    /// values cannot be represented as a DataFrame.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="command"/> or its command text is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the command has no connection or the connection is in an unsupported state.
    /// </exception>
    public static DataFrame ReadSql(DbCommand command)
    {
        return SqlDataFrameReader.Read(command);
    }

    /// <summary>
    /// Writes the current DataFrame to a JSON file using default write options.
    /// </summary>
    /// <param name="path">The local JSON file path to create or replace.</param>
    /// <remarks>
    /// This overload uses <see cref="JsonWriteOptions"/> defaults and writes indented JSON. The
    /// root value is always an array, each DataFrame row becomes one object, and each DataFrame
    /// column becomes one object property in column order. Null cells become JSON null values.
    /// Strings, including numeric-looking, Boolean-looking, null-looking, date-looking, and
    /// formula-like strings, remain JSON strings. Date/time values are written as invariant ISO
    /// 8601 strings, enum values are written by name, and unsupported nested or custom values
    /// fail before the target file is replaced.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="path"/> is empty or whitespace, or a cell contains a value
    /// that cannot be written safely as a JSON primitive.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="path"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown by the underlying file system when the target directory does not exist.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown by the underlying file system when the file cannot be written, replaced, or moved.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown by the underlying file system when access to the path is denied.
    /// </exception>
    /// <exception cref="PathTooLongException">
    /// Thrown by the underlying file system when the path exceeds platform limits.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown by the underlying file system when the path format is not supported.
    /// </exception>
    public void WriteJson(string path)
    {
        WriteJson(path, new JsonWriteOptions());
    }

    /// <summary>
    /// Writes the current DataFrame to a JSON file using explicit write options.
    /// </summary>
    /// <param name="path">The local JSON file path to create or replace.</param>
    /// <param name="options">The JSON options controlling insignificant whitespace formatting.</param>
    /// <remarks>
    /// Existing files are overwritten rather than appended. The writer manually emits DataFrame
    /// rows and ordered columns as a JSON array of objects instead of serializing DataFrame
    /// internals. Numeric values are written as JSON numbers only when the runtime value is one
    /// of the supported CLR numeric primitives; <see cref="float.NaN"/>,
    /// <see cref="float.PositiveInfinity"/>, <see cref="float.NegativeInfinity"/>,
    /// <see cref="double.NaN"/>, <see cref="double.PositiveInfinity"/>, and
    /// <see cref="double.NegativeInfinity"/> are rejected. The target file is replaced only
    /// after the full JSON payload has been serialized to a temporary file in the target
    /// directory.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="path"/> is empty or whitespace, or a cell contains a value
    /// that cannot be written safely as a JSON primitive.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="path"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown by the underlying file system when the target directory does not exist.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown by the underlying file system when the file cannot be written, replaced, or moved.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown by the underlying file system when access to the path is denied.
    /// </exception>
    /// <exception cref="PathTooLongException">
    /// Thrown by the underlying file system when the path exceeds platform limits.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown by the underlying file system when the path format is not supported.
    /// </exception>
    public void WriteJson(string path, JsonWriteOptions options)
    {
        JsonDataFrameWriter.Write(this, path, options);
    }

    /// <summary>
    /// Writes the current DataFrame to a comma-delimited CSV file using default write options.
    /// </summary>
    /// <param name="path">The local file path to create or replace.</param>
    /// <remarks>
    /// This overload uses <see cref="CsvWriteOptions"/> defaults: a header row is written and
    /// comma is used as the delimiter. Null cells are written as empty unquoted fields, empty
    /// strings are written as quoted empty fields, strings are quoted only when CSV structure
    /// requires it, Booleans are written as <c>true</c> or <c>false</c>, and numeric values use
    /// invariant culture formatting. The file is encoded as UTF-8 without a byte-order mark.
    /// The target file is replaced after content is serialized to a temporary file in the target
    /// directory, so an existing file is preserved when serialization fails before replacement.
    /// CSV does not store DataFrame schema metadata, so reading the file back may infer
    /// compatible but not identical CLR column types.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="path"/> is empty or whitespace, the DataFrame cannot produce
    /// a CSV record shape, or a cell contains a value that cannot be written deterministically.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="path"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown by the underlying file system when the target directory does not exist.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown by the underlying file system when the file cannot be written, replaced, or moved.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown by the underlying file system when access to the path is denied.
    /// </exception>
    /// <exception cref="PathTooLongException">
    /// Thrown by the underlying file system when the path exceeds platform limits.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown by the underlying file system when the path format is not supported.
    /// </exception>
    public void WriteCsv(string path)
    {
        WriteCsv(path, new CsvWriteOptions());
    }

    /// <summary>
    /// Writes the current DataFrame to a CSV file using explicit write options.
    /// </summary>
    /// <param name="path">The local file path to create or replace.</param>
    /// <param name="options">
    /// The CSV options controlling whether a header row is written and which single-character
    /// delimiter separates fields.
    /// </param>
    /// <remarks>
    /// Existing files are overwritten rather than appended. Header names and cell values use the
    /// same CSV escaping rules: fields containing the delimiter, quote, carriage return, or line
    /// feed are quoted, and quote characters inside quoted fields are doubled. Null cells are
    /// written as empty unquoted fields, while empty strings are written as quoted empty fields
    /// to preserve the distinction used by <see cref="ReadCsv(string, CsvReadOptions)"/>.
    /// Booleans are written as <c>true</c> or <c>false</c>, numeric values use invariant culture,
    /// and unsupported runtime values such as custom objects or non-finite floating-point values
    /// fail before the target file is replaced. The writer uses UTF-8 without a byte-order mark
    /// and writes each physical CSV record with <see cref="Environment.NewLine"/>.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="path"/> is empty or whitespace, options contain an invalid
    /// delimiter, the DataFrame cannot produce a CSV record shape, or a cell contains a value
    /// that cannot be written deterministically.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="path"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown by the underlying file system when the target directory does not exist.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown by the underlying file system when the file cannot be written, replaced, or moved.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown by the underlying file system when access to the path is denied.
    /// </exception>
    /// <exception cref="PathTooLongException">
    /// Thrown by the underlying file system when the path exceeds platform limits.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown by the underlying file system when the path format is not supported.
    /// </exception>
    public void WriteCsv(string path, CsvWriteOptions options)
    {
        CsvDataFrameWriter.Write(this, path, options);
    }

    /// <summary>
    /// Writes the current DataFrame to an Excel <c>.xlsx</c> workbook using default write options.
    /// </summary>
    /// <param name="path">The local <c>.xlsx</c> file path to create or replace.</param>
    /// <remarks>
    /// This overload uses <see cref="ExcelWriteOptions"/> defaults: one worksheet named
    /// <c>Sheet1</c> and a header row. Existing files are overwritten rather than appended.
    /// String values, including formula-like strings such as <c>=1+1</c>, are written as text;
    /// null cells are written as blank cells; empty strings remain text cells; Booleans,
    /// supported numeric values, and <see cref="DateTime"/> values are written as native Excel
    /// cells. The target file is replaced only after the workbook has been serialized to a
    /// temporary <c>.xlsx</c> file in the target directory. Excel does not persist
    /// <see cref="DateTimeKind"/> metadata and cannot safely store all CLR numeric values.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="path"/> is empty or whitespace, the DataFrame cannot produce
    /// an Excel worksheet shape, or a cell contains a value that cannot be written safely.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="path"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when <paramref name="path"/> does not have a <c>.xlsx</c> extension or when the
    /// underlying file system reports an unsupported path format.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown by the underlying file system when the target directory does not exist.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown by the underlying file system when the file cannot be written, replaced, or moved.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown by the underlying file system when access to the path is denied.
    /// </exception>
    /// <exception cref="PathTooLongException">
    /// Thrown by the underlying file system when the path exceeds platform limits.
    /// </exception>
    public void WriteExcel(string path)
    {
        WriteExcel(path, new ExcelWriteOptions());
    }

    /// <summary>
    /// Writes the current DataFrame to an Excel <c>.xlsx</c> workbook using explicit write options.
    /// </summary>
    /// <param name="path">The local <c>.xlsx</c> file path to create or replace.</param>
    /// <param name="options">The Excel options controlling worksheet name and header output.</param>
    /// <remarks>
    /// A new workbook with a single worksheet is created for every call. Existing workbook
    /// content is not preserved, and rows or worksheets are not appended. The worksheet name is
    /// used exactly as supplied and is validated before ClosedXML creates the sheet. Unsupported
    /// runtime values such as custom objects, non-finite floating-point values, unsafe integral
    /// values, unsafe decimals, and dates outside Excel's date range fail before the target file
    /// is replaced.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="path"/> is empty or whitespace, options contain an invalid
    /// worksheet name, the DataFrame cannot produce an Excel worksheet shape, or a cell contains
    /// a value that cannot be written safely.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="path"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when <paramref name="path"/> does not have a <c>.xlsx</c> extension or when the
    /// underlying file system reports an unsupported path format.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown by the underlying file system when the target directory does not exist.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown by the underlying file system when the file cannot be written, replaced, or moved.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown by the underlying file system when access to the path is denied.
    /// </exception>
    /// <exception cref="PathTooLongException">
    /// Thrown by the underlying file system when the path exceeds platform limits.
    /// </exception>
    public void WriteExcel(string path, ExcelWriteOptions options)
    {
        ExcelDataFrameWriter.Write(this, path, options);
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
    /// operations accessed through <see cref="Columns"/> or <see cref="Rows"/> does not mutate
    /// the original instance. Mutating the original after
    /// copying likewise does not mutate the copy. Use this method when immutable-style workflows
    /// need an explicit branch before applying direct mutations.
    /// </remarks>
    public DataFrame Copy()
    {
        var copiedColumns = columns
            .Select(CloneSeries)
            .ToArray();

        return new DataFrame(schema, Array.AsReadOnly(copiedColumns));
    }

    /// <summary>
    /// Starts an inner join with another DataFrame.
    /// </summary>
    /// <param name="right">The right DataFrame to join with the current left DataFrame.</param>
    /// <returns>
    /// A join condition builder. Calling one of its <c>On</c> overloads executes the inner join
    /// and returns a new DataFrame without mutating either source.
    /// </returns>
    /// <remarks>
    /// Inner joins return only rows whose key exists on both sides. Same-name keys can be
    /// supplied with <c>On("Id")</c> or as composite keys with <c>On(["CompanyId", "OrderId"])</c>.
    /// Different key names can be supplied with <c>On("LeftId", "RightId")</c> or tuple-based
    /// composite key pairs.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="right"/> is null.</exception>
    public DataFrameJoin InnerJoin(DataFrame right)
    {
        ArgumentNullException.ThrowIfNull(right);
        return new DataFrameJoin(this, right, JoinKind.Inner);
    }

    /// <summary>
    /// Starts a left join with another DataFrame.
    /// </summary>
    /// <param name="right">The right DataFrame to join with the current left DataFrame.</param>
    /// <returns>
    /// A join condition builder. Calling one of its <c>On</c> overloads executes the left join
    /// and returns a new DataFrame without mutating either source.
    /// </returns>
    /// <remarks>
    /// Left joins return every left row and fill right-side result columns with null when no
    /// right row matches. Same-name keys can be supplied with <c>On("Id")</c> or as composite
    /// keys with <c>On(["CompanyId", "OrderId"])</c>. Different key names can be supplied with
    /// <c>On("LeftId", "RightId")</c> or tuple-based composite key pairs.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="right"/> is null.</exception>
    public DataFrameJoin LeftJoin(DataFrame right)
    {
        ArgumentNullException.ThrowIfNull(right);
        return new DataFrameJoin(this, right, JoinKind.Left);
    }

    /// <summary>
    /// Starts a right join with another DataFrame.
    /// </summary>
    /// <param name="right">The right DataFrame to join with the current left DataFrame.</param>
    /// <returns>
    /// A join condition builder. Calling one of its <c>On</c> overloads executes the right join
    /// and returns a new DataFrame without mutating either source.
    /// </returns>
    /// <remarks>
    /// Right joins return every right row and fill left-side result columns with null when no
    /// left row matches. Same-name keys can be supplied with <c>On("Id")</c> or as composite
    /// keys with <c>On(["CompanyId", "OrderId"])</c>. Different key names can be supplied with
    /// <c>On("LeftId", "RightId")</c> or tuple-based composite key pairs.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="right"/> is null.</exception>
    public DataFrameJoin RightJoin(DataFrame right)
    {
        ArgumentNullException.ThrowIfNull(right);
        return new DataFrameJoin(this, right, JoinKind.Right);
    }

    /// <summary>
    /// Starts a full join with another DataFrame.
    /// </summary>
    /// <param name="right">The right DataFrame to join with the current left DataFrame.</param>
    /// <returns>
    /// A join condition builder. Calling one of its <c>On</c> overloads executes the full join
    /// and returns a new DataFrame without mutating either source.
    /// </returns>
    /// <remarks>
    /// Full joins return every row from both sides, preserving left-join ordering first and then
    /// appending unmatched right rows in right source order. Same-name keys can be supplied with
    /// <c>On("Id")</c> or as composite keys with <c>On(["CompanyId", "OrderId"])</c>. Different
    /// key names can be supplied with <c>On("LeftId", "RightId")</c> or tuple-based composite
    /// key pairs.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="right"/> is null.</exception>
    public DataFrameJoin FullJoin(DataFrame right)
    {
        ArgumentNullException.ThrowIfNull(right);
        return new DataFrameJoin(this, right, JoinKind.Full);
    }

    /// <summary>
    /// Sums the non-null numeric values in the specified column without mutating the source DataFrame.
    /// </summary>
    /// <param name="columnName">The numeric column name to aggregate using case-insensitive lookup.</param>
    /// <returns>
    /// The sum using the column numeric type contract: small signed and unsigned integers return
    /// <see cref="int"/>, <see cref="uint"/> returns <see cref="uint"/>, <see cref="long"/>
    /// returns <see cref="long"/>, <see cref="ulong"/> returns <see cref="ulong"/>,
    /// <see cref="float"/> returns <see cref="float"/>, <see cref="double"/> returns
    /// <see cref="double"/>, and <see cref="decimal"/> returns <see cref="decimal"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or whitespace, the column is empty,
    /// contains null values, contains values incompatible with its declared type, or is not numeric.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when no matching column exists.</exception>
    /// <exception cref="OverflowException">
    /// Thrown when checked integer or decimal addition overflows.
    /// </exception>
    public object Sum(string columnName)
    {
        var column = GetColumn(columnName);
        ValidateAggregationColumnHasRows(column);
        return SumColumn(column, null);
    }

    /// <summary>
    /// Averages the non-null numeric values in the specified column as a <see cref="double"/> without mutating the source DataFrame.
    /// </summary>
    /// <param name="columnName">The numeric column name to aggregate using case-insensitive lookup.</param>
    /// <returns>The arithmetic average of the column values as a <see cref="double"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or whitespace, the column is empty,
    /// contains null values, contains values incompatible with its declared type, or is not numeric.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when no matching column exists.</exception>
    /// <exception cref="OverflowException">
    /// Thrown when checked integer or decimal addition overflows before division.
    /// </exception>
    public double Average(string columnName)
    {
        var column = GetColumn(columnName);
        ValidateAggregationColumnHasRows(column);
        return AverageColumn(column, null);
    }

    /// <summary>
    /// Returns the first minimum non-null value in the specified comparable column without mutating the source DataFrame.
    /// </summary>
    /// <param name="columnName">The comparable column name to aggregate using case-insensitive lookup.</param>
    /// <returns>The first minimum value in the column.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or whitespace, the column is empty,
    /// contains null values, contains values incompatible with its declared type, or contains
    /// values that cannot be compared.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when no matching column exists.</exception>
    public object? Min(string columnName)
    {
        var column = GetColumn(columnName);
        ValidateAggregationColumnHasRows(column);
        return MinOrMaxColumn(column, null, findMaximum: false);
    }

    /// <summary>
    /// Returns the first maximum non-null value in the specified comparable column without mutating the source DataFrame.
    /// </summary>
    /// <param name="columnName">The comparable column name to aggregate using case-insensitive lookup.</param>
    /// <returns>The first maximum value in the column.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or whitespace, the column is empty,
    /// contains null values, contains values incompatible with its declared type, or contains
    /// values that cannot be compared.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when no matching column exists.</exception>
    public object? Max(string columnName)
    {
        var column = GetColumn(columnName);
        ValidateAggregationColumnHasRows(column);
        return MinOrMaxColumn(column, null, findMaximum: true);
    }

    /// <summary>
    /// Creates a reusable grouping snapshot over one or more key columns without mutating the source DataFrame.
    /// </summary>
    /// <param name="columnNames">The group key column names, in the order they should appear in grouped results.</param>
    /// <returns>
    /// A grouped DataFrame snapshot that preserves first-seen group ordering and can run grouped
    /// aggregations independently of later source DataFrame mutations.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when no key columns are supplied, a key name is empty or whitespace, or the same
    /// key column is supplied more than once using case-insensitive comparison.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnNames"/> or one of its entries is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when a requested key column does not exist.</exception>
    public GroupedDataFrame GroupBy(params string[] columnNames)
    {
        ArgumentNullException.ThrowIfNull(columnNames);

        if (columnNames.Length == 0)
        {
            throw new ArgumentException("At least one group key column must be supplied.", nameof(columnNames));
        }

        var selectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolvedNames = new string[columnNames.Length];
        for (var index = 0; index < columnNames.Length; index++)
        {
            var columnName = columnNames[index];
            ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

            if (!selectedNames.Add(columnName))
            {
                throw new ArgumentException($"Column '{columnName}' was selected as a group key more than once.", nameof(columnNames));
            }

            resolvedNames[index] = GetColumn(columnName).Name;
        }

        return new GroupedDataFrame(Copy(), resolvedNames);
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

        if (droppedNames.Count == columnCount)
        {
            throw new ArgumentException("Dropping all columns is not supported because a DataFrame schema must contain at least one column.", nameof(columnNames));
        }

        var remainingColumns = columns
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
    /// Returns a new DataFrame with duplicate rows removed while preserving the source schema.
    /// </summary>
    /// <param name="columnNames">
    /// Optional column names to use as the duplicate key. When no columns are supplied, all
    /// columns are used together as the key.
    /// </param>
    /// <returns>
    /// A new DataFrame containing the first row seen for each duplicate key, with the same
    /// schema, column order, and stable row order as the source DataFrame. The source DataFrame
    /// is not modified.
    /// </returns>
    /// <remarks>
    /// When one or more column names are supplied, only those columns determine duplicate keys,
    /// but every source column is copied into the result. Value comparison uses the current
    /// runtime equality behavior for the stored cell values.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when a column name is empty or whitespace, or when a column name is supplied more
    /// than once using case-insensitive comparison.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnNames"/> or one of its entries is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when a requested key column does not exist.
    /// </exception>
    public DataFrame Distinct(params string[] columnNames)
    {
        var keyColumns = ResolveDistinctKeyColumns(columnNames);
        var seenKeys = new HashSet<object?[]>(RowKeyComparer.Instance);
        var distinctRowIndexes = new List<int>();

        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var key = new object?[keyColumns.Length];
            for (var columnIndex = 0; columnIndex < keyColumns.Length; columnIndex++)
            {
                key[columnIndex] = keyColumns[columnIndex].GetValue(rowIndex);
            }

            if (seenKeys.Add(key))
            {
                distinctRowIndexes.Add(rowIndex);
            }
        }

        var distinctColumns = columns
            .Select(column => CreateFilteredSeries(column, distinctRowIndexes))
            .ToArray();

        return new DataFrame(schema, Array.AsReadOnly(distinctColumns));
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
    /// Predicate evaluation uses <see cref="FilterRow"/> views so direct
    /// <see cref="Row"/> access can keep returning raw object values. Missing columns,
    /// invalid column names, unsupported comparisons, and exceptions thrown by the predicate are
    /// not swallowed; they fail the filter operation immediately.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="predicate"/> is <see langword="null"/>.
    /// </exception>
    public DataFrame Filter(Func<FilterRow, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var matchingRowIndexes = new List<int>();
        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            if (predicate(new FilterRow(this, rowIndex)))
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
    internal void RenameColumnCore(string currentName, string newName)
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
    /// collection, or has a value count that differs from the current row count.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or whitespace, when the name conflicts with
    /// an existing column, when <paramref name="values"/> is a string, or when the value count
    /// does not match the current row count.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> or <paramref name="values"/> is <see langword="null"/>.
    /// </exception>
    internal void AddColumnCore<T>(string name, IEnumerable<T> values)
    {
        var column = CreateAddedColumn(name, values);
        var addedColumns = columns.Concat([column]).ToArray();

        ReplaceState(addedColumns);
    }

    internal void AppendRowCore(object row)
    {
        var rowValues = CreateValidatedRowValues(row);
        var appendedColumns = columns
            .Select(column => CreateAppendedSeries(column, rowValues[column.Name]))
            .ToArray();

        columns = Array.AsReadOnly(appendedColumns);
        rowCount = appendedColumns.Length == 0 ? 0 : appendedColumns[0].Count;
        columnsByName = CreateColumnLookup(appendedColumns);
    }

    internal void ReplaceRowCore(int index, object row)
    {
        ValidateRowIndex(index);

        var rowValues = CreateValidatedRowValues(row);
        var updatedColumns = columns
            .Select(column => CreateUpdatedSeries(column, index, rowValues[column.Name]))
            .ToArray();

        columns = Array.AsReadOnly(updatedColumns);
        rowCount = updatedColumns.Length == 0 ? 0 : updatedColumns[0].Count;
        columnsByName = CreateColumnLookup(updatedColumns);
    }

    internal void DeleteRowCore(int index)
    {
        ValidateRowIndex(index);

        var updatedColumns = columns
            .Select(column => CreateRemovedSeries(column, index))
            .ToArray();

        columns = Array.AsReadOnly(updatedColumns);
        rowCount = updatedColumns.Length == 0 ? 0 : updatedColumns[0].Count;
        columnsByName = CreateColumnLookup(updatedColumns);
    }

    internal void SortRowsCore(string columnName, bool descending)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

        var sortColumn = GetColumn(columnName);
        ValidateSortableColumn(sortColumn);

        var sortedIndexes = Enumerable.Range(0, rowCount).ToArray();
        Array.Sort(sortedIndexes, (leftIndex, rightIndex) =>
        {
            var comparison = CompareSortValues(sortColumn, leftIndex, rightIndex);
            return descending ? -comparison : comparison;
        });

        var sortedColumns = columns
            .Select(column => CreateReorderedSeries(column, sortedIndexes))
            .ToArray();

        columns = Array.AsReadOnly(sortedColumns);
        rowCount = sortedColumns.Length == 0 ? 0 : sortedColumns[0].Count;
        columnsByName = CreateColumnLookup(sortedColumns);
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
    internal void RemoveColumnCore(string columnName)
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

        ValidateRowTotals(series);

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

    internal static ISeries CreateSeriesFromValues(string name, Type dataType, IEnumerable<object?> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(dataType);
        ArgumentNullException.ThrowIfNull(values);

        var snapshot = values.ToArray();
        var array = Array.CreateInstance(dataType, snapshot.Length);
        var index = 0;
        foreach (var value in snapshot)
        {
            array.SetValue(value, index);
            index++;
        }

        var genericCreateMethod = CreateSeriesMethod.MakeGenericMethod(dataType);
        return (ISeries)genericCreateMethod.Invoke(null, [name, array])!;
    }

    internal static DataFrame CreateFromSeries(IReadOnlyList<ISeries> sourceColumns)
    {
        ArgumentNullException.ThrowIfNull(sourceColumns);

        if (sourceColumns.Count == 0)
        {
            throw new ArgumentException("A DataFrame requires at least one column.", nameof(sourceColumns));
        }

        var copiedColumns = sourceColumns
            .Select(CloneSeries)
            .ToArray();
        ValidateRowTotals(copiedColumns);

        var schema = DataFrameSchema.Create(CreateSchemaColumns(copiedColumns));
        return new DataFrame(schema, Array.AsReadOnly(copiedColumns));
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

    private static void ValidateAggregationColumnHasRows(ISeries column)
    {
        if (column.Count == 0)
        {
            throw new ArgumentException($"Column '{column.Name}' contains no values to aggregate.");
        }
    }

    internal static object SumColumn(ISeries column, IReadOnlyList<int>? rowIndexes)
    {
        var numericType = GetNumericAggregationType(column);
        var rowCount = GetAggregationRowCount(column, rowIndexes);

        if (numericType == typeof(byte) || numericType == typeof(sbyte) || numericType == typeof(short) ||
            numericType == typeof(ushort) || numericType == typeof(int))
        {
            var sum = 0;
            for (var index = 0; index < rowCount; index++)
            {
                sum = checked(sum + GetInt32AggregationValue(column, numericType, GetAggregationRowIndex(rowIndexes, index)));
            }

            return sum;
        }

        if (numericType == typeof(uint))
        {
            var sum = 0u;
            for (var index = 0; index < rowCount; index++)
            {
                sum = checked(sum + GetAggregationValue<uint>(column, GetAggregationRowIndex(rowIndexes, index)));
            }

            return sum;
        }

        if (numericType == typeof(long))
        {
            var sum = 0L;
            for (var index = 0; index < rowCount; index++)
            {
                sum = checked(sum + GetAggregationValue<long>(column, GetAggregationRowIndex(rowIndexes, index)));
            }

            return sum;
        }

        if (numericType == typeof(ulong))
        {
            var sum = 0UL;
            for (var index = 0; index < rowCount; index++)
            {
                sum = checked(sum + GetAggregationValue<ulong>(column, GetAggregationRowIndex(rowIndexes, index)));
            }

            return sum;
        }

        if (numericType == typeof(float))
        {
            var sum = 0f;
            for (var index = 0; index < rowCount; index++)
            {
                sum += GetAggregationValue<float>(column, GetAggregationRowIndex(rowIndexes, index));
            }

            return sum;
        }

        if (numericType == typeof(double))
        {
            var sum = 0d;
            for (var index = 0; index < rowCount; index++)
            {
                sum += GetAggregationValue<double>(column, GetAggregationRowIndex(rowIndexes, index));
            }

            return sum;
        }

        if (numericType == typeof(decimal))
        {
            var sum = 0m;
            for (var index = 0; index < rowCount; index++)
            {
                sum = checked(sum + GetAggregationValue<decimal>(column, GetAggregationRowIndex(rowIndexes, index)));
            }

            return sum;
        }

        throw new ArgumentException($"Column '{column.Name}' has data type '{column.DataType}' and is not numeric.");
    }

    internal static double AverageColumn(ISeries column, IReadOnlyList<int>? rowIndexes)
    {
        var numericType = GetNumericAggregationType(column);
        var rowCount = GetAggregationRowCount(column, rowIndexes);

        if (numericType == typeof(byte) || numericType == typeof(sbyte) || numericType == typeof(short) ||
            numericType == typeof(ushort) || numericType == typeof(int))
        {
            var sum = 0;
            for (var index = 0; index < rowCount; index++)
            {
                sum = checked(sum + GetInt32AggregationValue(column, numericType, GetAggregationRowIndex(rowIndexes, index)));
            }

            return (double)sum / rowCount;
        }

        if (numericType == typeof(uint))
        {
            var sum = 0u;
            for (var index = 0; index < rowCount; index++)
            {
                sum = checked(sum + GetAggregationValue<uint>(column, GetAggregationRowIndex(rowIndexes, index)));
            }

            return (double)sum / rowCount;
        }

        if (numericType == typeof(long))
        {
            var sum = 0L;
            for (var index = 0; index < rowCount; index++)
            {
                sum = checked(sum + GetAggregationValue<long>(column, GetAggregationRowIndex(rowIndexes, index)));
            }

            return (double)sum / rowCount;
        }

        if (numericType == typeof(ulong))
        {
            var sum = 0UL;
            for (var index = 0; index < rowCount; index++)
            {
                sum = checked(sum + GetAggregationValue<ulong>(column, GetAggregationRowIndex(rowIndexes, index)));
            }

            return (double)sum / rowCount;
        }

        if (numericType == typeof(float))
        {
            var sum = 0f;
            for (var index = 0; index < rowCount; index++)
            {
                sum += GetAggregationValue<float>(column, GetAggregationRowIndex(rowIndexes, index));
            }

            return sum / rowCount;
        }

        if (numericType == typeof(double))
        {
            var sum = 0d;
            for (var index = 0; index < rowCount; index++)
            {
                sum += GetAggregationValue<double>(column, GetAggregationRowIndex(rowIndexes, index));
            }

            return sum / rowCount;
        }

        if (numericType == typeof(decimal))
        {
            var sum = 0m;
            for (var index = 0; index < rowCount; index++)
            {
                sum = checked(sum + GetAggregationValue<decimal>(column, GetAggregationRowIndex(rowIndexes, index)));
            }

            return (double)(sum / rowCount);
        }

        throw new ArgumentException($"Column '{column.Name}' has data type '{column.DataType}' and is not numeric.");
    }

    internal static object? MinOrMaxColumn(ISeries column, IReadOnlyList<int>? rowIndexes, bool findMaximum)
    {
        var expectedType = Nullable.GetUnderlyingType(column.DataType) ?? column.DataType;
        if (!typeof(IComparable).IsAssignableFrom(expectedType))
        {
            throw new ArgumentException(
                $"Column '{column.Name}' has data type '{column.DataType}' and cannot be compared safely.");
        }

        var rowCount = GetAggregationRowCount(column, rowIndexes);
        var result = GetNonNullAggregationValue(column, GetAggregationRowIndex(rowIndexes, 0));
        if (!expectedType.IsInstanceOfType(result))
        {
            throw CreateIncompatibleAggregationValueException(column, result);
        }

        var resultComparable = (IComparable)result;
        for (var index = 1; index < rowCount; index++)
        {
            var candidate = GetNonNullAggregationValue(column, GetAggregationRowIndex(rowIndexes, index));
            if (!expectedType.IsInstanceOfType(candidate))
            {
                throw CreateIncompatibleAggregationValueException(column, candidate);
            }

            try
            {
                var comparison = resultComparable.CompareTo(candidate);
                if ((findMaximum && comparison < 0) || (!findMaximum && comparison > 0))
                {
                    result = candidate;
                    resultComparable = (IComparable)candidate;
                }
            }
            catch (ArgumentException exception)
            {
                throw new ArgumentException(
                    $"Column '{column.Name}' contains values that cannot be compared safely.",
                    exception);
            }
        }

        return result;
    }

    internal static Type GetSumResultType(ISeries column)
    {
        var numericType = GetNumericAggregationType(column);
        if (numericType == typeof(byte) || numericType == typeof(sbyte) || numericType == typeof(short) ||
            numericType == typeof(ushort) || numericType == typeof(int))
        {
            return typeof(int);
        }

        return numericType;
    }

    internal static Type GetNumericAggregationType(ISeries column)
    {
        var dataType = Nullable.GetUnderlyingType(column.DataType) ?? column.DataType;
        if (dataType == typeof(byte) || dataType == typeof(sbyte) || dataType == typeof(short) ||
            dataType == typeof(ushort) || dataType == typeof(int) || dataType == typeof(uint) ||
            dataType == typeof(long) || dataType == typeof(ulong) || dataType == typeof(float) ||
            dataType == typeof(double) || dataType == typeof(decimal))
        {
            return dataType;
        }

        throw new ArgumentException($"Column '{column.Name}' has data type '{column.DataType}' and is not numeric.");
    }

    private static int GetAggregationRowCount(ISeries column, IReadOnlyList<int>? rowIndexes)
    {
        var rowCount = rowIndexes?.Count ?? column.Count;
        if (rowCount == 0)
        {
            throw new ArgumentException($"Column '{column.Name}' contains no values to aggregate.");
        }

        return rowCount;
    }

    private static int GetAggregationRowIndex(IReadOnlyList<int>? rowIndexes, int index)
    {
        return rowIndexes is null ? index : rowIndexes[index];
    }

    private static T GetAggregationValue<T>(ISeries column, int index)
        where T : struct
    {
        var value = GetNonNullAggregationValue(column, index);
        if (value is T typedValue)
        {
            return typedValue;
        }

        throw CreateIncompatibleAggregationValueException(column, value);
    }

    private static int GetInt32AggregationValue(ISeries column, Type numericType, int index)
    {
        var value = GetNonNullAggregationValue(column, index);
        if (numericType == typeof(byte) && value is byte byteValue)
        {
            return byteValue;
        }

        if (numericType == typeof(sbyte) && value is sbyte sbyteValue)
        {
            return sbyteValue;
        }

        if (numericType == typeof(short) && value is short shortValue)
        {
            return shortValue;
        }

        if (numericType == typeof(ushort) && value is ushort ushortValue)
        {
            return ushortValue;
        }

        if (numericType == typeof(int) && value is int intValue)
        {
            return intValue;
        }

        throw CreateIncompatibleAggregationValueException(column, value);
    }

    private static object GetNonNullAggregationValue(ISeries column, int index)
    {
        var value = column.GetValue(index);
        if (value is null)
        {
            throw new ArgumentException($"Column '{column.Name}' contains null values, which are not supported for aggregation.");
        }

        return value;
    }

    private static ArgumentException CreateIncompatibleAggregationValueException(ISeries column, object value)
    {
        return new ArgumentException(
            $"Column '{column.Name}' contains value type '{value.GetType()}' that does not match declared data type '{column.DataType}'.");
    }

    private ISeries[] ResolveDistinctKeyColumns(string[] columnNames)
    {
        ArgumentNullException.ThrowIfNull(columnNames);

        if (columnNames.Length == 0)
        {
            return columns.ToArray();
        }

        var selectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keyColumns = new ISeries[columnNames.Length];

        for (var index = 0; index < columnNames.Length; index++)
        {
            var columnName = columnNames[index];
            ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

            if (!selectedNames.Add(columnName))
            {
                throw new ArgumentException($"Column '{columnName}' was selected more than once.", nameof(columnNames));
            }

            keyColumns[index] = GetColumn(columnName);
        }

        return keyColumns;
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

    private static ISeries CreateUpdatedSeries(ISeries column, int rowIndex, object? updatedValue)
    {
        var values = Array.CreateInstance(column.DataType, column.Count);
        for (var index = 0; index < column.Count; index++)
        {
            values.SetValue(index == rowIndex ? updatedValue : column.GetValue(index), index);
        }

        var genericCreateMethod = CreateSeriesMethod.MakeGenericMethod(column.DataType);
        return (ISeries)genericCreateMethod.Invoke(null, [column.Name, values])!;
    }

    private static ISeries CreateRemovedSeries(ISeries column, int rowIndex)
    {
        var values = Array.CreateInstance(column.DataType, column.Count - 1);
        var targetIndex = 0;
        for (var index = 0; index < column.Count; index++)
        {
            if (index == rowIndex)
            {
                continue;
            }

            values.SetValue(column.GetValue(index), targetIndex);
            targetIndex++;
        }

        var genericCreateMethod = CreateSeriesMethod.MakeGenericMethod(column.DataType);
        return (ISeries)genericCreateMethod.Invoke(null, [column.Name, values])!;
    }

    private static ISeries CreateReorderedSeries(ISeries column, IReadOnlyList<int> rowIndexes)
    {
        var values = Array.CreateInstance(column.DataType, rowIndexes.Count);
        for (var index = 0; index < rowIndexes.Count; index++)
        {
            values.SetValue(column.GetValue(rowIndexes[index]), index);
        }

        var genericCreateMethod = CreateSeriesMethod.MakeGenericMethod(column.DataType);
        return (ISeries)genericCreateMethod.Invoke(null, [column.Name, values])!;
    }

    private static void ValidateSortableColumn(ISeries column)
    {
        var comparableType = Nullable.GetUnderlyingType(column.DataType) ?? column.DataType;
        if (!typeof(IComparable).IsAssignableFrom(comparableType))
        {
            throw new ArgumentException(
                $"Column '{column.Name}' has data type '{column.DataType}' and cannot be compared safely for sorting.");
        }

        for (var index = 0; index < column.Count; index++)
        {
            if (column.GetValue(index) is null)
            {
                throw new ArgumentException(
                    $"Column '{column.Name}' contains null values, which are not supported for sorting.");
            }
        }
    }

    private static int CompareSortValues(ISeries column, int leftIndex, int rightIndex)
    {
        var leftValue = column.GetValue(leftIndex);
        var rightValue = column.GetValue(rightIndex);

        if (leftValue is not IComparable leftComparable)
        {
            throw new ArgumentException(
                $"Column '{column.Name}' contains values that cannot be compared safely for sorting.");
        }

        try
        {
            return leftComparable.CompareTo(rightValue);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(
                $"Column '{column.Name}' contains values that cannot be compared safely for sorting.",
                exception);
        }
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

        var resultRowTotal = Math.Min(count, rowCount);
        var limitedColumns = columns
            .Select(column => CreateLeadingSeries(column, resultRowTotal))
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

    private void ValidateRowIndex(int index)
    {
        if (index < 0 || index >= rowCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                index,
                $"Row index must be between 0 and {rowCount - 1}, but the DataFrame contains {rowCount} rows.");
        }
    }

    private static void ValidateRowTotals(IReadOnlyList<ISeries> columns)
    {
        var expectedRowTotal = columns[0].Count;
        foreach (var column in columns)
        {
            if (column.Count != expectedRowTotal)
            {
                throw new ArgumentException(
                    $"Column '{column.Name}' contains {column.Count} values, but the DataFrame expects {expectedRowTotal} values.");
            }
        }
    }

    private sealed class RowKeyComparer : IEqualityComparer<object?[]>
    {
        internal static readonly RowKeyComparer Instance = new();

        private RowKeyComparer()
        {
        }

        public bool Equals(object?[]? left, object?[]? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null || left.Length != right.Length)
            {
                return false;
            }

            for (var index = 0; index < left.Length; index++)
            {
                if (!EqualityComparer<object?>.Default.Equals(left[index], right[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(object?[] key)
        {
            var hash = new HashCode();
            foreach (var value in key)
            {
                hash.Add(value);
            }

            return hash.ToHashCode();
        }
    }
}


