using System.Globalization;
using ClosedXML.Excel;
using Runiq.Data.Series;

namespace Runiq.Data.IO;

/// <summary>
/// Coordinates Excel option validation, worksheet resolution, range detection, type inference, and DataFrame creation.
/// </summary>
internal static class ExcelDataFrameReader
{
    internal static DataFrame Read(string path, ExcelReadOptions options)
    {
        ValidatePath(path);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);
        var names = options.Names is null ? null : ValidateNames(options.Names);

        using var workbook = new XLWorkbook(path);
        var worksheet = ResolveWorksheet(workbook, options);
        var dataRange = ResolveDataRange(worksheet);
        if (dataRange is null)
        {
            if (names is not null)
            {
                return CreateEmptyDataFrame(names);
            }

            throw new ArgumentException($"Worksheet '{worksheet.Name}' does not contain any value or formula cells.", nameof(path));
        }

        var resolvedRange = dataRange.Value;
        var cells = ReadCells(worksheet, resolvedRange);
        var resolved = ResolveHeader(worksheet, resolvedRange, cells, options.Header, names);
        return CreateDataFrame(resolved.ColumnNames, resolved.DataRows);
    }

    private static void ValidatePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var extension = Path.GetExtension(path);
        if (extension.Equals(".xls", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".xlsb", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".ods", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Excel reading supports .xlsx workbooks only. File extension '{extension}' is not supported.");
        }
    }

    private static void ValidateOptions(ExcelReadOptions options)
    {
        if (!Enum.IsDefined(options.Header))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Header, "ExcelReadOptions.Header contains an undefined ExcelHeaderMode value.");
        }

        if (options.SheetName is not null && string.IsNullOrWhiteSpace(options.SheetName))
        {
            throw new ArgumentException("ExcelReadOptions.SheetName cannot be empty or whitespace.", nameof(options));
        }

        if (options.SheetIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.SheetIndex, "ExcelReadOptions.SheetIndex must be zero or greater.");
        }

        if (options.SheetName is not null && options.SheetIndex is not null)
        {
            throw new ArgumentException("Specify either ExcelReadOptions.SheetName or ExcelReadOptions.SheetIndex, not both.", nameof(options));
        }
    }

    private static string[] ValidateNames(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
        {
            throw new ArgumentException("ExcelReadOptions.Names must contain at least one column name.", nameof(names));
        }

        var snapshot = new string[names.Count];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < names.Count; index++)
        {
            var name = names[index];
            if (name is null)
            {
                throw new ArgumentException($"ExcelReadOptions.Names contains a null value at index {index}.", nameof(names));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"ExcelReadOptions.Names contains an empty or whitespace column name at index {index}.", nameof(names));
            }

            if (!seen.Add(name))
            {
                throw new ArgumentException($"ExcelReadOptions.Names contains duplicate column name '{name}'.", nameof(names));
            }

            snapshot[index] = name;
        }

        return snapshot;
    }

    private static IXLWorksheet ResolveWorksheet(XLWorkbook workbook, ExcelReadOptions options)
    {
        var worksheetCount = workbook.Worksheets.Count;
        if (worksheetCount == 0)
        {
            throw new ArgumentException("Excel workbook does not contain any worksheets.");
        }

        if (options.SheetName is not null)
        {
            if (workbook.Worksheets.TryGetWorksheet(options.SheetName, out var worksheet))
            {
                return worksheet;
            }

            throw new ArgumentException($"Excel workbook does not contain a worksheet named '{options.SheetName}'.", nameof(options));
        }

        var index = options.SheetIndex ?? 0;
        if (index >= worksheetCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                index,
                $"ExcelReadOptions.SheetIndex {index} is outside the worksheet range. The workbook contains {worksheetCount} worksheet(s).");
        }

        return workbook.Worksheets.Worksheet(index + 1);
    }

    private static ExcelDataRange? ResolveDataRange(IXLWorksheet worksheet)
    {
        var cells = worksheet.CellsUsed(XLCellsUsedOptions.Contents)
            .Where(HasContent)
            .ToArray();

        if (cells.Length == 0)
        {
            return null;
        }

        return new ExcelDataRange(
            cells.Min(static cell => cell.Address.RowNumber),
            cells.Max(static cell => cell.Address.RowNumber),
            cells.Min(static cell => cell.Address.ColumnNumber),
            cells.Max(static cell => cell.Address.ColumnNumber));
    }

    private static bool HasContent(IXLCell cell)
    {
        return !string.IsNullOrEmpty(cell.FormulaA1) || cell.Value.Type != XLDataType.Blank;
    }

    private static ExcelCellValue?[][] ReadCells(IXLWorksheet worksheet, ExcelDataRange range)
    {
        var rowCount = range.LastRow - range.FirstRow + 1;
        var columnCount = range.LastColumn - range.FirstColumn + 1;
        var rows = new ExcelCellValue?[rowCount][];

        for (var rowOffset = 0; rowOffset < rowCount; rowOffset++)
        {
            var values = new ExcelCellValue?[columnCount];
            for (var columnOffset = 0; columnOffset < columnCount; columnOffset++)
            {
                var cell = worksheet.Cell(range.FirstRow + rowOffset, range.FirstColumn + columnOffset);
                values[columnOffset] = ReadCellValue(worksheet, cell);
            }

            rows[rowOffset] = values;
        }

        return rows;
    }

    private static ExcelCellValue? ReadCellValue(IXLWorksheet worksheet, IXLCell cell)
    {
        var isFormula = !string.IsNullOrEmpty(cell.FormulaA1);
        if (isFormula && cell.NeedsRecalculation)
        {
            throw new ArgumentException($"Formula cell '{worksheet.Name}'!{cell.Address} does not have a usable cached result.");
        }

        var value = isFormula ? cell.CachedValue : cell.Value;
        return ConvertCellValue(worksheet, cell, value);
    }

    private static ExcelCellValue? ConvertCellValue(IXLWorksheet worksheet, IXLCell cell, XLCellValue value)
    {
        return value.Type switch
        {
            XLDataType.Blank => null,
            XLDataType.Text => new ExcelCellValue(value.GetText(), ExcelCellKind.Text),
            XLDataType.Boolean => new ExcelCellValue(value.GetBoolean(), ExcelCellKind.Boolean),
            XLDataType.Number when IsDateFormatted(cell) => new ExcelCellValue(
                DateTime.SpecifyKind(DateTime.FromOADate(value.GetNumber()), DateTimeKind.Unspecified),
                ExcelCellKind.DateTime),
            XLDataType.Number => new ExcelCellValue(ConvertNumber(value.GetNumber()), ExcelCellKind.Numeric),
            XLDataType.DateTime => new ExcelCellValue(DateTime.SpecifyKind(value.GetDateTime(), DateTimeKind.Unspecified), ExcelCellKind.DateTime),
            XLDataType.TimeSpan => new ExcelCellValue(value.GetTimeSpan().ToString("c", CultureInfo.InvariantCulture), ExcelCellKind.Text),
            XLDataType.Error => throw new ArgumentException($"Excel error cell '{worksheet.Name}'!{cell.Address} contains {value.GetError()}."),
            _ => throw new ArgumentOutOfRangeException(nameof(value), value.Type, "Unsupported Excel cell value type.")
        };
    }

    private static bool IsDateFormatted(IXLCell cell)
    {
        var numberFormatId = cell.Style.DateFormat.NumberFormatId;
        if ((numberFormatId >= 14 && numberFormatId <= 22) ||
            (numberFormatId >= 27 && numberFormatId <= 36) ||
            (numberFormatId >= 45 && numberFormatId <= 47) ||
            (numberFormatId >= 50 && numberFormatId <= 58))
        {
            return true;
        }

        var format = cell.Style.DateFormat.Format;
        if (string.IsNullOrWhiteSpace(format))
        {
            return false;
        }

        var normalized = RemoveQuotedAndEscapedFormatText(format).ToLowerInvariant();
        return normalized.Any(static character => character is 'y' or 'm' or 'd' or 'h' or 's');
    }

    private static string RemoveQuotedAndEscapedFormatText(string format)
    {
        var result = new char[format.Length];
        var resultLength = 0;
        var inQuotedText = false;

        for (var index = 0; index < format.Length; index++)
        {
            var character = format[index];
            if (character == '"')
            {
                inQuotedText = !inQuotedText;
                continue;
            }

            if (inQuotedText)
            {
                continue;
            }

            if (character == '\\' || character == '_')
            {
                index++;
                continue;
            }

            result[resultLength] = character;
            resultLength++;
        }

        return new string(result, 0, resultLength);
    }

    private static object ConvertNumber(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentException($"Excel numeric value '{value.ToString(CultureInfo.InvariantCulture)}' is not finite.");
        }

        if (value >= long.MinValue && value <= long.MaxValue && Math.Truncate(value) == value)
        {
            var longValue = (long)value;
            if ((double)longValue == value)
            {
                if (longValue >= int.MinValue && longValue <= int.MaxValue)
                {
                    return (int)longValue;
                }

                return longValue;
            }
        }

        if (value >= (double)decimal.MinValue && value <= (double)decimal.MaxValue)
        {
            var decimalValue = (decimal)value;
            if ((double)decimalValue == value)
            {
                return decimalValue;
            }
        }

        return value;
    }

    private static ResolvedExcel ResolveHeader(
        IXLWorksheet worksheet,
        ExcelDataRange range,
        ExcelCellValue?[][] rows,
        ExcelHeaderMode header,
        string[]? names)
    {
        return header switch
        {
            ExcelHeaderMode.Infer when names is null => ResolvePresentHeader(worksheet, range, rows, null),
            ExcelHeaderMode.Infer => ResolveAbsentHeader(range, rows, names),
            ExcelHeaderMode.Present => ResolvePresentHeader(worksheet, range, rows, names),
            ExcelHeaderMode.Absent => ResolveAbsentHeader(range, rows, names),
            _ => throw new ArgumentOutOfRangeException(nameof(header), header, "Excel header mode is not supported.")
        };
    }

    private static ResolvedExcel ResolvePresentHeader(IXLWorksheet worksheet, ExcelDataRange range, ExcelCellValue?[][] rows, string[]? names)
    {
        ValidateMergedHeader(worksheet, range);
        var sourceNames = ValidateHeaderNames(rows[0]);
        if (names is not null && names.Length != sourceNames.Length)
        {
            throw new ArgumentException($"ExcelReadOptions.Names contains {names.Length} names, but the Excel header has {sourceNames.Length} columns.");
        }

        return new ResolvedExcel(names ?? sourceNames, rows.Skip(1).ToArray());
    }

    private static ResolvedExcel ResolveAbsentHeader(ExcelDataRange range, ExcelCellValue?[][] rows, string[]? names)
    {
        var columnCount = range.LastColumn - range.FirstColumn + 1;
        if (columnCount == 0)
        {
            throw new ArgumentException("Excel worksheet did not produce any usable columns.");
        }

        if (names is not null && names.Length != columnCount)
        {
            throw new ArgumentException($"ExcelReadOptions.Names contains {names.Length} names, but the Excel data has {columnCount} columns.");
        }

        var columnNames = names ?? Enumerable.Range(1, columnCount).Select(static index => $"Column{index}").ToArray();
        return new ResolvedExcel(columnNames, rows);
    }

    private static void ValidateMergedHeader(IXLWorksheet worksheet, ExcelDataRange range)
    {
        foreach (var mergedRange in worksheet.MergedRanges)
        {
            if (mergedRange.FirstRow().RowNumber() <= range.FirstRow &&
                mergedRange.LastRow().RowNumber() >= range.FirstRow &&
                mergedRange.FirstColumn().ColumnNumber() < mergedRange.LastColumn().ColumnNumber() &&
                mergedRange.LastColumn().ColumnNumber() >= range.FirstColumn &&
                mergedRange.FirstColumn().ColumnNumber() <= range.LastColumn)
            {
                throw new ArgumentException($"Worksheet '{worksheet.Name}' has a merged header range '{mergedRange.RangeAddress}' spanning multiple DataFrame columns.");
            }
        }
    }

    private static string[] ValidateHeaderNames(IReadOnlyList<ExcelCellValue?> fields)
    {
        if (fields.Count == 0)
        {
            throw new ArgumentException("Excel header did not produce any usable columns.");
        }

        var names = new string[fields.Count];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < fields.Count; index++)
        {
            var name = HeaderToString(fields[index], index);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"Excel header column {index + 1} is empty or whitespace.");
            }

            if (!seen.Add(name))
            {
                throw new ArgumentException($"Excel header contains duplicate column name '{name}'.");
            }

            names[index] = name;
        }

        return names;
    }

    private static string HeaderToString(ExcelCellValue? value, int index)
    {
        if (value is null)
        {
            throw new ArgumentException($"Excel header column {index + 1} is blank.");
        }

        return ValueToString(value.Value.Value);
    }

    private static DataFrame CreateDataFrame(IReadOnlyList<string> columnNames, IReadOnlyList<ExcelCellValue?[]> rows)
    {
        if (columnNames.Count == 0)
        {
            throw new ArgumentException("Excel worksheet did not produce any usable columns.");
        }

        var columns = new ISeries[columnNames.Count];
        for (var columnIndex = 0; columnIndex < columnNames.Count; columnIndex++)
        {
            var values = rows.Select(row => row[columnIndex]).ToArray();
            var dataType = InferColumnType(values);
            columns[columnIndex] = DataFrame.CreateSeriesFromValues(
                columnNames[columnIndex],
                dataType,
                values.Select(value => ConvertForDataType(value, dataType)));
        }

        return DataFrame.CreateFromSeries(columns);
    }

    private static Type InferColumnType(IReadOnlyList<ExcelCellValue?> values)
    {
        var hasBlank = values.Any(static value => value is null);
        var nonBlank = values.Where(static value => value is not null).Select(static value => value!.Value).ToArray();
        if (nonBlank.Length == 0)
        {
            return typeof(string);
        }

        var kind = InferKind(nonBlank);
        var type = kind switch
        {
            ExcelColumnKind.Boolean => typeof(bool),
            ExcelColumnKind.Int => typeof(int),
            ExcelColumnKind.Long => typeof(long),
            ExcelColumnKind.Decimal => typeof(decimal),
            ExcelColumnKind.Double => typeof(double),
            ExcelColumnKind.DateTime => typeof(DateTime),
            _ => typeof(string)
        };

        return hasBlank && type.IsValueType ? typeof(Nullable<>).MakeGenericType(type) : type;
    }

    private static ExcelColumnKind InferKind(IReadOnlyList<ExcelCellValue> values)
    {
        if (values.Any(static value => value.Kind == ExcelCellKind.Text))
        {
            return ExcelColumnKind.String;
        }

        if (values.All(static value => value.Kind == ExcelCellKind.Boolean))
        {
            return ExcelColumnKind.Boolean;
        }

        if (values.All(static value => value.Kind == ExcelCellKind.DateTime))
        {
            return ExcelColumnKind.DateTime;
        }

        if (values.All(static value => value.Kind == ExcelCellKind.Numeric))
        {
            return InferNumericKind(values.Select(static value => value.Value).ToArray());
        }

        return ExcelColumnKind.String;
    }

    private static ExcelColumnKind InferNumericKind(IReadOnlyList<object> values)
    {
        if (values.All(static value => value is int))
        {
            return ExcelColumnKind.Int;
        }

        if (values.All(static value => value is int or long))
        {
            return ExcelColumnKind.Long;
        }

        if (values.All(static value => value is int or long or decimal))
        {
            return ExcelColumnKind.Decimal;
        }

        return ExcelColumnKind.Double;
    }

    private static object? ConvertForDataType(ExcelCellValue? value, Type dataType)
    {
        if (value is null)
        {
            return null;
        }

        var targetType = Nullable.GetUnderlyingType(dataType) ?? dataType;
        if (targetType == typeof(string))
        {
            return ValueToString(value.Value.Value);
        }

        if (targetType == typeof(long) && value.Value.Value is int intValue)
        {
            return (long)intValue;
        }

        if (targetType == typeof(decimal))
        {
            return value.Value.Value switch
            {
                int numericIntValue => (decimal)numericIntValue,
                long longValue => (decimal)longValue,
                decimal decimalValue => decimalValue,
                _ => value.Value.Value
            };
        }

        if (targetType == typeof(double))
        {
            return value.Value.Value switch
            {
                int numericIntValue => (double)numericIntValue,
                long longValue => (double)longValue,
                decimal decimalValue => (double)decimalValue,
                double doubleValue => doubleValue,
                _ => value.Value.Value
            };
        }

        return value.Value.Value;
    }

    private static string ValueToString(object value)
    {
        return value switch
        {
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static DataFrame CreateEmptyDataFrame(IReadOnlyList<string> columnNames)
    {
        var columns = columnNames
            .Select(static name => DataFrame.CreateSeriesFromValues(name, typeof(string), Array.Empty<object?>()))
            .ToArray();

        return DataFrame.CreateFromSeries(columns);
    }

    private readonly record struct ExcelDataRange(int FirstRow, int LastRow, int FirstColumn, int LastColumn);

    private readonly record struct ExcelCellValue(object Value, ExcelCellKind Kind);

    private sealed record ResolvedExcel(IReadOnlyList<string> ColumnNames, IReadOnlyList<ExcelCellValue?[]> DataRows);

    private enum ExcelCellKind
    {
        Text,
        Boolean,
        Numeric,
        DateTime
    }

    private enum ExcelColumnKind
    {
        String,
        Boolean,
        Int,
        Long,
        Decimal,
        Double,
        DateTime
    }
}
