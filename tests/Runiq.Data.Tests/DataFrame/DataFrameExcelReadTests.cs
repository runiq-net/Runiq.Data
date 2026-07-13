using ClosedXML.Excel;

namespace Runiq.Data.Tests.DataFrame;

/// <summary>
/// Verifies Excel reading through the public DataFrame API.
/// </summary>
public sealed class DataFrameExcelReadTests
{
    // Verifies that the basic overload reads the first worksheet, consumes the first used row as a header, and preserves native values.
    [Fact]
    public void ReadExcel_DefaultUsage_ReadsFirstWorksheetWithHeader()
    {
        using var file = ExcelFile(workbook =>
        {
            var ws = workbook.AddWorksheet("Employees");
            ws.Cell("A1").Value = "Name";
            ws.Cell("B1").Value = "Age";
            ws.Cell("C1").Value = "Active";
            ws.Cell("A2").Value = "Ali";
            ws.Cell("B2").Value = 34;
            ws.Cell("C2").Value = true;
            workbook.AddWorksheet("Departments").Cell("A1").Value = "Department";
        });

        var df = global::Runiq.Data.DataFrame.ReadExcel(file.Path);

        Assert.Equal(1, df.Rows.Count());
        Assert.Equal(new[] { "Name", "Age", "Active" }, df.Columns.Select(static c => c.Name));
        Assert.Equal("Ali", df["Name"].GetValue(0));
        Assert.Equal(34, df["Age"].GetValue(0));
        Assert.Equal(true, df["Active"].GetValue(0));
    }

    // Verifies that worksheet names are matched exactly and hidden worksheets remain readable when selected by name.
    [Fact]
    public void ReadExcel_WithSheetName_ReadsHiddenWorksheet()
    {
        using var file = ExcelFile(workbook =>
        {
            workbook.AddWorksheet("Visible").Cell("A1").Value = "Ignored";
            var hidden = workbook.AddWorksheet("Employees");
            hidden.Visibility = XLWorksheetVisibility.Hidden;
            hidden.Cell("A1").Value = "Name";
            hidden.Cell("A2").Value = "Ayse";
        });

        var df = global::Runiq.Data.DataFrame.ReadExcel(file.Path, new global::Runiq.Data.ExcelReadOptions { SheetName = "Employees" });

        Assert.Equal("Ayse", df["Name"].GetValue(0));
    }

    // Verifies that zero-based worksheet indexes select the expected worksheet.
    [Fact]
    public void ReadExcel_WithSheetIndex_ReadsZeroBasedWorksheet()
    {
        using var file = ExcelFile(workbook =>
        {
            workbook.AddWorksheet("First").Cell("A1").Value = "Ignored";
            var second = workbook.AddWorksheet("Second");
            second.Cell("A1").Value = "Name";
            second.Cell("A2").Value = "Mehmet";
        });

        var df = global::Runiq.Data.DataFrame.ReadExcel(file.Path, new global::Runiq.Data.ExcelReadOptions { SheetIndex = 1 });

        Assert.Equal("Mehmet", df["Name"].GetValue(0));
    }

    // Verifies that Infer with explicit names treats the first used row as data and keeps the caller collection unchanged.
    [Fact]
    public void ReadExcel_WithInferHeaderAndNames_TreatsFirstRowAsData()
    {
        var names = new List<string> { "Name", "Age" };
        using var file = ExcelFile(workbook =>
        {
            var ws = workbook.AddWorksheet("Sheet1");
            ws.Cell("A1").Value = "Ali";
            ws.Cell("B1").Value = 34;
        });

        var df = global::Runiq.Data.DataFrame.ReadExcel(file.Path, new global::Runiq.Data.ExcelReadOptions { Names = names });

        Assert.Equal(new[] { "Name", "Age" }, df.Columns.Select(static c => c.Name));
        Assert.Equal("Ali", df["Name"].GetValue(0));
        Assert.Equal(new[] { "Name", "Age" }, names);
    }

    // Verifies that Present consumes the source header while explicit names replace the DataFrame column names.
    [Fact]
    public void ReadExcel_WithPresentHeaderAndNames_ReplacesColumnNames()
    {
        using var file = ExcelFile(workbook =>
        {
            var ws = workbook.AddWorksheet("Sheet1");
            ws.Cell("A1").Value = "source_name";
            ws.Cell("B1").Value = "source_age";
            ws.Cell("A2").Value = "Ali";
            ws.Cell("B2").Value = 34;
        });

        var df = global::Runiq.Data.DataFrame.ReadExcel(
            file.Path,
            new global::Runiq.Data.ExcelReadOptions
            {
                Header = global::Runiq.Data.ExcelHeaderMode.Present,
                Names = ["EmployeeName", "EmployeeAge"]
            });

        Assert.Equal("EmployeeName", df.Columns[0].Name);
        Assert.Equal("Ali", df["EmployeeName"].GetValue(0));
        Assert.False(df.HasColumn("source_name"));
    }

    // Verifies that Absent treats the first used row as data and generates ColumnN names when no names are supplied.
    [Fact]
    public void ReadExcel_WithAbsentHeaderAndNoNames_GeneratesColumnNames()
    {
        using var file = ExcelFile(workbook =>
        {
            var ws = workbook.AddWorksheet("Sheet1");
            ws.Cell("A1").Value = "Ali";
            ws.Cell("B1").Value = 34;
        });

        var df = global::Runiq.Data.DataFrame.ReadExcel(file.Path, new global::Runiq.Data.ExcelReadOptions { Header = global::Runiq.Data.ExcelHeaderMode.Absent });

        Assert.Equal(new[] { "Column1", "Column2" }, df.Columns.Select(static c => c.Name));
        Assert.Equal("Ali", df["Column1"].GetValue(0));
    }

    // Verifies that validation rejects conflicting worksheet selectors, invalid names, and invalid header modes.
    [Fact]
    public void ReadExcel_WithInvalidOptions_Throws()
    {
        using var file = ExcelFile(workbook => workbook.AddWorksheet("Sheet1").Cell("A1").Value = "Name");

        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadExcel(file.Path, new global::Runiq.Data.ExcelReadOptions { SheetName = "Sheet1", SheetIndex = 0 }));
        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadExcel(file.Path, new global::Runiq.Data.ExcelReadOptions { SheetName = "   " }));
        Assert.Throws<ArgumentOutOfRangeException>(() => global::Runiq.Data.DataFrame.ReadExcel(file.Path, new global::Runiq.Data.ExcelReadOptions { SheetIndex = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => global::Runiq.Data.DataFrame.ReadExcel(file.Path, new global::Runiq.Data.ExcelReadOptions { Header = (global::Runiq.Data.ExcelHeaderMode)999 }));
        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadExcel(file.Path, new global::Runiq.Data.ExcelReadOptions { Header = global::Runiq.Data.ExcelHeaderMode.Absent, Names = [] }));
        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadExcel(file.Path, new global::Runiq.Data.ExcelReadOptions { Header = global::Runiq.Data.ExcelHeaderMode.Absent, Names = ["Name", "name"] }));
    }

    // Verifies that invalid worksheet references include the requested selector in diagnostic exceptions.
    [Fact]
    public void ReadExcel_WithMissingWorksheet_ThrowsDiagnosticException()
    {
        using var file = ExcelFile(workbook => workbook.AddWorksheet("Sheet1").Cell("A1").Value = "Name");

        var nameException = Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadExcel(file.Path, new global::Runiq.Data.ExcelReadOptions { SheetName = "Missing" }));
        var indexException = Assert.Throws<ArgumentOutOfRangeException>(() => global::Runiq.Data.DataFrame.ReadExcel(file.Path, new global::Runiq.Data.ExcelReadOptions { SheetIndex = 3 }));

        Assert.Contains("Missing", nameException.Message);
        Assert.Contains("3", indexException.Message);
        Assert.Contains("1", indexException.Message);
    }

    // Verifies that blank, whitespace, duplicate, and merged source headers are rejected.
    [Fact]
    public void ReadExcel_WithInvalidSourceHeader_Throws()
    {
        using var blank = ExcelFile(workbook => workbook.AddWorksheet("Sheet1").Cell("A1").Value = " ");
        using var duplicate = ExcelFile(workbook =>
        {
            var ws = workbook.AddWorksheet("Sheet1");
            ws.Cell("A1").Value = "Name";
            ws.Cell("B1").Value = "name";
        });
        using var merged = ExcelFile(workbook =>
        {
            var ws = workbook.AddWorksheet("Sheet1");
            ws.Range("A1:B1").Merge();
            ws.Cell("A1").Value = "Name";
            ws.Cell("A2").Value = "Ali";
            ws.Cell("B2").Value = "Engineering";
        });

        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadExcel(blank.Path));
        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadExcel(duplicate.Path));
        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadExcel(merged.Path));
    }

    // Verifies that non-text headers are converted deterministically without trimming text headers.
    [Fact]
    public void ReadExcel_WithNonTextHeaders_ConvertsHeaderNamesInvariantly()
    {
        using var file = ExcelFile(workbook =>
        {
            var ws = workbook.AddWorksheet("Sheet1");
            ws.Cell("A1").Value = 123;
            ws.Cell("B1").Value = true;
            ws.Cell("C1").Value = new DateTime(2024, 1, 2, 3, 4, 5);
            ws.Cell("D1").Value = " Name ";
            ws.Cell("A2").Value = 1;
            ws.Cell("B2").Value = false;
            ws.Cell("C2").Value = new DateTime(2024, 1, 3);
            ws.Cell("D2").Value = "Ali";
        });

        var df = global::Runiq.Data.DataFrame.ReadExcel(file.Path);

        Assert.Equal("123", df.Columns[0].Name);
        Assert.Equal("true", df.Columns[1].Name);
        Assert.Equal("2024-01-02T03:04:05.0000000", df.Columns[2].Name);
        Assert.Equal(" Name ", df.Columns[3].Name);
    }

    // Verifies that the resolved rectangular range starts at the first content cell and ignores formatting-only trailing cells.
    [Fact]
    public void ReadExcel_WithOffsetRangeAndFormattingOnlyCells_PreservesRectangularShape()
    {
        using var file = ExcelFile(workbook =>
        {
            var ws = workbook.AddWorksheet("Sheet1");
            ws.Cell("A1").Style.Fill.BackgroundColor = XLColor.Yellow;
            ws.Cell("B3").Value = "Name";
            ws.Cell("C3").Value = "Age";
            ws.Cell("B4").Value = "Ali";
            ws.Cell("D5").Value = "Engineering";
            ws.Cell("H20").Style.Fill.BackgroundColor = XLColor.Red;
        });

        var df = global::Runiq.Data.DataFrame.ReadExcel(
            file.Path,
            new global::Runiq.Data.ExcelReadOptions
            {
                Header = global::Runiq.Data.ExcelHeaderMode.Absent,
                Names = ["Name", "Age", "Department"]
            });

        Assert.Equal(new[] { "Name", "Age", "Department" }, df.Columns.Select(static c => c.Name));
        Assert.Equal(3, df.Rows.Count());
        Assert.Equal("Name", df["Name"].GetValue(0));
        Assert.Null(df["Age"].GetValue(1));
        Assert.Equal("Engineering", df["Department"].GetValue(2));
    }

    // Verifies native text, Boolean, numeric, DateTime, Unicode, whitespace, and nullable inference behavior.
    [Fact]
    public void ReadExcel_InfersSupportedColumnTypesFromNativeCells()
    {
        using var file = ExcelFile(workbook =>
        {
            var ws = workbook.AddWorksheet("Sheet1");
            var headers = new[] { "Text", "Flag", "IntValue", "LongValue", "DecimalValue", "DoubleValue", "Date", "NullableInt", "Empty" };
            for (var index = 0; index < headers.Length; index++)
            {
                ws.Cell(1, index + 1).Value = headers[index];
            }

            ws.Cell("A2").SetValue("  00123 ");
            ws.Cell("A3").SetValue("Çağrı 😀");
            ws.Cell("B2").Value = true;
            ws.Cell("B3").Value = false;
            ws.Cell("C2").Value = 1;
            ws.Cell("C3").Value = 2;
            ws.Cell("D2").Value = 2147483648d;
            ws.Cell("D3").Value = 2147483649d;
            ws.Cell("E2").Value = 12.5;
            ws.Cell("E3").Value = 13.75;
            ws.Cell("F2").Value = 1e40;
            ws.Cell("F3").Value = 2e40;
            ws.Cell("G2").Value = new DateTime(2022, 3, 1);
            ws.Cell("G3").Value = new DateTime(2023, 6, 15);
            ws.Cell("G3").Value = new DateTime(2023, 6, 15).ToOADate();
            ws.Cell("G3").Style.DateFormat.Format = "yyyy-mm-dd";
            ws.Cell("H2").Value = 10;
        });

        var df = global::Runiq.Data.DataFrame.ReadExcel(file.Path);

        Assert.Equal(typeof(string), df["Text"].DataType);
        Assert.Equal("  00123 ", df["Text"].GetValue(0));
        Assert.Equal("Çağrı 😀", df["Text"].GetValue(1));
        Assert.Equal(typeof(bool), df["Flag"].DataType);
        Assert.Equal(typeof(int), df["IntValue"].DataType);
        Assert.Equal(typeof(long), df["LongValue"].DataType);
        Assert.Equal(typeof(decimal), df["DecimalValue"].DataType);
        Assert.Equal(typeof(double), df["DoubleValue"].DataType);
        Assert.Equal(typeof(DateTime), df["Date"].DataType);
        Assert.Equal(DateTimeKind.Unspecified, ((DateTime)df["Date"].GetValue(0)!).Kind);
        Assert.Equal(typeof(int?), df["NullableInt"].DataType);
        Assert.True(df["NullableInt"].IsNullable);
        Assert.Equal(typeof(string), df["Empty"].DataType);
        Assert.True(df["Empty"].IsNullable);
    }

    // Verifies that mixed native cell types fall back to deterministic invariant strings.
    [Fact]
    public void ReadExcel_WithMixedTypes_FallsBackToString()
    {
        using var file = ExcelFile(workbook =>
        {
            var ws = workbook.AddWorksheet("Sheet1");
            ws.Cell("A1").Value = "Mixed";
            ws.Cell("A2").Value = 100;
            ws.Cell("A3").Value = "ABC";
            ws.Cell("B1").Value = "DateNumeric";
            ws.Cell("B2").Value = new DateTime(2024, 1, 2);
            ws.Cell("B3").Value = 10;
        });

        var df = global::Runiq.Data.DataFrame.ReadExcel(file.Path);

        Assert.Equal(typeof(string), df["Mixed"].DataType);
        Assert.Equal("100", df["Mixed"].GetValue(0));
        Assert.Equal("ABC", df["Mixed"].GetValue(1));
        Assert.Equal(typeof(string), df["DateNumeric"].DataType);
        Assert.Equal("2024-01-02T00:00:00.0000000", df["DateNumeric"].GetValue(0));
    }

    // Verifies that formula cached results are read while formula text is never returned as the cell value.
    [Fact]
    public void ReadExcel_WithFormulaCachedResult_ReadsResult()
    {
        using var file = ExcelFile(workbook =>
        {
            var ws = workbook.AddWorksheet("Sheet1");
            ws.Cell("A1").Value = "Total";
            ws.Cell("A2").Value = 1;
            ws.Cell("A3").Value = 2;
            ws.Cell("A4").FormulaA1 = "SUM(A2:A3)";
            ws.Cell("A4").Value = 3;
        });

        var df = global::Runiq.Data.DataFrame.ReadExcel(file.Path);

        Assert.Equal(3, df["Total"].GetValue(2));
        Assert.NotEqual("SUM(A2:A3)", df["Total"].GetValue(2));
    }

    // Verifies that Excel error cells fail with worksheet, address, and error information.
    [Fact]
    public void ReadExcel_WithErrorCell_ThrowsDiagnosticException()
    {
        using var file = ExcelFile(workbook =>
        {
            var ws = workbook.AddWorksheet("Sheet1");
            ws.Cell("A1").Value = "Value";
            ws.Cell("A2").Value = XLError.DivisionByZero;
        });

        var exception = Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadExcel(file.Path));

        Assert.Contains("Sheet1", exception.Message);
        Assert.Contains("A2", exception.Message);
        Assert.Contains("DivisionByZero", exception.Message);
    }

    // Verifies that merged data cells keep only the top-left value and do not propagate it across the merge range.
    [Fact]
    public void ReadExcel_WithMergedDataCells_ReadsOnlyTopLeftValue()
    {
        using var file = ExcelFile(workbook =>
        {
            var ws = workbook.AddWorksheet("Sheet1");
            ws.Cell("A1").Value = "Name";
            ws.Cell("B1").Value = "Department";
            ws.Range("A2:B2").Merge();
            ws.Cell("A2").Value = "Ali";
        });

        var df = global::Runiq.Data.DataFrame.ReadExcel(file.Path);

        Assert.Equal("Ali", df["Name"].GetValue(0));
        Assert.Null(df["Department"].GetValue(0));
    }

    // Verifies documented empty worksheet and header-only worksheet behavior.
    [Fact]
    public void ReadExcel_WithEmptyWorksheet_UsesNamesOrRejectsMissingNames()
    {
        using var file = ExcelFile(workbook => workbook.AddWorksheet("Sheet1"));

        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadExcel(file.Path));

        var df = global::Runiq.Data.DataFrame.ReadExcel(
            file.Path,
            new global::Runiq.Data.ExcelReadOptions { Header = global::Runiq.Data.ExcelHeaderMode.Absent, Names = ["Name", "Age"] });

        Assert.Equal(0, df.Rows.Count());
        Assert.Equal(typeof(string), df["Name"].DataType);
        Assert.True(df["Age"].IsNullable);
    }

    // Verifies that a header-only worksheet creates a zero-row DataFrame with nullable string columns.
    [Fact]
    public void ReadExcel_WithHeaderOnlyWorksheet_CreatesEmptyStringColumns()
    {
        using var file = ExcelFile(workbook =>
        {
            var ws = workbook.AddWorksheet("Sheet1");
            ws.Cell("A1").Value = "Name";
            ws.Cell("B1").Value = "Age";
        });

        var df = global::Runiq.Data.DataFrame.ReadExcel(file.Path);

        Assert.Equal(0, df.Rows.Count());
        Assert.Equal(typeof(string), df["Name"].DataType);
        Assert.True(df["Age"].IsNullable);
    }

    // Verifies public argument and unsupported extension validation without wrapping natural missing-file exceptions.
    [Fact]
    public void ReadExcel_WithInvalidPublicArguments_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => global::Runiq.Data.DataFrame.ReadExcel(null!));
        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadExcel(string.Empty));
        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadExcel("   "));
        Assert.Throws<ArgumentNullException>(() => global::Runiq.Data.DataFrame.ReadExcel("missing.xlsx", null!));
        Assert.ThrowsAny<IOException>(() => global::Runiq.Data.DataFrame.ReadExcel(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.xlsx")));
        Assert.Throws<NotSupportedException>(() => global::Runiq.Data.DataFrame.ReadExcel(Path.Combine(Path.GetTempPath(), "legacy.xls")));
    }

    // Verifies that locked workbooks preserve the natural file system failure instead of being wrapped.
    [Fact]
    public void ReadExcel_WithLockedWorkbook_PreservesNaturalIoException()
    {
        using var file = ExcelFile(workbook =>
        {
            var ws = workbook.AddWorksheet("Sheet1");
            ws.Cell("A1").Value = "Name";
            ws.Cell("A2").Value = "Ali";
        });
        using var stream = File.Open(file.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.ThrowsAny<IOException>(() => global::Runiq.Data.DataFrame.ReadExcel(file.Path));
    }

    // Verifies that read-only workbooks can still be opened for reading and cleanup restores attributes.
    [Fact]
    public void ReadExcel_WithReadOnlyWorkbook_ReadsWorkbook()
    {
        using var file = ExcelFile(workbook =>
        {
            var ws = workbook.AddWorksheet("Sheet1");
            ws.Cell("A1").Value = "Name";
            ws.Cell("A2").Value = "Ali";
        });
        File.SetAttributes(file.Path, File.GetAttributes(file.Path) | FileAttributes.ReadOnly);

        var df = global::Runiq.Data.DataFrame.ReadExcel(file.Path);

        Assert.Equal("Ali", df["Name"].GetValue(0));
    }

    // Verifies that corrupt workbook content is rejected by the workbook loader rather than normalized.
    [Fact]
    public void ReadExcel_WithCorruptWorkbook_Throws()
    {
        using var file = TemporaryFile(".xlsx", path => File.WriteAllText(path, "not an xlsx"));

        Assert.ThrowsAny<Exception>(() => global::Runiq.Data.DataFrame.ReadExcel(file.Path));
    }

    private static TemporaryExcelFile ExcelFile(Action<XLWorkbook> configure)
    {
        return TemporaryFile(".xlsx", path =>
        {
            using var workbook = new XLWorkbook();
            configure(workbook);
            workbook.SaveAs(path);
        });
    }

    private static TemporaryExcelFile TemporaryFile(string extension, Action<string> write)
    {
        var directory = Path.Combine(Path.GetTempPath(), "Runiq.Data.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "workbook" + extension);
        write(path);
        return new TemporaryExcelFile(directory, path);
    }

    private sealed class TemporaryExcelFile : IDisposable
    {
        internal TemporaryExcelFile(string directory, string path)
        {
            Directory = directory;
            Path = path;
        }

        internal string Directory { get; }

        internal string Path { get; }

        public void Dispose()
        {
            try
            {
                if (System.IO.Directory.Exists(Directory))
                {
                    File.SetAttributes(Path, FileAttributes.Normal);
                    System.IO.Directory.Delete(Directory, recursive: true);
                }
            }
            catch
            {
                // Test cleanup is best-effort so cleanup failures do not hide assertion failures.
            }
        }
    }
}
