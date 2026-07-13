using System.Text;

namespace Runiq.Data.IO;

/// <summary>
/// Parses RFC 4180-style CSV records including quoted delimiters, escaped quotes, and multiline quoted fields.
/// </summary>
internal sealed class CsvParser
{
    private readonly char delimiter;

    internal CsvParser(char delimiter)
    {
        this.delimiter = delimiter;
    }

    internal IReadOnlyList<CsvRecord> Parse(string content)
    {
        var records = new List<CsvRecord>();
        var fields = new List<CsvField>();
        var field = new StringBuilder();
        var inQuotes = false;
        var fieldWasQuoted = false;
        var afterClosingQuote = false;
        var fieldStarted = false;
        var recordLine = 1;
        var line = 1;

        for (var index = 0; index < content.Length; index++)
        {
            var current = content[index];

            if (inQuotes)
            {
                if (current == '"')
                {
                    if (index + 1 < content.Length && content[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                        afterClosingQuote = true;
                    }

                    continue;
                }

                if (current == '\r')
                {
                    field.Append(current);
                    if (index + 1 < content.Length && content[index + 1] == '\n')
                    {
                        field.Append('\n');
                        index++;
                    }

                    line++;
                    continue;
                }

                if (current == '\n')
                {
                    field.Append(current);
                    line++;
                    continue;
                }

                field.Append(current);
                continue;
            }

            if (afterClosingQuote)
            {
                if (current == delimiter)
                {
                    AddField(fields, field, fieldWasQuoted);
                    fieldWasQuoted = false;
                    afterClosingQuote = false;
                    fieldStarted = false;
                    continue;
                }

                if (current == '\r' || current == '\n')
                {
                    AddField(fields, field, fieldWasQuoted);
                    records.Add(new CsvRecord(fields.ToArray(), recordLine));
                    fields.Clear();
                    fieldWasQuoted = false;
                    afterClosingQuote = false;
                    fieldStarted = false;

                    if (current == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                    {
                        index++;
                    }

                    line++;
                    recordLine = line;
                    continue;
                }

                throw new CsvParseException($"Quote character is followed by invalid character '{current}' at line {line}.");
            }

            if (current == '"')
            {
                if (fieldStarted || field.Length > 0)
                {
                    throw new CsvParseException($"Quote character appears in an invalid position at line {line}.");
                }

                inQuotes = true;
                fieldWasQuoted = true;
                fieldStarted = true;
                continue;
            }

            if (current == delimiter)
            {
                AddField(fields, field, fieldWasQuoted);
                fieldWasQuoted = false;
                fieldStarted = false;
                continue;
            }

            if (current == '\r' || current == '\n')
            {
                AddField(fields, field, fieldWasQuoted);
                records.Add(new CsvRecord(fields.ToArray(), recordLine));
                fields.Clear();
                fieldWasQuoted = false;
                fieldStarted = false;

                if (current == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                {
                    index++;
                }

                line++;
                recordLine = line;
                continue;
            }

            field.Append(current);
            fieldStarted = true;
        }

        if (inQuotes)
        {
            throw new CsvParseException($"Quoted field starting before line {line} is not closed before the end of the file.");
        }

        if (afterClosingQuote || fieldStarted || field.Length > 0 || fieldWasQuoted || fields.Count > 0)
        {
            AddField(fields, field, fieldWasQuoted);
            records.Add(new CsvRecord(fields.ToArray(), recordLine));
        }

        return records;
    }

    private static void AddField(List<CsvField> fields, StringBuilder field, bool wasQuoted)
    {
        fields.Add(new CsvField(field.ToString(), wasQuoted));
        field.Clear();
    }
}
