namespace Runiq.Data.IO;

/// <summary>
/// Stores one parsed CSV field and whether quotes were used to distinguish empty strings from missing values.
/// </summary>
internal readonly record struct CsvField(string Value, bool WasQuoted);
