namespace Runiq.Data;

/// <summary>
/// Configures Excel workbook reading for <see cref="DataFrame.ReadExcel(string, ExcelReadOptions)"/>.
/// </summary>
/// <remarks>
/// Only <c>.xlsx</c> workbooks are supported. By default the first worksheet is read, header
/// behavior is inferred, and column names are read from the first used row. When
/// <see cref="Names"/> is supplied with <see cref="ExcelHeaderMode.Infer"/>, the first used row is
/// treated as data. Formula cells are read from cached workbook results and are not recalculated.
/// Excel date cells are returned as <see cref="DateTime"/> values with
/// <see cref="DateTimeKind.Unspecified"/>.
/// </remarks>
public sealed class ExcelReadOptions
{
    /// <summary>
    /// Gets the exact worksheet name to read, or <see langword="null"/> to use index/default selection.
    /// </summary>
    /// <remarks>
    /// This value cannot be empty or whitespace and cannot be supplied together with
    /// <see cref="SheetIndex"/>. Hidden worksheets can be read when selected by name.
    /// </remarks>
    public string? SheetName { get; init; }

    /// <summary>
    /// Gets the zero-based worksheet index to read, or <see langword="null"/> to use name/default selection.
    /// </summary>
    /// <remarks>
    /// This value cannot be negative and cannot be supplied together with <see cref="SheetName"/>.
    /// When neither selector is supplied, the first worksheet is read.
    /// </remarks>
    public int? SheetIndex { get; init; }

    /// <summary>
    /// Gets the header interpretation mode used to decide whether the first used row is metadata or data.
    /// </summary>
    public ExcelHeaderMode Header { get; init; } = ExcelHeaderMode.Infer;

    /// <summary>
    /// Gets optional explicit column names whose order becomes the DataFrame column order.
    /// </summary>
    /// <remarks>
    /// Names may be <see langword="null"/>. When provided, the collection must be non-empty,
    /// contain no null, empty, whitespace, or duplicate names, and its count must match the
    /// resolved worksheet column count. The supplied collection is copied and never mutated.
    /// </remarks>
    public IReadOnlyList<string>? Names { get; init; }
}
