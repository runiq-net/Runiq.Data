namespace Runiq.Data.IO;

/// <summary>
/// Stores one parsed CSV record with its starting physical line for validation diagnostics.
/// </summary>
internal sealed class CsvRecord
{
    internal CsvRecord(IReadOnlyList<CsvField> fields, int lineNumber)
    {
        Fields = fields;
        LineNumber = lineNumber;
    }

    internal IReadOnlyList<CsvField> Fields { get; }

    internal int LineNumber { get; }
}
