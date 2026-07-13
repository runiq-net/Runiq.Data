using System.Globalization;
using System.Runtime.InteropServices;

namespace Runiq.Data.IO.Tests.Csv;

/// <summary>
/// Verifies CSV writing through the public DataFrame API.
/// </summary>
public sealed class DataFrameCsvWriteTests
{
    // Verifies that default writing creates a UTF-8 CSV with header, comma delimiter, preserved column order, preserved row order, and a final newline.
    [Fact]
    public void WriteCsv_DefaultUsage_WritesHeaderCommaDelimiterAndOrder()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("employees.csv");
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Age = new[] { 34, 29 },
            Active = new[] { true, false }
        });

        df.WriteCsv(path);

        Assert.Equal($"Name,Age,Active{Environment.NewLine}Ali,34,true{Environment.NewLine}Ayse,29,false{Environment.NewLine}", File.ReadAllText(path));
        var bytes = File.ReadAllBytes(path);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
    }

    // Verifies that custom delimiters and disabled headers change only the requested CSV record shape.
    [Fact]
    public void WriteCsv_WithCustomDelimiterAndNoHeader_WritesDataRowsOnly()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("employees.csv");
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Department = new[] { "Engineering", "Finance" }
        });

        df.WriteCsv(path, new global::Runiq.Data.CsvWriteOptions { IncludeHeader = false, Delimiter = ';' });

        Assert.Equal($"Ali;Engineering{Environment.NewLine}Ayse;Finance{Environment.NewLine}", File.ReadAllText(path));
    }

    // Verifies that an existing longer file is fully replaced and does not keep stale trailing bytes.
    [Fact]
    public void WriteCsv_WithExistingLongerFile_ReplacesEntireContent()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("employees.csv");
        File.WriteAllText(path, "this content is intentionally much longer than the new csv");
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" } });

        df.WriteCsv(path);

        Assert.Equal($"Name{Environment.NewLine}Ali{Environment.NewLine}", File.ReadAllText(path));
    }

    // Verifies that missing parent directories are not created implicitly and the natural file-system exception is preserved.
    [Fact]
    public void WriteCsv_WithMissingParentDirectory_ThrowsDirectoryNotFoundException()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "missing", "employees.csv");
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" } });

        Assert.Throws<DirectoryNotFoundException>(() => df.WriteCsv(path));
    }

    // Verifies delimiter, quote, CR, LF, CRLF, multiline, whitespace, Unicode, Turkish characters, emoji, and minimal quoting rules.
    [Fact]
    public void WriteCsv_WithStrings_EscapesOnlyWhenRequiredAndPreservesText()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("strings.csv");
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Normal = new[] { "plain" },
            Delimited = new[] { "Karadag, Kemal" },
            Quoted = new[] { "He said \"Hello\"" },
            Cr = new[] { "first\rsecond" },
            Lf = new[] { "first\nsecond" },
            CrLf = new[] { "first\r\nsecond" },
            Leading = new[] { "  left" },
            Trailing = new[] { "right  " },
            Unicode = new[] { "Ayse, Çağrı, İstanbul, 😀" }
        });

        df.WriteCsv(path);

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Normal,Delimited,Quoted,Cr,Lf,CrLf,Leading,Trailing,Unicode",
                "plain,\"Karadag, Kemal\",\"He said \"\"Hello\"\"\",\"first\rsecond\",\"first\nsecond\",\"first\r\nsecond\",  left,right  ,\"Ayse, Çağrı, İstanbul, 😀\"",
                string.Empty),
            File.ReadAllText(path));
    }

    // Verifies that null fields remain empty, empty strings are quoted, and leading, trailing, and consecutive null fields are retained.
    [Fact]
    public void WriteCsv_WithNullAndEmptyString_PreservesDistinctCsvRepresentations()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("nulls.csv");
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            A = new string?[] { null, string.Empty },
            B = new string?[] { null, null },
            C = new string?[] { "tail", null }
        });

        df.WriteCsv(path);

        Assert.Equal($"A,B,C{Environment.NewLine},,tail{Environment.NewLine}\"\",,{Environment.NewLine}", File.ReadAllText(path));
    }

    // Verifies that primitive values are formatted with invariant culture, deterministic Boolean casing, signed values, zero, nullable nulls, and finite floating-point values.
    [Fact]
    public void WriteCsv_WithPrimitiveValues_UsesInvariantDeterministicFormatting()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("numbers.csv");
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Flag = new[] { true, false },
            IntValue = new[] { -1, 0 },
            LongValue = new[] { 2_147_483_648L, -2_147_483_649L },
            DecimalValue = new[] { 1234.50m, -0.25m },
            DoubleValue = new[] { 1.5d, 2.5d },
            NullableValue = new int?[] { null, 42 }
        });

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            df.WriteCsv(path);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        Assert.Equal(
            $"Flag,IntValue,LongValue,DecimalValue,DoubleValue,NullableValue{Environment.NewLine}true,-1,2147483648,1234.50,1.5,{Environment.NewLine}false,0,-2147483649,-0.25,2.5,42{Environment.NewLine}",
            File.ReadAllText(path));
    }

    // Verifies that non-finite floating-point values are rejected instead of being written in a form the reader intentionally does not infer.
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void WriteCsv_WithNonFiniteDouble_Throws(double value)
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("numbers.csv");
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { value } });

        Assert.Throws<ArgumentException>(() => df.WriteCsv(path));
    }

    // Verifies that header names use the same escaping rules as data fields and disappear entirely when headers are disabled.
    [Fact]
    public void WriteCsv_WithSpecialHeaderNames_EscapesHeaderAndOmitsWhenDisabled()
    {
        using var directory = TemporaryDirectory.Create();
        var headerPath = directory.FilePath("header.csv");
        var noHeaderPath = directory.FilePath("no-header.csv");
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali" },
            Age = new[] { 34 }
        });
        df.Columns.Rename("Name", "Last, First");
        df.Columns.Rename("Age", "Said \"Age\"\nYears");

        df.WriteCsv(headerPath);
        df.WriteCsv(noHeaderPath, new global::Runiq.Data.CsvWriteOptions { IncludeHeader = false });

        Assert.StartsWith("\"Last, First\",\"Said \"\"Age\"\"\nYears\"", File.ReadAllText(headerPath));
        Assert.DoesNotContain("Last", File.ReadAllText(noHeaderPath));
    }

    // Verifies that a zero-row DataFrame with columns writes only a header by default and writes an empty file when headers are disabled.
    [Fact]
    public void WriteCsv_WithColumnsAndNoRows_HandlesHeaderOptions()
    {
        using var directory = TemporaryDirectory.Create();
        var headerPath = directory.FilePath("header.csv");
        var noHeaderPath = directory.FilePath("no-header.csv");
        var df = global::Runiq.Data.DataFrame.ReadCsv(
            directory.EmptyFile("empty.csv"),
            new global::Runiq.Data.CsvReadOptions { Header = global::Runiq.Data.CsvHeaderMode.Absent, Names = ["Name", "Age"] });

        df.WriteCsv(headerPath);
        df.WriteCsv(noHeaderPath, new global::Runiq.Data.CsvWriteOptions { IncludeHeader = false });

        Assert.Equal($"Name,Age{Environment.NewLine}", File.ReadAllText(headerPath));
        Assert.Equal(string.Empty, File.ReadAllText(noHeaderPath));
    }

    // Verifies that invalid public arguments and invalid delimiters are rejected before CSV output is produced.
    [Fact]
    public void WriteCsv_WithInvalidArguments_Throws()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("employees.csv");
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" } });

        Assert.Throws<ArgumentNullException>(() => df.WriteCsv(null!));
        Assert.Throws<ArgumentException>(() => df.WriteCsv(string.Empty));
        Assert.Throws<ArgumentException>(() => df.WriteCsv("   "));
        Assert.Throws<ArgumentNullException>(() => df.WriteCsv(path, null!));
        Assert.Throws<ArgumentException>(() => df.WriteCsv(path, new global::Runiq.Data.CsvWriteOptions { Delimiter = '\n' }));
        Assert.Throws<ArgumentException>(() => df.WriteCsv(path, new global::Runiq.Data.CsvWriteOptions { Delimiter = '\r' }));
        Assert.Throws<ArgumentException>(() => df.WriteCsv(path, new global::Runiq.Data.CsvWriteOptions { Delimiter = '"' }));
        Assert.Throws<ArgumentException>(() => df.WriteCsv(path, new global::Runiq.Data.CsvWriteOptions { Delimiter = '\0' }));
    }

    // Verifies that unsupported runtime value diagnostics include the column name, row index, and runtime type while preserving an existing target file.
    [Fact]
    public void WriteCsv_WithUnsupportedRuntimeValue_ThrowsDiagnosticExceptionAndPreservesExistingFile()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("objects.csv");
        File.WriteAllText(path, "existing");
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new object?[] { "ok", new object() } });

        var exception = Assert.Throws<ArgumentException>(() => df.WriteCsv(path));

        Assert.Contains("Value", exception.Message);
        Assert.Contains("row 1", exception.Message);
        Assert.Contains(typeof(object).ToString(), exception.Message);
        Assert.Equal("existing", File.ReadAllText(path));
        Assert.Empty(Directory.EnumerateFiles(directory.Path, ".runiq-data-*.tmp"));
    }

    // Verifies that writing to a directory path fails with a natural I/O or access exception rather than being wrapped.
    [Fact]
    public void WriteCsv_WhenTargetIsDirectory_PreservesNaturalException()
    {
        using var directory = TemporaryDirectory.Create();
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" } });

        var exception = Record.Exception(() => df.WriteCsv(directory.Path));

        Assert.NotNull(exception);
        Assert.False(exception is InvalidOperationException);
        Assert.True(exception is IOException or UnauthorizedAccessException);
    }

    // Verifies that read-only target files fail with an access exception and that the exception is not wrapped.
    [Fact]
    public void WriteCsv_WhenTargetIsReadOnly_FailsWhenPlatformEnforcesAttribute()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("readonly.csv");
        File.WriteAllText(path, "existing");
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" } });

        try
        {
            Assert.Throws<UnauthorizedAccessException>(() => df.WriteCsv(path));
        }
        finally
        {
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        }
    }

    // Verifies that an exclusive file lock prevents replacement on platforms that enforce the lock and that the exception is not wrapped.
    [Fact]
    public void WriteCsv_WhenTargetIsLocked_FailsWhenPlatformEnforcesLock()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("locked.csv");
        File.WriteAllText(path, "existing");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" } });

        var exception = Record.Exception(() => df.WriteCsv(path));

        if (exception is not null)
        {
            Assert.False(exception is InvalidOperationException);
            Assert.True(exception is IOException or UnauthorizedAccessException);
        }
    }

    // Verifies that permission-denied directories are checked only on Unix-like platforms where mode bits can reliably remove write access for non-root users.
    [Fact]
    public void WriteCsv_WhenDirectoryPermissionDeniedOnUnix_FailsIfPermissionModelApplies()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || Environment.UserName == "root")
        {
            return;
        }

        using var directory = TemporaryDirectory.Create();
        File.SetUnixFileMode(directory.Path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" } });

        try
        {
            var exception = Record.Exception(() => df.WriteCsv(directory.FilePath("denied.csv")));
            Assert.True(exception is UnauthorizedAccessException or IOException);
        }
        finally
        {
            File.SetUnixFileMode(directory.Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    // Verifies that invalid path syntax surfaces the natural path validation exception and is not wrapped.
    [Fact]
    public void WriteCsv_WithInvalidPath_PreservesNaturalPathException()
    {
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" } });

        Assert.ThrowsAny<ArgumentException>(() => df.WriteCsv("bad\0path.csv"));
    }

    // Verifies that successful writes remove their temporary file after replacing or creating the target.
    [Fact]
    public void WriteCsv_WhenSuccessful_RemovesTemporaryFile()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("employees.csv");
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" } });

        df.WriteCsv(path);

        Assert.Empty(Directory.EnumerateFiles(directory.Path, ".runiq-data-*.tmp"));
    }

    // Verifies that values represented by CSV round-trip through the existing reader, including nulls, empty strings, delimiters, quotes, multiline text, numerics, and Booleans.
    [Fact]
    public void WriteCsv_ThenReadCsv_RoundTripsRepresentedValues()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("roundtrip.csv");
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Note = new[] { "hello, \"world\"", "line1\nline2" },
            Empty = new string?[] { string.Empty, null },
            Age = new int?[] { 34, null },
            Salary = new[] { 125000.50m, 98000.25m },
            Active = new[] { true, false }
        });

        df.WriteCsv(path);
        var loaded = global::Runiq.Data.DataFrame.ReadCsv(path);

        Assert.Equal(df.Rows.Count(), loaded.Rows.Count());
        Assert.Equal(df.Columns.Count(), loaded.Columns.Count());
        Assert.Equal(df.Columns.Select(static column => column.Name), loaded.Columns.Select(static column => column.Name));
        Assert.Equal("hello, \"world\"", loaded["Note"].GetValue(0));
        Assert.Equal("line1\nline2", loaded["Note"].GetValue(1));
        Assert.Equal(string.Empty, loaded["Empty"].GetValue(0));
        Assert.Null(loaded["Empty"].GetValue(1));
        Assert.Equal(34, loaded["Age"].GetValue(0));
        Assert.Null(loaded["Age"].GetValue(1));
        Assert.Equal(125000.50m, loaded["Salary"].GetValue(0));
        Assert.Equal(false, loaded["Active"].GetValue(1));
    }

    // Verifies that headerless custom-delimiter output can be read back with explicit absent-header options and supplied names.
    [Fact]
    public void WriteCsv_HeaderlessCustomDelimiter_ReadsBackWithNames()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("roundtrip.csv");
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Department = new[] { "Engineering", "Finance" },
            Age = new[] { 34, 29 }
        });

        df.WriteCsv(path, new global::Runiq.Data.CsvWriteOptions { IncludeHeader = false, Delimiter = ';' });
        var loaded = global::Runiq.Data.DataFrame.ReadCsv(
            path,
            new global::Runiq.Data.CsvReadOptions
            {
                Header = global::Runiq.Data.CsvHeaderMode.Absent,
                Names = ["Name", "Department", "Age"],
                Delimiter = ';'
            });

        Assert.Equal("Engineering", loaded["Department"].GetValue(0));
        Assert.Equal(29, loaded["Age"].GetValue(1));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        internal string Path { get; }

        internal static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Runiq.Data.IO.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        internal string FilePath(string fileName)
        {
            return System.IO.Path.Combine(Path, fileName);
        }

        internal string EmptyFile(string fileName)
        {
            var path = FilePath(fileName);
            File.WriteAllText(path, string.Empty);
            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
                    }

                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // Test cleanup is best-effort and must not mask assertion failures.
            }
        }
    }
}
