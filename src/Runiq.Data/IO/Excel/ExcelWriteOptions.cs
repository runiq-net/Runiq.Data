namespace Runiq.Data;

/// <summary>
/// Configures how a <see cref="DataFrame"/> is written to an Excel workbook.
/// </summary>
/// <remarks>
/// Defaults create a new <c>.xlsx</c> workbook with one worksheet named <c>Sheet1</c> and a
/// header row. Existing files are replaced after the workbook is serialized to a temporary
/// <c>.xlsx</c> file in the target directory. Excel stores dates without
/// <see cref="DateTimeKind"/> metadata, so values read back use <see cref="DateTimeKind.Unspecified"/>.
/// Excel also stores numeric cells with finite precision; integral and decimal values that
/// cannot be represented safely are rejected instead of silently rounded.
/// </remarks>
public sealed class ExcelWriteOptions
{
    /// <summary>
    /// Gets the worksheet name written to the generated workbook.
    /// </summary>
    /// <remarks>
    /// The default is <c>Sheet1</c>. The value cannot be null, empty, whitespace, longer than
    /// Excel's 31-character worksheet-name limit, start or end with an apostrophe, or contain
    /// <c>:</c>, <c>\</c>, <c>/</c>, <c>?</c>, <c>*</c>, <c>[</c>, or <c>]</c>. The value is
    /// used exactly as supplied and is not trimmed.
    /// </remarks>
    public string SheetName { get; init; } = "Sheet1";

    /// <summary>
    /// Gets a value indicating whether DataFrame column names are written as the first row.
    /// </summary>
    /// <remarks>
    /// When this value is <see langword="false"/>, the first DataFrame row starts at the first
    /// worksheet row. A zero-row DataFrame can then produce an empty worksheet, which requires
    /// explicit names when read back.
    /// </remarks>
    public bool IncludeHeader { get; init; } = true;
}
