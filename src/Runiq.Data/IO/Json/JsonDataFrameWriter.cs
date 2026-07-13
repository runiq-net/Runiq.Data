using System.Collections;
using System.Text;
using System.Text.Json;
using Runiq.Data.Series;

namespace Runiq.Data.IO;

/// <summary>
/// Coordinates JSON write option validation, native value serialization, and safe target replacement.
/// </summary>
internal static class JsonDataFrameWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes a DataFrame as a JSON array of objects without exposing DataFrame internals to the serializer.
    /// </summary>
    internal static void Write(DataFrame dataFrame, string path, JsonWriteOptions options)
    {
        ArgumentNullException.ThrowIfNull(dataFrame);
        ValidatePath(path);
        ArgumentNullException.ThrowIfNull(options);

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
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = options.WriteIndented }))
            {
                WriteContent(dataFrame, writer);
            }

            ReplaceTarget(temporaryPath, fullPath);
            temporaryPath = string.Empty;
        }
        finally
        {
            DeleteTemporaryFile(temporaryPath);
        }
    }

    /// <summary>
    /// Validates path arguments before temporary file creation so caller errors do not leave artifacts.
    /// </summary>
    private static void ValidatePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
    }

    /// <summary>
    /// Streams rows as objects and columns as ordered properties, which keeps JSON shape independent from DataFrame internals.
    /// </summary>
    private static void WriteContent(DataFrame dataFrame, Utf8JsonWriter writer)
    {
        writer.WriteStartArray();

        for (var rowIndex = 0; rowIndex < dataFrame.RowTotalCore; rowIndex++)
        {
            writer.WriteStartObject();
            foreach (var column in dataFrame.ColumnSeries)
            {
                writer.WritePropertyName(column.Name);
                WriteCellValue(writer, column, rowIndex);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// Converts one cell to a native JSON value while rejecting lossy or ambiguous fallback behavior.
    /// </summary>
    private static void WriteCellValue(Utf8JsonWriter writer, ISeries column, int rowIndex)
    {
        var value = column.GetValue(rowIndex);
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var expectedType = Nullable.GetUnderlyingType(column.DataType) ?? column.DataType;
        if (expectedType != typeof(object) && !expectedType.IsInstanceOfType(value))
        {
            throw new ArgumentException(
                $"Column '{column.Name}' contains runtime type '{value.GetType()}' that does not match declared data type '{column.DataType}' at row {rowIndex}.");
        }

        switch (value)
        {
            case string stringValue:
                writer.WriteStringValue(stringValue);
                break;
            case bool boolValue:
                writer.WriteBooleanValue(boolValue);
                break;
            case byte byteValue:
                writer.WriteNumberValue(byteValue);
                break;
            case sbyte sbyteValue:
                writer.WriteNumberValue(sbyteValue);
                break;
            case short shortValue:
                writer.WriteNumberValue(shortValue);
                break;
            case ushort ushortValue:
                writer.WriteNumberValue(ushortValue);
                break;
            case int intValue:
                writer.WriteNumberValue(intValue);
                break;
            case uint uintValue:
                writer.WriteNumberValue(uintValue);
                break;
            case long longValue:
                writer.WriteNumberValue(longValue);
                break;
            case ulong ulongValue:
                writer.WriteNumberValue(ulongValue);
                break;
            case decimal decimalValue:
                writer.WriteNumberValue(decimalValue);
                break;
            case float floatValue:
                ValidateFinite(column, rowIndex, floatValue);
                writer.WriteNumberValue(floatValue);
                break;
            case double doubleValue:
                ValidateFinite(column, rowIndex, doubleValue);
                writer.WriteNumberValue(doubleValue);
                break;
            case DateTime dateTime:
                writer.WriteStringValue(dateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
                break;
            case DateTimeOffset dateTimeOffset:
                writer.WriteStringValue(dateTimeOffset.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
                break;
            case Guid guidValue:
                writer.WriteStringValue(guidValue.ToString("D"));
                break;
            case Enum enumValue:
                writer.WriteStringValue(enumValue.ToString());
                break;
            default:
                throw CreateUnsupportedValueException(column, rowIndex, value);
        }
    }

    /// <summary>
    /// Rejects non-finite floating-point values because JSON has no portable representation for them.
    /// </summary>
    private static void ValidateFinite(ISeries column, int rowIndex, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw CreateUnsupportedValueException(column, rowIndex, value);
        }
    }

    /// <summary>
    /// Creates a diagnostic that covers nested objects, collections, dictionaries, and other unsupported values.
    /// </summary>
    private static ArgumentException CreateUnsupportedValueException(ISeries column, int rowIndex, object value)
    {
        var kind = value is IDictionary
            ? "dictionary"
            : value is IEnumerable and not string
                ? "collection"
                : "value";

        return new ArgumentException(
            $"Column '{column.Name}' contains unsupported JSON write {kind} type '{value.GetType()}' at row {rowIndex}.");
    }

    /// <summary>
    /// Creates a hidden temporary path in the destination directory so replacement stays on one volume.
    /// </summary>
    private static string CreateTemporaryPath(string directory)
    {
        return Path.Combine(directory, $".runiq-data-{Guid.NewGuid():N}.tmp.json");
    }

    /// <summary>
    /// Replaces the destination only after serialization succeeds, preserving existing content on failures.
    /// </summary>
    private static void ReplaceTarget(string temporaryPath, string fullPath)
    {
        if (File.Exists(fullPath))
        {
            if ((File.GetAttributes(fullPath) & FileAttributes.ReadOnly) != 0)
            {
                throw new UnauthorizedAccessException($"Access to the read-only JSON target '{fullPath}' is denied.");
            }

            File.Replace(temporaryPath, fullPath, null);
            return;
        }

        File.Move(temporaryPath, fullPath);
    }

    /// <summary>
    /// Removes abandoned temporary files without hiding the original serialization or I/O exception.
    /// </summary>
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
