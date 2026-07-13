using Runiq.Data.Series;

namespace Runiq.Data.IO;

/// <summary>
/// Coordinates CSV option validation, header resolution, row-shape validation, type inference, and DataFrame creation.
/// </summary>
internal static class CsvDataFrameReader
{
    internal static DataFrame Read(string path, CsvReadOptions options)
    {
        ValidatePath(path);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);
        var names = options.Names is null ? null : ValidateNames(options.Names);

        var content = File.ReadAllText(path);
        if (content.Length == 0)
        {
            if (options.Header == CsvHeaderMode.Absent && names is not null)
            {
                return CreateEmptyDataFrame(names);
            }

            throw new ArgumentException("CSV file is empty.", nameof(path));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("CSV file contains only whitespace.", nameof(path));
        }

        var records = new CsvParser(options.Delimiter).Parse(content);
        if (records.Count == 0)
        {
            throw new ArgumentException("CSV file does not contain any records.", nameof(path));
        }

        var resolved = ResolveHeader(records, options.Header, names);
        ValidateDataRows(resolved.DataRows, resolved.ColumnNames.Count);

        return CreateDataFrame(resolved.ColumnNames, resolved.DataRows);
    }

    private static void ValidatePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
    }

    private static void ValidateOptions(CsvReadOptions options)
    {
        if (!Enum.IsDefined(options.Header))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Header, "CsvReadOptions.Header contains an undefined CsvHeaderMode value.");
        }

        if (options.Delimiter == default)
        {
            throw new ArgumentException("CsvReadOptions.Delimiter cannot be the null character.", nameof(options));
        }

        if (options.Delimiter == '"' || options.Delimiter == '\r' || options.Delimiter == '\n')
        {
            throw new ArgumentException("CsvReadOptions.Delimiter cannot be a quote or line break character.", nameof(options));
        }
    }

    private static string[] ValidateNames(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
        {
            throw new ArgumentException("CsvReadOptions.Names must contain at least one column name.", nameof(names));
        }

        var snapshot = new string[names.Count];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < names.Count; index++)
        {
            var name = names[index];
            if (name is null)
            {
                throw new ArgumentException($"CsvReadOptions.Names contains a null value at index {index}.", nameof(names));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"CsvReadOptions.Names contains an empty or whitespace column name at index {index}.", nameof(names));
            }

            if (!seen.Add(name))
            {
                throw new ArgumentException($"CsvReadOptions.Names contains duplicate column name '{name}'.", nameof(names));
            }

            snapshot[index] = name;
        }

        return snapshot;
    }

    private static ResolvedCsv ResolveHeader(IReadOnlyList<CsvRecord> records, CsvHeaderMode header, string[]? names)
    {
        return header switch
        {
            CsvHeaderMode.Infer when names is null => ResolvePresentHeader(records, null),
            CsvHeaderMode.Infer => ResolveAbsentHeader(records, names),
            CsvHeaderMode.Present => ResolvePresentHeader(records, names),
            CsvHeaderMode.Absent => ResolveAbsentHeader(records, names),
            _ => throw new ArgumentOutOfRangeException(nameof(header), header, "CSV header mode is not supported.")
        };
    }

    private static ResolvedCsv ResolvePresentHeader(IReadOnlyList<CsvRecord> records, string[]? names)
    {
        var header = records[0];
        var sourceNames = ValidateHeaderNames(header.Fields);
        if (names is not null && names.Length != sourceNames.Length)
        {
            throw new ArgumentException($"CsvReadOptions.Names contains {names.Length} names, but the CSV header has {sourceNames.Length} columns.");
        }

        return new ResolvedCsv(names ?? sourceNames, records.Skip(1).ToArray());
    }

    private static ResolvedCsv ResolveAbsentHeader(IReadOnlyList<CsvRecord> records, string[]? names)
    {
        var columnCount = records[0].Fields.Count;
        if (columnCount == 0)
        {
            throw new ArgumentException("CSV content did not produce any usable columns.");
        }

        if (names is not null && names.Length != columnCount)
        {
            throw new ArgumentException($"CsvReadOptions.Names contains {names.Length} names, but the CSV data has {columnCount} columns.");
        }

        var columnNames = names ?? Enumerable.Range(1, columnCount).Select(static index => $"Column{index}").ToArray();
        return new ResolvedCsv(columnNames, records);
    }

    private static string[] ValidateHeaderNames(IReadOnlyList<CsvField> fields)
    {
        if (fields.Count == 0)
        {
            throw new ArgumentException("CSV header did not produce any usable columns.");
        }

        var names = new string[fields.Count];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < fields.Count; index++)
        {
            var name = fields[index].Value;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"CSV header column {index + 1} is empty or whitespace.");
            }

            if (!seen.Add(name))
            {
                throw new ArgumentException($"CSV header contains duplicate column name '{name}'.");
            }

            names[index] = name;
        }

        return names;
    }

    private static void ValidateDataRows(IReadOnlyList<CsvRecord> rows, int expectedColumnCount)
    {
        foreach (var row in rows)
        {
            if (row.Fields.Count != expectedColumnCount)
            {
                throw new ArgumentException(
                    $"CSV row starting at line {row.LineNumber} has {row.Fields.Count} fields, but {expectedColumnCount} fields were expected.");
            }
        }
    }

    private static DataFrame CreateDataFrame(IReadOnlyList<string> columnNames, IReadOnlyList<CsvRecord> rows)
    {
        if (columnNames.Count == 0)
        {
            throw new ArgumentException("CSV content did not produce any usable columns.");
        }

        var columns = new ISeries[columnNames.Count];
        for (var columnIndex = 0; columnIndex < columnNames.Count; columnIndex++)
        {
            var fields = rows.Select(row => row.Fields[columnIndex]).ToArray();
            var columnType = CsvTypeInference.Infer(fields);
            var values = fields.Select(field => CsvTypeInference.Convert(field, columnType.DataType));
            columns[columnIndex] = DataFrame.CreateSeriesFromValues(columnNames[columnIndex], columnType.DataType, values);
        }

        return DataFrame.CreateFromSeries(columns);
    }

    private static DataFrame CreateEmptyDataFrame(IReadOnlyList<string> columnNames)
    {
        var columns = columnNames
            .Select(static name => DataFrame.CreateSeriesFromValues(name, typeof(string), Array.Empty<object?>()))
            .ToArray();

        return DataFrame.CreateFromSeries(columns);
    }

    private sealed record ResolvedCsv(IReadOnlyList<string> ColumnNames, IReadOnlyList<CsvRecord> DataRows);
}
