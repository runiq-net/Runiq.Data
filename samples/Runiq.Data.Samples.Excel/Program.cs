using Runiq.Data;

var excelPath = Path.Combine(AppContext.BaseDirectory, "Data", "employees.xlsx");

Console.WriteLine("=== Default First Worksheet ===");
var employees = DataFrame.ReadExcel(excelPath);
Print(employees);
PrintSchema(employees);

Console.WriteLine();
Console.WriteLine("=== SheetName: Departments ===");
var departments = DataFrame.ReadExcel(
    excelPath,
    new ExcelReadOptions
    {
        SheetName = "Departments"
    });
Print(departments);

Console.WriteLine();
Console.WriteLine("=== SheetIndex: 1 ===");
var departmentsByIndex = DataFrame.ReadExcel(
    excelPath,
    new ExcelReadOptions
    {
        SheetIndex = 1
    });
Print(departmentsByIndex);

Console.WriteLine();
Console.WriteLine("=== Headerless Worksheet with Names ===");
var employeesWithoutHeader = DataFrame.ReadExcel(
    excelPath,
    new ExcelReadOptions
    {
        SheetName = "EmployeesWithoutHeader",
        Header = ExcelHeaderMode.Absent,
        Names = ["Name", "Department", "Age"]
    });
Print(employeesWithoutHeader);

var outputPath = Path.Combine(
    Path.GetTempPath(),
    $"runiq-data-{Guid.NewGuid():N}.xlsx");

var headerlessOutputPath = Path.Combine(
    Path.GetTempPath(),
    $"runiq-data-{Guid.NewGuid():N}.xlsx");

try
{
    Console.WriteLine();
    Console.WriteLine("=== Excel Write Round-trip: Header On, Custom Worksheet ===");
    employees.WriteExcel(
        outputPath,
        new ExcelWriteOptions
        {
            SheetName = "EmployeesExport",
            IncludeHeader = true
        });

    var reloaded = DataFrame.ReadExcel(
        outputPath,
        new ExcelReadOptions
        {
            SheetName = "EmployeesExport"
        });
    Print(reloaded);

    Console.WriteLine();
    Console.WriteLine("=== Excel Write Round-trip: Header Off ===");
    employees.WriteExcel(
        headerlessOutputPath,
        new ExcelWriteOptions
        {
            SheetName = "Employees",
            IncludeHeader = false
        });

    var headerlessReloaded = DataFrame.ReadExcel(
        headerlessOutputPath,
        new ExcelReadOptions
        {
            SheetName = "Employees",
            Header = ExcelHeaderMode.Absent,
            Names =
            [
                "Name",
                "Department",
                "Age",
                "Salary",
                "Active",
                "StartDate"
            ]
        });
    Print(headerlessReloaded);
}
finally
{
    DeleteTemporaryWorkbook(outputPath);
    DeleteTemporaryWorkbook(headerlessOutputPath);
}

static void Print(DataFrame dataFrame)
{
    var columnNames = dataFrame.Columns.Select(static column => column.Name).ToArray();
    Console.WriteLine(string.Join(" | ", columnNames));

    for (var rowIndex = 0; rowIndex < dataFrame.Rows.Count(); rowIndex++)
    {
        var values = columnNames.Select(columnName => dataFrame[columnName].GetValue(rowIndex));
        Console.WriteLine(string.Join(" | ", values.Select(static value => value?.ToString() ?? "null")));
    }
}

static void PrintSchema(DataFrame dataFrame)
{
    Console.WriteLine("Schema");
    foreach (var column in dataFrame.Schema.Columns)
    {
        Console.WriteLine($"{column.Name}: {column.DataType.Name}, Nullable: {column.IsNullable}");
    }
}

static void DeleteTemporaryWorkbook(string path)
{
    try
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
    catch
    {
        // Sample cleanup is best-effort so cleanup failures do not hide the demonstrated flow.
    }
}
