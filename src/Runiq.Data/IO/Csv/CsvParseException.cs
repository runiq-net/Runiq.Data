namespace Runiq.Data.IO;

/// <summary>
/// Reports malformed CSV syntax with line information while keeping the exception internal to CSV reading.
/// </summary>
internal sealed class CsvParseException : FormatException
{
    internal CsvParseException(string message)
        : base(message)
    {
    }
}
