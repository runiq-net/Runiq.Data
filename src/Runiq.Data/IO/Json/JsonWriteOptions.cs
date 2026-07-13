namespace Runiq.Data;

/// <summary>
/// Configures how a <see cref="DataFrame"/> is written to a JSON file.
/// </summary>
/// <remarks>
/// The writer emits a JSON array of objects, preserves DataFrame row and column order, and
/// replaces the target file after the full JSON payload has been serialized to a temporary file
/// in the target directory.
/// </remarks>
public sealed class JsonWriteOptions
{
    /// <summary>
    /// Gets a value indicating whether the JSON output is formatted with indentation.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="true"/> so direct calls produce human-readable JSON. Set
    /// this value to <see langword="false"/> to produce compact JSON without insignificant
    /// whitespace.
    /// </remarks>
    public bool WriteIndented { get; init; } = true;
}
