namespace Runiq.Data;

/// <summary>
/// Configures how a <see cref="DataFrame"/> is written to a CSV file.
/// </summary>
/// <remarks>
/// Defaults write a header row and use a comma delimiter. The writer always uses UTF-8 without a
/// byte-order mark, writes each physical CSV record with <see cref="Environment.NewLine"/>, and
/// replaces the target file after the CSV content has been serialized to a temporary file in the
/// target directory. CSV does not store schema metadata, so reading a written file may infer
/// compatible but not identical CLR types for values such as numeric-looking strings.
/// </remarks>
public sealed class CsvWriteOptions
{
    /// <summary>
    /// Initializes a new instance that writes headers and uses a comma delimiter.
    /// </summary>
    public CsvWriteOptions()
    {
    }

    /// <summary>
    /// Gets a value indicating whether column names are written as the first CSV record.
    /// </summary>
    /// <remarks>
    /// Header fields use the same delimiter, quote, carriage-return, and line-feed escaping rules
    /// as data fields. When this value is <see langword="false"/>, the first physical record is
    /// the first DataFrame row; a zero-row DataFrame then produces an empty file.
    /// </remarks>
    public bool IncludeHeader { get; init; } = true;

    /// <summary>
    /// Gets the single-character delimiter written between CSV fields.
    /// </summary>
    /// <remarks>
    /// The default delimiter is comma. The null character, quote character, carriage return, and
    /// line feed are rejected because they make CSV output ambiguous.
    /// </remarks>
    public char Delimiter { get; init; } = ',';
}
