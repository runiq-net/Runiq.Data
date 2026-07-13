using System.Globalization;

namespace Runiq.Data.IO;

/// <summary>
/// Infers column types from every non-missing CSV value and converts fields using the same deterministic rules.
/// </summary>
internal static class CsvTypeInference
{
    private static readonly string[] TrueValues = ["true"];
    private static readonly string[] FalseValues = ["false"];

    internal static CsvColumnType Infer(IReadOnlyList<CsvField> values)
    {
        var hasMissing = values.Any(static value => IsMissing(value));
        var nonMissing = values.Where(static value => !IsMissing(value)).Select(static value => value.Value).ToArray();
        if (nonMissing.Length == 0)
        {
            return new CsvColumnType(typeof(string), IsNullable: true);
        }

        var dataType = InferDataType(nonMissing);
        return new CsvColumnType(hasMissing && dataType.IsValueType ? typeof(Nullable<>).MakeGenericType(dataType) : dataType, hasMissing || !dataType.IsValueType);
    }

    internal static object? Convert(CsvField field, Type dataType)
    {
        if (IsMissing(field))
        {
            return null;
        }

        var targetType = Nullable.GetUnderlyingType(dataType) ?? dataType;
        if (targetType == typeof(bool))
        {
            return ParseBoolean(field.Value);
        }

        if (targetType == typeof(int))
        {
            return int.Parse(field.Value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(long))
        {
            return long.Parse(field.Value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(decimal))
        {
            return decimal.Parse(field.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(double))
        {
            return double.Parse(field.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
        }

        return field.Value;
    }

    internal static bool IsMissing(CsvField field)
    {
        return field.Value.Length == 0 && !field.WasQuoted;
    }

    private static Type InferDataType(IReadOnlyList<string> values)
    {
        if (values.All(CanParseBoolean))
        {
            return typeof(bool);
        }

        if (values.All(static value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
        {
            return typeof(int);
        }

        if (values.All(static value => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
        {
            return typeof(long);
        }

        if (values.All(static value => decimal.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _)))
        {
            return typeof(decimal);
        }

        if (values.All(CanParseDouble))
        {
            return typeof(double);
        }

        return typeof(string);
    }

    private static bool CanParseBoolean(string value)
    {
        return TrueValues.Contains(value, StringComparer.OrdinalIgnoreCase) ||
            FalseValues.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    private static bool ParseBoolean(string value)
    {
        if (TrueValues.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (FalseValues.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new FormatException($"Value '{value}' is not a supported Boolean literal. Supported values are true and false.");
    }

    private static bool CanParseDouble(string value)
    {
        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed) &&
            !double.IsNaN(parsed) &&
            !double.IsInfinity(parsed);
    }
}
