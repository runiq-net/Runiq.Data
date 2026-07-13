using System.Globalization;
using System.Runtime.InteropServices;
using ClosedXML.Excel;

namespace Runiq.Data.Tests.IO.Excel;

/// <summary>
/// Verifies Excel writing through the public DataFrame API.
/// </summary>
public sealed class DataFrameExcelWriteTests
{
    // Verifies that default writing creates one Sheet1 worksheet with headers and preserves row and column order.
    [Fact]
    public void WriteExcel_DefaultUsage_WritesSheetHeaderAndOrder()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("employees.xlsx");
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Age = new[] { 34, 29 },
            Active = new[] { true, false }
        });

        df.WriteExcel(path);

        using var workbook = new XLWorkbook(path);
        Assert.Single(workbook.Worksheets);
        var worksheet = workbook.Worksheet("Sheet1");
        Assert.Equal("Name", worksheet.Cell("A1").GetString());
        Assert.Equal("Age", worksheet.Cell("B1").GetString());
        Assert.Equal("Active", worksheet.Cell("C1").GetString());
        Assert.Equal("Ali", worksheet.Cell("A2").GetString());
        Assert.Equal(29d, worksheet.Cell("B3").GetDouble());
        Assert.False(worksheet.Cell("C3").GetBoolean());
    }

    // Verifies that explicit options use the exact worksheet name and can omit the header row.
    [Fact]
    public void WriteExcel_WithCustomSheetAndNoHeader_WritesDataAtFirstRow()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("employees.xlsx");
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali" },
            Department = new[] { "Engineering" }
        });

        df.WriteExcel(path, new global::Runiq.Data.ExcelWriteOptions { SheetName = "Employees", IncludeHeader = false });

        using var workbook = new XLWorkbook(path);
        var worksheet = workbook.Worksheet("Employees");
        Assert.Equal("Ali", worksheet.Cell("A1").GetString());
        Assert.Equal("Engineering", worksheet.Cell("B1").GetString());
        Assert.True(worksheet.Cell("A2").IsEmpty());
    }

    // Verifies native cell types, formula-like text safety, blank null cells, empty text cells, DateTime formatting, and Unicode preservation.
    [Fact]
    public void WriteExcel_WithSupportedValues_WritesNativeCellTypes()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("values.xlsx");
        var date = new DateTime(2024, 5, 6, 7, 8, 9, 123, DateTimeKind.Local);
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Text = new[] { "00123", "=1+1" },
            Empty = new string?[] { string.Empty, null },
            Unicode = new[] { "Cagri Istanbul 😀", "  trailing  " },
            Flag = new[] { true, false },
            Number = new[] { 12.5d, -3.25d },
            DecimalValue = new[] { 1234.50m, -0.25m },
            Date = new[] { date, date.AddDays(1) }
        });

        df.WriteExcel(path);

        using var workbook = new XLWorkbook(path);
        var worksheet = workbook.Worksheet("Sheet1");
        Assert.Equal(XLDataType.Text, worksheet.Cell("A2").Value.Type);
        Assert.Equal("00123", worksheet.Cell("A2").GetString());
        Assert.Equal(XLDataType.Text, worksheet.Cell("A3").Value.Type);
        Assert.Equal("=1+1", worksheet.Cell("A3").GetString());
        Assert.True(string.IsNullOrEmpty(worksheet.Cell("A3").FormulaA1));
        Assert.Equal(XLDataType.Text, worksheet.Cell("B2").Value.Type);
        Assert.Equal(string.Empty, worksheet.Cell("B2").GetString());
        Assert.Equal(XLDataType.Blank, worksheet.Cell("B3").Value.Type);
        Assert.Equal("Cagri Istanbul 😀", worksheet.Cell("C2").GetString());
        Assert.Equal("  trailing  ", worksheet.Cell("C3").GetString());
        Assert.Equal(XLDataType.Boolean, worksheet.Cell("D2").Value.Type);
        Assert.True(worksheet.Cell("D2").GetBoolean());
        Assert.Equal(XLDataType.Number, worksheet.Cell("E2").Value.Type);
        Assert.Equal(12.5d, worksheet.Cell("E2").GetDouble());
        Assert.Equal(XLDataType.Number, worksheet.Cell("F2").Value.Type);
        Assert.Equal(1234.50d, worksheet.Cell("F2").GetDouble());
        Assert.True(worksheet.Cell("G2").Value.Type is XLDataType.DateTime or XLDataType.Number);
        Assert.Contains("yyyy", worksheet.Cell("G2").Style.DateFormat.Format, StringComparison.OrdinalIgnoreCase);
    }

    // Verifies that values represented by Excel round-trip through the existing reader with column shape and values intact.
    [Fact]
    public void WriteExcel_ThenReadExcel_RoundTripsRepresentedValues()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("roundtrip.xlsx");
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Code = new[] { "00123", "true" },
            Age = new int?[] { 34, null },
            Salary = new[] { 125000.50m, 98000.25m },
            Active = new[] { true, false },
            StartDate = new[] { new DateTime(2024, 1, 2, 3, 4, 5), new DateTime(2024, 2, 3, 4, 5, 6) }
        });

        df.WriteExcel(path, new global::Runiq.Data.ExcelWriteOptions { SheetName = "Employees" });
        var loaded = global::Runiq.Data.DataFrame.ReadExcel(path, new global::Runiq.Data.ExcelReadOptions { SheetName = "Employees" });

        Assert.Equal(df.Rows.Count(), loaded.Rows.Count());
        Assert.Equal(df.Columns.Count(), loaded.Columns.Count());
        Assert.Equal(df.Columns.Select(static column => column.Name), loaded.Columns.Select(static column => column.Name));
        Assert.Equal("00123", loaded["Code"].GetValue(0));
        Assert.Equal("true", loaded["Code"].GetValue(1));
        Assert.Equal(34, loaded["Age"].GetValue(0));
        Assert.Null(loaded["Age"].GetValue(1));
        Assert.Equal(125000.50m, loaded["Salary"].GetValue(0));
        Assert.Equal(false, loaded["Active"].GetValue(1));
        Assert.Equal(DateTimeKind.Unspecified, ((DateTime)loaded["StartDate"].GetValue(0)!).Kind);
    }

    // Verifies that headerless output can be read back with absent-header options and explicit names.
    [Fact]
    public void WriteExcel_Headerless_ReadsBackWithNames()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("headerless.xlsx");
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Department = new[] { "Engineering", "Finance" },
            Age = new[] { 34, 29 }
        });

        df.WriteExcel(path, new global::Runiq.Data.ExcelWriteOptions { SheetName = "Employees", IncludeHeader = false });
        var loaded = global::Runiq.Data.DataFrame.ReadExcel(
            path,
            new global::Runiq.Data.ExcelReadOptions
            {
                SheetName = "Employees",
                Header = global::Runiq.Data.ExcelHeaderMode.Absent,
                Names = ["Name", "Department", "Age"]
            });

        Assert.Equal("Engineering", loaded["Department"].GetValue(0));
        Assert.Equal(29, loaded["Age"].GetValue(1));
    }

    // Verifies that a zero-row DataFrame with columns writes only headers or an empty worksheet depending on IncludeHeader.
    [Fact]
    public void WriteExcel_WithColumnsAndNoRows_HandlesHeaderOptions()
    {
        using var directory = TemporaryDirectory.Create();
        var headerPath = directory.FilePath("header.xlsx");
        var noHeaderPath = directory.FilePath("no-header.xlsx");
        var df = global::Runiq.Data.DataFrame.ReadCsv(
            directory.EmptyFile("empty.csv"),
            new global::Runiq.Data.CsvReadOptions { Header = global::Runiq.Data.CsvHeaderMode.Absent, Names = ["Name", "Age"] });

        df.WriteExcel(headerPath);
        df.WriteExcel(noHeaderPath, new global::Runiq.Data.ExcelWriteOptions { IncludeHeader = false });

        using var headerWorkbook = new XLWorkbook(headerPath);
        using var noHeaderWorkbook = new XLWorkbook(noHeaderPath);
        Assert.Equal("Name", headerWorkbook.Worksheet("Sheet1").Cell("A1").GetString());
        Assert.Equal("Age", headerWorkbook.Worksheet("Sheet1").Cell("B1").GetString());
        Assert.Empty(noHeaderWorkbook.Worksheet("Sheet1").CellsUsed(XLCellsUsedOptions.Contents));
    }

    // Verifies public argument, extension, and worksheet-name validation before Excel output is produced.
    [Fact]
    public void WriteExcel_WithInvalidArguments_Throws()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("employees.xlsx");
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" } });

        Assert.Throws<ArgumentNullException>(() => df.WriteExcel(null!));
        Assert.Throws<ArgumentException>(() => df.WriteExcel(string.Empty));
        Assert.Throws<ArgumentException>(() => df.WriteExcel("   "));
        Assert.Throws<ArgumentNullException>(() => df.WriteExcel(path, null!));
        Assert.Throws<NotSupportedException>(() => df.WriteExcel(directory.FilePath("employees.xls")));
        Assert.Throws<NotSupportedException>(() => df.WriteExcel(directory.FilePath("employees")));
        Assert.Throws<ArgumentException>(() => df.WriteExcel(path, new global::Runiq.Data.ExcelWriteOptions { SheetName = null! }));
        Assert.Throws<ArgumentException>(() => df.WriteExcel(path, new global::Runiq.Data.ExcelWriteOptions { SheetName = "" }));
        Assert.Throws<ArgumentException>(() => df.WriteExcel(path, new global::Runiq.Data.ExcelWriteOptions { SheetName = "   " }));
        Assert.Throws<ArgumentException>(() => df.WriteExcel(path, new global::Runiq.Data.ExcelWriteOptions { SheetName = new string('A', 32) }));
        Assert.Throws<ArgumentException>(() => df.WriteExcel(path, new global::Runiq.Data.ExcelWriteOptions { SheetName = "Bad/Name" }));
        Assert.Throws<ArgumentException>(() => df.WriteExcel(path, new global::Runiq.Data.ExcelWriteOptions { SheetName = "'Bad" }));
        Assert.Throws<ArgumentException>(() => df.WriteExcel(path, new global::Runiq.Data.ExcelWriteOptions { SheetName = "Bad'" }));
    }

    // Verifies that unsupported and unsafe values produce diagnostic exceptions and preserve an existing target workbook.
    [Fact]
    public void WriteExcel_WithUnsupportedRuntimeValue_ThrowsDiagnosticExceptionAndPreservesExistingFile()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("objects.xlsx");
        CreateWorkbook(path, "Existing", "A1", "original");
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new object?[] { "ok", new object() } });

        var exception = Assert.Throws<ArgumentException>(() => df.WriteExcel(path));

        Assert.Contains("Value", exception.Message);
        Assert.Contains("row 1", exception.Message);
        Assert.Contains(typeof(object).ToString(), exception.Message);
        using var workbook = new XLWorkbook(path);
        Assert.Equal("original", workbook.Worksheet("Existing").Cell("A1").GetString());
        Assert.Empty(Directory.EnumerateFiles(directory.Path, ".runiq-data-*.tmp.xlsx"));
    }

    // Verifies that non-finite floating-point values are rejected instead of being written as invalid Excel numbers.
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void WriteExcel_WithNonFiniteDouble_Throws(double value)
    {
        using var directory = TemporaryDirectory.Create();
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { value } });

        Assert.Throws<ArgumentException>(() => df.WriteExcel(directory.FilePath("numbers.xlsx")));
    }

    // Verifies that integral values outside Excel's safe precision range are rejected rather than silently rounded.
    [Fact]
    public void WriteExcel_WithUnsafeInteger_Throws()
    {
        using var directory = TemporaryDirectory.Create();
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { 1_000_000_000_000_000L } });

        Assert.Throws<ArgumentException>(() => df.WriteExcel(directory.FilePath("numbers.xlsx")));
    }

    // Verifies that decimal values requiring precision Excel cannot safely preserve are rejected.
    [Fact]
    public void WriteExcel_WithUnsafeDecimal_Throws()
    {
        using var directory = TemporaryDirectory.Create();
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { 0.1234567890123456789012345678m } });

        Assert.Throws<ArgumentException>(() => df.WriteExcel(directory.FilePath("numbers.xlsx")));
    }

    // Verifies that DateTime values outside Excel's supported date range are rejected.
    [Fact]
    public void WriteExcel_WithUnsupportedDate_Throws()
    {
        using var directory = TemporaryDirectory.Create();
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { new DateTime(1899, 12, 31) } });

        Assert.Throws<ArgumentException>(() => df.WriteExcel(directory.FilePath("dates.xlsx")));
    }

    // Verifies that existing longer workbook content is fully replaced and old worksheets are not preserved.
    [Fact]
    public void WriteExcel_WithExistingWorkbook_ReplacesEntireWorkbook()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("employees.xlsx");
        CreateWorkbook(path, "Old", "A1", "old");
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" } });

        df.WriteExcel(path);

        using var workbook = new XLWorkbook(path);
        Assert.Single(workbook.Worksheets);
        Assert.True(workbook.Worksheets.TryGetWorksheet("Sheet1", out _));
        Assert.False(workbook.Worksheets.TryGetWorksheet("Old", out _));
    }

    // Verifies that missing parent directories are not created implicitly and the natural file-system exception is preserved.
    [Fact]
    public void WriteExcel_WithMissingParentDirectory_ThrowsDirectoryNotFoundException()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "missing", "employees.xlsx");
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" } });

        Assert.Throws<DirectoryNotFoundException>(() => df.WriteExcel(path));
    }

    // Verifies that writing to a directory path fails without wrapping the natural path, access, or support exception.
    [Fact]
    public void WriteExcel_WhenTargetIsDirectory_PreservesNaturalException()
    {
        using var directory = TemporaryDirectory.Create();
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" } });

        var exception = Record.Exception(() => df.WriteExcel(directory.Path));

        Assert.NotNull(exception);
        Assert.False(exception is InvalidOperationException);
        Assert.True(exception is NotSupportedException or IOException or UnauthorizedAccessException);
    }

    // Verifies that read-only target files fail with an access exception and that the exception is not wrapped.
    [Fact]
    public void WriteExcel_WhenTargetIsReadOnly_FailsWhenPlatformEnforcesAttribute()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("readonly.xlsx");
        CreateWorkbook(path, "Existing", "A1", "original");
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" } });

        try
        {
            Assert.Throws<UnauthorizedAccessException>(() => df.WriteExcel(path));
        }
        finally
        {
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        }
    }

    // Verifies that an exclusive file lock prevents replacement on platforms that enforce the lock and that the exception is not wrapped.
    [Fact]
    public void WriteExcel_WhenTargetIsLocked_FailsWhenPlatformEnforcesLock()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("locked.xlsx");
        CreateWorkbook(path, "Existing", "A1", "original");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" } });

        var exception = Record.Exception(() => df.WriteExcel(path));

        if (exception is not null)
        {
            Assert.False(exception is InvalidOperationException);
            Assert.True(exception is IOException or UnauthorizedAccessException);
        }
    }

    // Verifies that permission-denied directories are checked only where mode bits can reliably remove write access for non-root users.
    [Fact]
    public void WriteExcel_WhenDirectoryPermissionDeniedOnUnix_FailsIfPermissionModelApplies()
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
            var exception = Record.Exception(() => df.WriteExcel(directory.FilePath("denied.xlsx")));
            Assert.True(exception is UnauthorizedAccessException or IOException);
        }
        finally
        {
            File.SetUnixFileMode(directory.Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    // Verifies that invalid path syntax surfaces the natural path validation exception and is not wrapped.
    [Fact]
    public void WriteExcel_WithInvalidPath_PreservesNaturalPathException()
    {
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" } });

        Assert.ThrowsAny<ArgumentException>(() => df.WriteExcel("bad\0path.xlsx"));
    }

    // Verifies that successful writes remove their temporary file after replacing or creating the target.
    [Fact]
    public void WriteExcel_WhenSuccessful_RemovesTemporaryFile()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("employees.xlsx");
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" } });

        df.WriteExcel(path);

        Assert.Empty(Directory.EnumerateFiles(directory.Path, ".runiq-data-*.tmp.xlsx"));
    }

    // Verifies that Excel writing does not use current culture when serializing native numeric cells.
    [Fact]
    public void WriteExcel_WithCurrentCulture_WritesNativeNumbers()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("numbers.xlsx");
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { 1234.5d } });

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            df.WriteExcel(path);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        using var workbook = new XLWorkbook(path);
        Assert.Equal(XLDataType.Number, workbook.Worksheet("Sheet1").Cell("A2").Value.Type);
        Assert.Equal(1234.5d, workbook.Worksheet("Sheet1").Cell("A2").GetDouble());
    }

    private static void CreateWorkbook(string path, string sheetName, string cellAddress, string value)
    {
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet(sheetName).Cell(cellAddress).Value = value;
        workbook.SaveAs(path);
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
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Runiq.Data.Tests", Guid.NewGuid().ToString("N"));
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
