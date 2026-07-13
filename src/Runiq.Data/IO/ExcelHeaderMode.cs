namespace Runiq.Data;

/// <summary>
/// Specifies how the first used Excel worksheet row is interpreted when reading a DataFrame.
/// </summary>
public enum ExcelHeaderMode
{
    /// <summary>
    /// Treats the first used row as a header unless explicit column names are supplied.
    /// </summary>
    Infer,

    /// <summary>
    /// Always consumes the first used row as a header row.
    /// </summary>
    Present,

    /// <summary>
    /// Always treats the first used row as data.
    /// </summary>
    Absent
}
