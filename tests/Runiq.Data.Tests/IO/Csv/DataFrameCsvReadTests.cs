using System.Globalization;

namespace Runiq.Data.Tests.IO.Csv;

/// <summary>
/// Verifies CSV reading through the public DataFrame API.
/// </summary>
public sealed class DataFrameCsvReadTests
{
    // Verifies that the basic overload uses a comma delimiter, consumes the first row as a header, and preserves row and column order.
    [Fact]
    public void ReadCsv_DefaultUsage_ReadsHeaderCommaDelimiterAndPreservesOrder()
    {
        using var file = CsvFile("Name,Age,Active\nAli,34,true\nAyse,29,false");

        var df = global::Runiq.Data.DataFrame.ReadCsv(file.Path);

        Assert.Equal(2, df.Rows.Count());
        Assert.Collection(df.Columns, c => Assert.Equal("Name", c.Name), c => Assert.Equal("Age", c.Name), c => Assert.Equal("Active", c.Name));
        Assert.Equal("Ali", df["Name"].GetValue(0));
        Assert.Equal("Ayse", df["Name"].GetValue(1));
        Assert.Equal(34, df["Age"].GetValue(0));
        Assert.Equal(false, df["Active"].GetValue(1));
    }

    // Verifies that a custom delimiter is honored without changing header or type inference behavior.
    [Fact]
    public void ReadCsv_WithCustomDelimiter_ReadsFields()
    {
        using var file = CsvFile("Name;Salary\nAli;125000.50\nAyse;98000.00");

        var df = global::Runiq.Data.DataFrame.ReadCsv(file.Path, new global::Runiq.Data.CsvReadOptions { Delimiter = ';' });

        Assert.Equal(typeof(decimal), df["Salary"].DataType);
        Assert.Equal(98000.00m, df["Salary"].GetValue(1));
    }

    // Verifies that files ending with either Windows, Unix, or no final line ending are read consistently.
    [Theory]
    [InlineData("Name,Age\r\nAli,34\r\nAyse,29")]
    [InlineData("Name,Age\nAli,34\nAyse,29\n")]
    [InlineData("Name,Age\nAli,34\nAyse,29")]
    public void ReadCsv_WithSupportedLineEndings_ReadsAllRows(string content)
    {
        using var file = CsvFile(content);

        var df = global::Runiq.Data.DataFrame.ReadCsv(file.Path);

        Assert.Equal(2, df.Rows.Count());
        Assert.Equal(29, df["Age"].GetValue(1));
    }

    // Verifies that Infer consumes the first row as a header when explicit names are not supplied.
    [Fact]
    public void ReadCsv_WithInferHeaderAndNoNames_UsesFirstRowAsHeader()
    {
        using var file = CsvFile("Name,Age\nAli,34");

        var df = global::Runiq.Data.DataFrame.ReadCsv(file.Path, new global::Runiq.Data.CsvReadOptions { Header = global::Runiq.Data.CsvHeaderMode.Infer });

        Assert.Equal("Name", df.Columns[0].Name);
        Assert.Equal(1, df.Rows.Count());
    }

    // Verifies that Infer with explicit names treats the first source row as data and uses the supplied names in order.
    [Fact]
    public void ReadCsv_WithInferHeaderAndNames_TreatsFirstRowAsData()
    {
        var names = new List<string> { "Name", "Age" };
        using var file = CsvFile("Ali,34\nAyse,29");

        var df = global::Runiq.Data.DataFrame.ReadCsv(file.Path, new global::Runiq.Data.CsvReadOptions { Names = names });

        Assert.Equal(new[] { "Name", "Age" }, df.Columns.Select(static c => c.Name));
        Assert.Equal("Ali", df["Name"].GetValue(0));
        Assert.Equal(new[] { "Name", "Age" }, names);
    }

    // Verifies that Present consumes the first row as metadata and can replace source names with explicit names.
    [Fact]
    public void ReadCsv_WithPresentHeaderAndNames_ReplacesColumnNames()
    {
        using var file = CsvFile("source_name,source_age\nAli,34");

        var df = global::Runiq.Data.DataFrame.ReadCsv(
            file.Path,
            new global::Runiq.Data.CsvReadOptions
            {
                Header = global::Runiq.Data.CsvHeaderMode.Present,
                Names = ["EmployeeName", "EmployeeAge"]
            });

        Assert.Equal("EmployeeName", df.Columns[0].Name);
        Assert.Equal("Ali", df["EmployeeName"].GetValue(0));
        Assert.False(df.HasColumn("source_name"));
    }

    // Verifies that Absent treats the first row as data and generates ColumnN names when no names are supplied.
    [Fact]
    public void ReadCsv_WithAbsentHeaderAndNoNames_GeneratesColumnNames()
    {
        using var file = CsvFile("Ali,34\nAyse,29");

        var df = global::Runiq.Data.DataFrame.ReadCsv(file.Path, new global::Runiq.Data.CsvReadOptions { Header = global::Runiq.Data.CsvHeaderMode.Absent });

        Assert.Equal(new[] { "Column1", "Column2" }, df.Columns.Select(static c => c.Name));
        Assert.Equal("Ali", df["Column1"].GetValue(0));
    }

    // Verifies that Absent with explicit names treats the first source row as data and applies the supplied names.
    [Fact]
    public void ReadCsv_WithAbsentHeaderAndNames_UsesNames()
    {
        using var file = CsvFile("Ali,Engineering,34");

        var df = global::Runiq.Data.DataFrame.ReadCsv(
            file.Path,
            new global::Runiq.Data.CsvReadOptions { Header = global::Runiq.Data.CsvHeaderMode.Absent, Names = ["Name", "Department", "Age"] });

        Assert.Equal("Engineering", df["Department"].GetValue(0));
    }

    // Verifies that undefined header mode values are rejected before parsing produces a DataFrame.
    [Fact]
    public void ReadCsv_WithUndefinedHeaderMode_Throws()
    {
        using var file = CsvFile("Name\nAli");

        Assert.Throws<ArgumentOutOfRangeException>(() => global::Runiq.Data.DataFrame.ReadCsv(file.Path, new global::Runiq.Data.CsvReadOptions { Header = (global::Runiq.Data.CsvHeaderMode)999 }));
    }

    // Verifies that invalid explicit name collections fail fast with diagnostic exceptions.
    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void ReadCsv_WithInvalidNames_Throws(IReadOnlyList<string?> names)
    {
        using var file = CsvFile("Ali,34");

        Assert.ThrowsAny<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadCsv(
            file.Path,
            new global::Runiq.Data.CsvReadOptions { Header = global::Runiq.Data.CsvHeaderMode.Absent, Names = names! }));
    }

    // Verifies that too few or too many explicit names are rejected against the real CSV column count.
    [Theory]
    [InlineData("One")]
    [InlineData("One,Two,Three")]
    public void ReadCsv_WithWrongNameCount_Throws(string nameCsv)
    {
        using var file = CsvFile("Ali,34");
        var names = nameCsv.Split(',');

        var exception = Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadCsv(
            file.Path,
            new global::Runiq.Data.CsvReadOptions { Header = global::Runiq.Data.CsvHeaderMode.Absent, Names = names }));

        Assert.Contains("Names", exception.Message);
    }

    // Verifies that string, Boolean, integer, long, decimal, double, mixed, nullable, and all-empty columns infer expected CLR contracts.
    [Fact]
    public void ReadCsv_InfersSupportedColumnTypesFromAllRows()
    {
        using var file = CsvFile(string.Join('\n',
            "Text,Flag,IntValue,LongValue,DecimalValue,DoubleValue,Mixed,NullableInt,Empty",
            "a,true,1,2147483648,12.50,1e40,1,10,",
            "b,false,2,2147483649,13.75,2e40,later,,"));

        var df = global::Runiq.Data.DataFrame.ReadCsv(file.Path);

        Assert.Equal(typeof(string), df["Text"].DataType);
        Assert.Equal(typeof(bool), df["Flag"].DataType);
        Assert.Equal(typeof(int), df["IntValue"].DataType);
        Assert.Equal(typeof(long), df["LongValue"].DataType);
        Assert.Equal(typeof(decimal), df["DecimalValue"].DataType);
        Assert.Equal(typeof(double), df["DoubleValue"].DataType);
        Assert.Equal(typeof(string), df["Mixed"].DataType);
        Assert.Equal(typeof(int?), df["NullableInt"].DataType);
        Assert.Equal(typeof(string), df["Empty"].DataType);
        Assert.True(df["NullableInt"].IsNullable);
        Assert.True(df["Empty"].IsNullable);
        Assert.Null(df["NullableInt"].GetValue(1));
    }

    // Verifies that numeric inference and conversion use invariant culture instead of the current thread culture.
    [Fact]
    public void ReadCsv_UsesInvariantCultureForNumericParsing()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        using var file = CsvFile("Value\n1234.50");

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            var df = global::Runiq.Data.DataFrame.ReadCsv(file.Path);

            Assert.Equal(1234.50m, df["Value"].GetValue(0));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    // Verifies that a header-only CSV creates a zero-row DataFrame with nullable string columns.
    [Fact]
    public void ReadCsv_WithHeaderOnlyFile_CreatesEmptyStringColumns()
    {
        using var file = CsvFile("Name,Age");

        var df = global::Runiq.Data.DataFrame.ReadCsv(file.Path);

        Assert.Equal(0, df.Rows.Count());
        Assert.Equal(typeof(string), df["Name"].DataType);
        Assert.True(df["Name"].IsNullable);
        Assert.Equal(typeof(string), df["Age"].DataType);
    }

    // Verifies that quoted delimiters, escaped quotes, and multiline quoted fields are parsed as field content.
    [Fact]
    public void ReadCsv_WithQuotedCsvFeatures_ReadsFieldContent()
    {
        using var file = CsvFile("Name,Note\nAli,\"hello, world\"\nAyse,\"said \"\"yes\"\"\"\nMehmet,\"line1\nline2\"");

        var df = global::Runiq.Data.DataFrame.ReadCsv(file.Path);

        Assert.Equal("hello, world", df["Note"].GetValue(0));
        Assert.Equal("said \"yes\"", df["Note"].GetValue(1));
        Assert.Equal("line1\nline2", df["Note"].GetValue(2));
    }

    // Verifies that leading, trailing, and consecutive empty unquoted fields are preserved as missing values.
    [Fact]
    public void ReadCsv_WithEmptyFields_PreservesMissingValues()
    {
        using var file = CsvFile("A,B,C\n,2,\n1,,3");

        var df = global::Runiq.Data.DataFrame.ReadCsv(file.Path);

        Assert.Null(df["A"].GetValue(0));
        Assert.Null(df["C"].GetValue(0));
        Assert.Null(df["B"].GetValue(1));
        Assert.Equal(typeof(int?), df["B"].DataType);
    }

    // Verifies that a quoted empty string remains a non-missing string while an unquoted empty cell is missing.
    [Fact]
    public void ReadCsv_WithQuotedEmptyString_DistinguishesEmptyStringFromMissing()
    {
        using var file = CsvFile("Value\n\"\"\n");

        var df = global::Runiq.Data.DataFrame.ReadCsv(file.Path);

        Assert.Equal(typeof(string), df["Value"].DataType);
        Assert.Equal(string.Empty, df["Value"].GetValue(0));
        Assert.NotNull(df["Value"].GetValue(0));
    }

    // Verifies that malformed quote placement is rejected instead of being silently normalized.
    [Theory]
    [InlineData("Name\n\"Ali\"x")]
    [InlineData("Name\nA\"li")]
    [InlineData("Name\n\"Ali")]
    public void ReadCsv_WithMalformedQuotedField_Throws(string content)
    {
        using var file = CsvFile(content);

        var exception = Assert.ThrowsAny<FormatException>(() => global::Runiq.Data.DataFrame.ReadCsv(file.Path));

        Assert.Contains("line", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Verifies that rows with too few or too many fields fail with the problem line and expected count.
    [Theory]
    [InlineData("A,B\n1")]
    [InlineData("A,B\n1,2,3")]
    public void ReadCsv_WithMismatchedDataRowShape_ThrowsWithLineAndCounts(string content)
    {
        using var file = CsvFile(content);

        var exception = Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadCsv(file.Path));

        Assert.Contains("line 2", exception.Message);
        Assert.Contains("2", exception.Message);
    }

    // Verifies that source headers reject empty, whitespace, and duplicate column names using DataFrame lookup semantics.
    [Theory]
    [InlineData(",Age\nAli,34")]
    [InlineData("   ,Age\nAli,34")]
    [InlineData("Name,name\nAli,34")]
    public void ReadCsv_WithInvalidSourceHeader_Throws(string content)
    {
        using var file = CsvFile(content);

        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadCsv(file.Path));
    }

    // Verifies that null, empty, and whitespace paths and null options are rejected by the public API.
    [Fact]
    public void ReadCsv_WithInvalidPublicArguments_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => global::Runiq.Data.DataFrame.ReadCsv(null!));
        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadCsv(string.Empty));
        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadCsv("   "));
        Assert.Throws<ArgumentNullException>(() => global::Runiq.Data.DataFrame.ReadCsv("missing.csv", null!));
    }

    // Verifies that invalid delimiters are rejected before file content is interpreted.
    [Theory]
    [InlineData('\0')]
    [InlineData('"')]
    [InlineData('\r')]
    [InlineData('\n')]
    public void ReadCsv_WithInvalidDelimiter_Throws(char delimiter)
    {
        using var file = CsvFile("Name\nAli");

        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadCsv(file.Path, new global::Runiq.Data.CsvReadOptions { Delimiter = delimiter }));
    }

    // Verifies that empty and whitespace-only files are rejected except the documented Absent plus Names empty-frame case.
    [Theory]
    [InlineData("")]
    [InlineData("   \r\n\t")]
    public void ReadCsv_WithEmptyOrWhitespaceOnlyFile_Throws(string content)
    {
        using var file = CsvFile(content);

        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadCsv(file.Path));
    }

    // Verifies that whitespace-only content remains invalid even when Absent plus Names could otherwise define columns.
    [Fact]
    public void ReadCsv_WithWhitespaceOnlyFileAbsentHeaderAndNames_Throws()
    {
        using var file = CsvFile("   \r\n\t");

        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadCsv(
            file.Path,
            new global::Runiq.Data.CsvReadOptions { Header = global::Runiq.Data.CsvHeaderMode.Absent, Names = ["Name"] }));
    }

    // Verifies that invalid Names are rejected even for the documented empty-file Absent plus Names path.
    [Fact]
    public void ReadCsv_WithEmptyFileAndInvalidNames_ThrowsNamesValidation()
    {
        using var file = CsvFile(string.Empty);

        var exception = Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadCsv(
            file.Path,
            new global::Runiq.Data.CsvReadOptions { Header = global::Runiq.Data.CsvHeaderMode.Absent, Names = [] }));

        Assert.Contains("Names", exception.Message);
    }

    // Verifies that Absent plus explicit names can create an empty DataFrame from an empty file with nullable string columns.
    [Fact]
    public void ReadCsv_WithEmptyFileAbsentHeaderAndNames_CreatesEmptyDataFrame()
    {
        using var file = CsvFile(string.Empty);

        var df = global::Runiq.Data.DataFrame.ReadCsv(
            file.Path,
            new global::Runiq.Data.CsvReadOptions { Header = global::Runiq.Data.CsvHeaderMode.Absent, Names = ["Name", "Age"] });

        Assert.Equal(0, df.Rows.Count());
        Assert.Equal(typeof(string), df["Name"].DataType);
        Assert.True(df["Age"].IsNullable);
    }

    // Verifies that missing files surface the natural file system exception instead of being wrapped.
    [Fact]
    public void ReadCsv_WithMissingFile_PreservesNaturalIoException()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.csv");

        Assert.ThrowsAny<IOException>(() => global::Runiq.Data.DataFrame.ReadCsv(path));
    }

    public static IEnumerable<object[]> InvalidNames()
    {
        yield return [Array.Empty<string>()];
        yield return [new string?[] { null }];
        yield return [new[] { "" }];
        yield return [new[] { "   " }];
        yield return [new[] { "Name", "name" }];
    }

    private static TemporaryCsvFile CsvFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "Runiq.Data.Tests", Guid.NewGuid().ToString("N") + ".csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return new TemporaryCsvFile(path);
    }

    private sealed class TemporaryCsvFile : IDisposable
    {
        internal TemporaryCsvFile(string path)
        {
            Path = path;
        }

        internal string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
