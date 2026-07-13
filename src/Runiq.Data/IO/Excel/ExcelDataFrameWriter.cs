using ClosedXML.Excel;
using Runiq.Data.Series;

namespace Runiq.Data.IO;

/// <summary>
/// Coordinates Excel write option validation, native cell serialization, and safe target replacement.
/// </summary>
internal static class ExcelDataFrameWriter
{
    private const long LargestSafeInteger = 999_999_999_999_999L;
    private static readonly DateTime MinimumExcelDate = new(1900, 1, 1);
    private static readonly DateTime MaximumExcelDate = new(9999, 12, 31, 23, 59, 59, 999);
    private static readonly char[] InvalidWorksheetNameCharacters = [':', '\\', '/', '?', '*', '[', ']'];

    internal static void Write(DataFrame dataFrame, string path, ExcelWriteOptions options)
    {
        ArgumentNullException.ThrowIfNull(dataFrame);
        ValidatePath(path);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
        {
            directory = Directory.GetCurrentDirectory();
            fullPath = Path.Combine(directory, fullPath);
        }

        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Could not find a part of the path '{directory}'.");
        }

        var temporaryPath = CreateTemporaryPath(directory);
        try
        {
            using (var workbook = new XLWorkbook())
            {
                WriteWorkbook(dataFrame, workbook, options);
                workbook.SaveAs(temporaryPath);
            }

            ReplaceTarget(temporaryPath, fullPath);
            temporaryPath = string.Empty;
        }
        finally
        {
            DeleteTemporaryFile(temporaryPath);
        }
    }

    private static void ValidatePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var extension = Path.GetExtension(path);
        if (!extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Excel writing supports .xlsx workbooks only. File extension '{extension}' is not supported.");
        }
    }

    private static void ValidateOptions(ExcelWriteOptions options)
    {
        if (options.SheetName is null)
        {
            throw new ArgumentException("ExcelWriteOptions.SheetName cannot be null.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.SheetName))
        {
            throw new ArgumentException("ExcelWriteOptions.SheetName cannot be empty or whitespace.", nameof(options));
        }

        if (options.SheetName.Length > 31)
        {
            throw new ArgumentException("ExcelWriteOptions.SheetName cannot be longer than 31 characters.", nameof(options));
        }

        var invalidCharacter = options.SheetName.IndexOfAny(InvalidWorksheetNameCharacters);
        if (invalidCharacter >= 0)
        {
            throw new ArgumentException($"ExcelWriteOptions.SheetName contains invalid character '{options.SheetName[invalidCharacter]}'.", nameof(options));
        }

        if (options.SheetName[0] == '\'' || options.SheetName[^1] == '\'')
        {
            throw new ArgumentException("ExcelWriteOptions.SheetName cannot start or end with an apostrophe.", nameof(options));
        }
    }

    private static void WriteWorkbook(DataFrame dataFrame, XLWorkbook workbook, ExcelWriteOptions options)
    {
        if (dataFrame.ColumnTotalCore == 0)
        {
            throw new ArgumentException("A DataFrame with zero columns cannot be written as Excel.", nameof(dataFrame));
        }

        var worksheet = workbook.Worksheets.Add(options.SheetName);
        var rowOffset = 1;
        if (options.IncludeHeader)
        {
            for (var columnIndex = 0; columnIndex < dataFrame.ColumnSeries.Count; columnIndex++)
            {
                var cell = worksheet.Cell(1, columnIndex + 1);
                cell.Style.NumberFormat.Format = "@";
                cell.SetValue(dataFrame.ColumnSeries[columnIndex].Name);
            }

            rowOffset = 2;
        }

        for (var rowIndex = 0; rowIndex < dataFrame.RowTotalCore; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < dataFrame.ColumnSeries.Count; columnIndex++)
            {
                WriteCell(worksheet.Cell(rowIndex + rowOffset, columnIndex + 1), dataFrame.ColumnSeries[columnIndex], rowIndex);
            }
        }
    }

    private static void WriteCell(IXLCell cell, ISeries column, int rowIndex)
    {
        var value = column.GetValue(rowIndex);
        if (value is null)
        {
            cell.Clear(XLClearOptions.Contents);
            return;
        }

        var expectedType = Nullable.GetUnderlyingType(column.DataType) ?? column.DataType;
        if (!expectedType.IsInstanceOfType(value))
        {
            throw new ArgumentException(
                $"Column '{column.Name}' row {rowIndex} contains runtime type '{value.GetType()}' but the column declares '{column.DataType}'.");
        }

        switch (value)
        {
            case string stringValue:
                cell.Style.NumberFormat.Format = "@";
                cell.SetValue(stringValue);
                break;
            case bool boolValue:
                cell.SetValue(boolValue);
                break;
            case byte byteValue:
                cell.SetValue((double)byteValue);
                break;
            case sbyte sbyteValue:
                cell.SetValue((double)sbyteValue);
                break;
            case short shortValue:
                cell.SetValue((double)shortValue);
                break;
            case ushort ushortValue:
                cell.SetValue((double)ushortValue);
                break;
            case int intValue:
                cell.SetValue((double)intValue);
                break;
            case uint uintValue:
                ValidateSafeInteger(column, rowIndex, uintValue);
                cell.SetValue((double)uintValue);
                break;
            case long longValue:
                ValidateSafeInteger(column, rowIndex, longValue);
                cell.SetValue((double)longValue);
                break;
            case ulong ulongValue:
                ValidateSafeInteger(column, rowIndex, ulongValue);
                cell.SetValue((double)ulongValue);
                break;
            case float floatValue:
                ValidateFinite(column, rowIndex, floatValue);
                cell.SetValue((double)floatValue);
                break;
            case double doubleValue:
                ValidateFinite(column, rowIndex, doubleValue);
                cell.SetValue(doubleValue);
                break;
            case decimal decimalValue:
                var decimalAsDouble = (double)decimalValue;
                if ((decimal)decimalAsDouble != decimalValue)
                {
                    throw CreateUnsupportedValueException(column, rowIndex, value);
                }

                cell.SetValue(decimalAsDouble);
                break;
            case DateTime dateTime:
                ValidateExcelDate(column, rowIndex, dateTime);
                cell.SetValue(dateTime);
                cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss.000";
                break;
            default:
                throw CreateUnsupportedValueException(column, rowIndex, value);
        }
    }

    private static void ValidateFinite(ISeries column, int rowIndex, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw CreateUnsupportedValueException(column, rowIndex, value);
        }
    }

    private static void ValidateSafeInteger(ISeries column, int rowIndex, long value)
    {
        if (value < -LargestSafeInteger || value > LargestSafeInteger)
        {
            throw CreateUnsupportedValueException(column, rowIndex, value);
        }
    }

    private static void ValidateSafeInteger(ISeries column, int rowIndex, ulong value)
    {
        if (value > LargestSafeInteger)
        {
            throw CreateUnsupportedValueException(column, rowIndex, value);
        }
    }

    private static void ValidateExcelDate(ISeries column, int rowIndex, DateTime value)
    {
        if (value < MinimumExcelDate || value > MaximumExcelDate)
        {
            throw CreateUnsupportedValueException(column, rowIndex, value);
        }
    }

    private static ArgumentException CreateUnsupportedValueException(ISeries column, int rowIndex, object value)
    {
        return new ArgumentException(
            $"Unsupported Excel value type '{value.GetType()}' in column '{column.Name}' at row {rowIndex}. Supported types are string, Boolean, finite numeric values, DateTime within the Excel date range, and null.");
    }

    private static string CreateTemporaryPath(string directory)
    {
        return Path.Combine(directory, $".runiq-data-{Guid.NewGuid():N}.tmp.xlsx");
    }

    private static void ReplaceTarget(string temporaryPath, string fullPath)
    {
        if (File.Exists(fullPath))
        {
            if ((File.GetAttributes(fullPath) & FileAttributes.ReadOnly) != 0)
            {
                throw new UnauthorizedAccessException($"Access to the read-only Excel target '{fullPath}' is denied.");
            }

            File.Replace(temporaryPath, fullPath, null);
            return;
        }

        File.Move(temporaryPath, fullPath);
    }

    private static void DeleteTemporaryFile(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Cleanup is best-effort and must not hide the original serialization or I/O failure.
        }
    }
}
