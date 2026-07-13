using System.Text.Json;
using Runiq.Data.Series;

namespace Runiq.Data.IO;

/// <summary>
/// Coordinates JSON structure validation, primitive type inference, and DataFrame creation.
/// </summary>
internal static class JsonDataFrameReader
{
    internal static DataFrame Read(string path)
    {
        ValidatePath(path);

        var content = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("JSON file is empty or contains only whitespace.", nameof(path));
        }

        using var document = JsonDocument.Parse(content);
        return ReadDocument(document);
    }

    private static void ValidatePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
    }

    private static DataFrame ReadDocument(JsonDocument document)
    {
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("JSON root must be an array of objects.");
        }

        var rows = new List<Dictionary<string, JsonCellValue?>>();
        var columnNames = new List<string>();
        var seenColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var rowIndex = 0;
        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException($"JSON array item at index {rowIndex} must be an object.");
            }

            var row = new Dictionary<string, JsonCellValue?>(StringComparer.OrdinalIgnoreCase);
            var seenInRow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in item.EnumerateObject())
            {
                ValidatePropertyName(property.Name, rowIndex);
                if (!seenInRow.Add(property.Name))
                {
                    throw new ArgumentException($"JSON object at index {rowIndex} contains duplicate property name '{property.Name}'.");
                }

                if (seenColumns.Add(property.Name))
                {
                    columnNames.Add(property.Name);
                }

                row[property.Name] = ReadValue(property.Name, property.Value, rowIndex);
            }

            rows.Add(row);
            rowIndex++;
        }

        if (rows.Count == 0)
        {
            throw new ArgumentException("JSON array is empty; a DataFrame schema cannot be inferred.");
        }

        if (columnNames.Count == 0)
        {
            throw new ArgumentException("JSON objects did not produce any usable properties.");
        }

        return CreateDataFrame(columnNames, rows);
    }

    private static void ValidatePropertyName(string name, int rowIndex)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException($"JSON object at index {rowIndex} contains an empty or whitespace property name.");
        }
    }

    private static JsonCellValue? ReadValue(string propertyName, JsonElement value, int rowIndex)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => new JsonCellValue(value.GetString() ?? string.Empty, JsonCellKind.String),
            JsonValueKind.True => new JsonCellValue(true, JsonCellKind.Boolean),
            JsonValueKind.False => new JsonCellValue(false, JsonCellKind.Boolean),
            JsonValueKind.Number => ReadNumber(propertyName, value, rowIndex),
            JsonValueKind.Object => throw new ArgumentException($"JSON property '{propertyName}' at object index {rowIndex} contains a nested object, which is not supported."),
            JsonValueKind.Array => throw new ArgumentException($"JSON property '{propertyName}' at object index {rowIndex} contains an array, which is not supported."),
            _ => throw new ArgumentException($"JSON property '{propertyName}' at object index {rowIndex} contains unsupported JSON value kind '{value.ValueKind}'.")
        };
    }

    private static JsonCellValue ReadNumber(string propertyName, JsonElement value, int rowIndex)
    {
        if (value.TryGetInt32(out var intValue))
        {
            return new JsonCellValue(intValue, JsonCellKind.Int);
        }

        if (value.TryGetInt64(out var longValue))
        {
            return new JsonCellValue(longValue, JsonCellKind.Long);
        }

        if (value.TryGetDecimal(out var decimalValue))
        {
            return new JsonCellValue(decimalValue, JsonCellKind.Decimal);
        }

        var doubleValue = value.GetDouble();
        if (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue))
        {
            throw new ArgumentException($"JSON property '{propertyName}' at object index {rowIndex} contains a non-finite number.");
        }

        return new JsonCellValue(doubleValue, JsonCellKind.Double);
    }

    private static DataFrame CreateDataFrame(IReadOnlyList<string> columnNames, IReadOnlyList<Dictionary<string, JsonCellValue?>> rows)
    {
        var columns = new ISeries[columnNames.Count];
        for (var columnIndex = 0; columnIndex < columnNames.Count; columnIndex++)
        {
            var columnName = columnNames[columnIndex];
            var values = rows
                .Select(row => row.TryGetValue(columnName, out var value) ? value : null)
                .ToArray();
            var dataType = InferColumnType(columnName, values);

            columns[columnIndex] = DataFrame.CreateSeriesFromValues(
                columnName,
                dataType,
                values.Select(value => ConvertForDataType(value, dataType)));
        }

        return DataFrame.CreateFromSeries(columns);
    }

    private static Type InferColumnType(string columnName, IReadOnlyList<JsonCellValue?> values)
    {
        var hasNull = values.Any(static value => value is null);
        var nonNull = values.Where(static value => value is not null).Select(static value => value!.Value).ToArray();
        if (nonNull.Length == 0)
        {
            return typeof(string);
        }

        var kind = InferKind(columnName, nonNull);
        var type = kind switch
        {
            JsonColumnKind.String => typeof(string),
            JsonColumnKind.Boolean => typeof(bool),
            JsonColumnKind.Int => typeof(int),
            JsonColumnKind.Long => typeof(long),
            JsonColumnKind.Decimal => typeof(decimal),
            JsonColumnKind.Double => typeof(double),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported JSON column kind.")
        };

        return hasNull && type.IsValueType ? typeof(Nullable<>).MakeGenericType(type) : type;
    }

    private static JsonColumnKind InferKind(string columnName, IReadOnlyList<JsonCellValue> values)
    {
        if (values.All(static value => value.Kind == JsonCellKind.String))
        {
            return JsonColumnKind.String;
        }

        if (values.All(static value => value.Kind == JsonCellKind.Boolean))
        {
            return JsonColumnKind.Boolean;
        }

        if (values.All(static value => value.Kind is JsonCellKind.Int or JsonCellKind.Long or JsonCellKind.Decimal or JsonCellKind.Double))
        {
            return InferNumericKind(values);
        }

        throw new ArgumentException($"JSON column '{columnName}' contains incompatible primitive types.");
    }

    private static JsonColumnKind InferNumericKind(IReadOnlyList<JsonCellValue> values)
    {
        if (values.All(static value => value.Kind == JsonCellKind.Int))
        {
            return JsonColumnKind.Int;
        }

        if (values.All(static value => value.Kind is JsonCellKind.Int or JsonCellKind.Long))
        {
            return JsonColumnKind.Long;
        }

        if (values.All(static value => value.Kind is JsonCellKind.Int or JsonCellKind.Long or JsonCellKind.Decimal))
        {
            return JsonColumnKind.Decimal;
        }

        return JsonColumnKind.Double;
    }

    private static object? ConvertForDataType(JsonCellValue? value, Type dataType)
    {
        if (value is null)
        {
            return null;
        }

        var targetType = Nullable.GetUnderlyingType(dataType) ?? dataType;
        if (targetType == typeof(long) && value.Value.Value is int intValue)
        {
            return (long)intValue;
        }

        if (targetType == typeof(decimal))
        {
            return value.Value.Value switch
            {
                int decimalIntValue => (decimal)decimalIntValue,
                long longValue => (decimal)longValue,
                decimal decimalValue => decimalValue,
                _ => value.Value.Value
            };
        }

        if (targetType == typeof(double))
        {
            return value.Value.Value switch
            {
                int doubleIntValue => (double)doubleIntValue,
                long longValue => (double)longValue,
                decimal decimalValue => (double)decimalValue,
                double doubleValue => doubleValue,
                _ => value.Value.Value
            };
        }

        return value.Value.Value;
    }

    private readonly record struct JsonCellValue(object Value, JsonCellKind Kind);

    private enum JsonCellKind
    {
        String,
        Boolean,
        Int,
        Long,
        Decimal,
        Double
    }

    private enum JsonColumnKind
    {
        String,
        Boolean,
        Int,
        Long,
        Decimal,
        Double
    }
}
