namespace Runiq.Data;

/// <summary>
/// Controls how CSV column names are resolved from the source file and optional explicit names.
/// </summary>
public enum CsvHeaderMode
{
    /// <summary>
    /// Uses the first row as a header when no explicit names are supplied; otherwise treats the first row as data.
    /// </summary>
    Infer,

    /// <summary>
    /// Always consumes the first row as a source header, replacing it with explicit names when names are supplied.
    /// </summary>
    Present,

    /// <summary>
    /// Always treats the first row as data and uses explicit names or generated Column1-style names.
    /// </summary>
    Absent
}
