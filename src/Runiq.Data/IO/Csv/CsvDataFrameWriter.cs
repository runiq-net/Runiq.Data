using System.Globalization;
using System.Text;
using Runiq.Data.Series;

namespace Runiq.Data.IO;

/// <summary>
/// Coordinates CSV write option validation, deterministic field formatting, and safe target replacement.
/// </summary>
internal static class CsvDataFrameWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    internal static void Write(DataFrame dataFrame, string path, CsvWriteOptions options)
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

        var temporaryPath = CreateTemporaryPath(directory);
        try
        {
            using (var writer = new StreamWriter(new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None), Utf8NoBom))
            {
                WriteContent(dataFrame, writer, options);
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
    }

    private static void ValidateOptions(CsvWriteOptions options)
    {
        if (options.Delimiter == default)
        {
            throw new ArgumentException("CsvWriteOptions.Delimiter cannot be the null character.", nameof(options));
        }

        if (options.Delimiter == '"' || options.Delimiter == '\r' || options.Delimiter == '\n')
        {
            throw new ArgumentException("CsvWriteOptions.Delimiter cannot be a quote or line break character.", nameof(options));
        }
    }

    private static void WriteContent(DataFrame dataFrame, TextWriter writer, CsvWriteOptions options)
    {
        if (dataFrame.ColumnTotalCore == 0)
        {
            throw new ArgumentException("A DataFrame with zero columns cannot be written as CSV.", nameof(dataFrame));
        }

        if (options.IncludeHeader)
        {
            WriteRecord(writer, dataFrame.ColumnSeries.Select(static column => column.Name), options.Delimiter);
        }

        for (var rowIndex = 0; rowIndex < dataFrame.RowTotalCore; rowIndex++)
        {
            var fields = dataFrame.ColumnSeries.Select(column => FormatValue(column, rowIndex));
            WriteRecord(writer, fields, options.Delimiter);
        }
    }

    private static void WriteRecord(TextWriter writer, IEnumerable<string?> fields, char delimiter)
    {
        var first = true;
        foreach (var field in fields)
        {
            if (!first)
            {
                writer.Write(delimiter);
            }

            writer.Write(EscapeField(field, delimiter));
            first = false;
        }

        writer.Write(Environment.NewLine);
    }

    private static string? FormatValue(ISeries column, int rowIndex)
    {
        var value = column.GetValue(rowIndex);
        if (value is null)
        {
            return null;
        }

        var expectedType = Nullable.GetUnderlyingType(column.DataType) ?? column.DataType;
        if (!expectedType.IsInstanceOfType(value))
        {
            throw new ArgumentException(
                $"Column '{column.Name}' row {rowIndex} contains runtime type '{value.GetType()}' but the column declares '{column.DataType}'.");
        }

        return value switch
        {
            string stringValue => stringValue,
            bool boolValue => boolValue ? "true" : "false",
            byte byteValue => byteValue.ToString(CultureInfo.InvariantCulture),
            sbyte sbyteValue => sbyteValue.ToString(CultureInfo.InvariantCulture),
            short shortValue => shortValue.ToString(CultureInfo.InvariantCulture),
            ushort ushortValue => ushortValue.ToString(CultureInfo.InvariantCulture),
            int intValue => intValue.ToString(CultureInfo.InvariantCulture),
            uint uintValue => uintValue.ToString(CultureInfo.InvariantCulture),
            long longValue => longValue.ToString(CultureInfo.InvariantCulture),
            ulong ulongValue => ulongValue.ToString(CultureInfo.InvariantCulture),
            float floatValue when float.IsNaN(floatValue) || float.IsInfinity(floatValue) => throw CreateUnsupportedValueException(column, rowIndex, value),
            float floatValue => floatValue.ToString(CultureInfo.InvariantCulture),
            double doubleValue when double.IsNaN(doubleValue) || double.IsInfinity(doubleValue) => throw CreateUnsupportedValueException(column, rowIndex, value),
            double doubleValue => doubleValue.ToString(CultureInfo.InvariantCulture),
            decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
            _ => throw CreateUnsupportedValueException(column, rowIndex, value)
        };
    }

    private static string EscapeField(string? value, char delimiter)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value.Length == 0)
        {
            return "\"\"";
        }

        if (value.Contains(delimiter) || value.Contains('"') || value.Contains('\r') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }

    private static ArgumentException CreateUnsupportedValueException(ISeries column, int rowIndex, object value)
    {
        return new ArgumentException(
            $"Column '{column.Name}' row {rowIndex} contains unsupported runtime type '{value.GetType()}'.");
    }

    private static string CreateTemporaryPath(string directory)
    {
        return Path.Combine(directory, $".runiq-data-{Guid.NewGuid():N}.tmp");
    }

    private static void ReplaceTarget(string temporaryPath, string fullPath)
    {
        if (File.Exists(fullPath))
        {
            if ((File.GetAttributes(fullPath) & FileAttributes.ReadOnly) != 0)
            {
                throw new UnauthorizedAccessException($"Access to the read-only CSV target '{fullPath}' is denied.");
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
