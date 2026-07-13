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
