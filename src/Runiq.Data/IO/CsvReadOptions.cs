namespace Runiq.Data;

/// <summary>
/// Configures CSV file reading for <see cref="DataFrame.ReadCsv(string, CsvReadOptions)"/>.
/// </summary>
/// <remarks>
/// By default, header behavior is inferred, the delimiter is a comma, and column names are read
/// from the first row. When <see cref="Names"/> is supplied with <see cref="CsvHeaderMode.Infer"/>,
/// the first CSV row is treated as data, matching the common Pandas <c>header="infer"</c> and
/// <c>names</c> interaction. Invalid header modes, delimiters, and column names fail before a
/// DataFrame is returned.
/// </remarks>
public sealed class CsvReadOptions
{
    /// <summary>
    /// Initializes a new instance with inferred header behavior, no explicit names, and comma delimiter.
    /// </summary>
    public CsvReadOptions()
    {
    }

    /// <summary>
    /// Gets the header interpretation mode used to decide whether the first row is metadata or data.
    /// </summary>
    public CsvHeaderMode Header { get; init; } = CsvHeaderMode.Infer;

    /// <summary>
    /// Gets optional explicit column names whose order becomes the DataFrame column order.
    /// </summary>
    /// <remarks>
    /// Names may be <see langword="null"/>. When provided, the collection must be non-empty,
    /// contain no null, empty, whitespace, or duplicate names, and its count must match the CSV
    /// column count. The supplied collection is copied and never mutated.
    /// </remarks>
    public IReadOnlyList<string>? Names { get; init; }

    /// <summary>
    /// Gets the single-character field delimiter used by the CSV parser.
    /// </summary>
    /// <remarks>
    /// The default delimiter is comma. Line breaks, the quote character, and the null character
    /// are rejected because they make CSV structure ambiguous.
    /// </remarks>
    public char Delimiter { get; init; } = ',';
}
