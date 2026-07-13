using ClosedXML.Excel;
using Runiq.Data;

Console.WriteLine("Runiq.Data samples");
Console.WriteLine();

var csvPath = Path.Combine(AppContext.BaseDirectory, "Data", "employees.csv");
var csvEmployees = DataFrame.ReadCsv(csvPath);

Console.WriteLine("CSV employees with header");
Print(csvEmployees);
Console.WriteLine();

var csvWriteDirectory = Path.Combine(Path.GetTempPath(), "Runiq.Data.Samples", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(csvWriteDirectory);
try
{
    var csvWriteInputPath = Path.Combine(csvWriteDirectory, "employees-input.csv");
    var csvWriteOutputPath = Path.Combine(csvWriteDirectory, "employees-copy.csv");
    var headerlessCsvWriteOutputPath = Path.Combine(csvWriteDirectory, "employees-headerless.csv");

    File.WriteAllText(
        csvWriteInputPath,
        string.Join(Environment.NewLine,
            "Name,Department,Age,Salary,Active",
            "Ali,Engineering,34,125000.50,true",
            "Ayşe,Finance,,98000.25,false",
            "Çağrı,Sales,29,87500.00,true",
            string.Empty));

    var employeesFromCsv = DataFrame.ReadCsv(csvWriteInputPath);
    employeesFromCsv.WriteCsv(csvWriteOutputPath);
    var reloadedEmployees = DataFrame.ReadCsv(csvWriteOutputPath);

    Console.WriteLine($"CSV write output: {csvWriteOutputPath}");
    Console.WriteLine("CSV write round-trip with header");
    Print(reloadedEmployees);
    Console.WriteLine();

    employeesFromCsv.WriteCsv(
        headerlessCsvWriteOutputPath,
        new CsvWriteOptions
        {
            IncludeHeader = false,
            Delimiter = ';'
        });

    var headerlessEmployees = DataFrame.ReadCsv(
        headerlessCsvWriteOutputPath,
        new CsvReadOptions
        {
            Header = CsvHeaderMode.Absent,
            Names = ["Name", "Department", "Age", "Salary", "Active"],
            Delimiter = ';'
        });

    Console.WriteLine($"CSV write headerless semicolon output: {headerlessCsvWriteOutputPath}");
    Console.WriteLine("CSV write headerless reload using Names");
    Print(headerlessEmployees);
    Console.WriteLine();
}
finally
{
    try
    {
        if (Directory.Exists(csvWriteDirectory))
        {
            Directory.Delete(csvWriteDirectory, recursive: true);
        }
    }
    catch
    {
        // Sample cleanup is best-effort so the displayed CSV write behavior remains the primary result.
    }
}

var headerlessCsvPath = Path.Combine(AppContext.BaseDirectory, "Data", "employees-without-header.csv");
var employeesWithoutHeader = DataFrame.ReadCsv(
    headerlessCsvPath,
    new CsvReadOptions
    {
        Header = CsvHeaderMode.Absent,
        Names = ["Name", "Department", "Age"]
    });

Console.WriteLine("CSV employees without header using Names");
Print(employeesWithoutHeader);
Console.WriteLine();

var excelDirectory = Path.Combine(Path.GetTempPath(), "Runiq.Data.Samples", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(excelDirectory);
try
{
    var excelPath = Path.Combine(excelDirectory, "employees.xlsx");
    CreateExcelSampleWorkbook(excelPath);

    var excelEmployees = DataFrame.ReadExcel(excelPath);
    Console.WriteLine("Excel employees default first worksheet");
    Print(excelEmployees);
    Console.WriteLine();

    var excelDepartments = DataFrame.ReadExcel(
        excelPath,
        new ExcelReadOptions
        {
            SheetName = "Departments"
        });

    Console.WriteLine("Excel departments by worksheet name");
    Print(excelDepartments);
    Console.WriteLine();

    var excelEmployeesWithoutHeader = DataFrame.ReadExcel(
        excelPath,
        new ExcelReadOptions
        {
            SheetName = "EmployeesWithoutHeader",
            Header = ExcelHeaderMode.Absent,
            Names = ["Name", "Department", "Age"]
        });

    Console.WriteLine("Excel employees without header using Names");
    Print(excelEmployeesWithoutHeader);
    Console.WriteLine();
}
finally
{
    try
    {
        if (Directory.Exists(excelDirectory))
        {
            Directory.Delete(excelDirectory, recursive: true);
        }
    }
    catch
    {
        // Sample cleanup is best-effort so the displayed Excel read behavior remains the primary result.
    }
}

var employees = DataFrame.Create(new
{
    Department = new[] { "Engineering", "Finance", "Engineering", "Finance", "Sales" },
    Employee = new[] { "Ali", "Ayse", "Mehmet", "Zeynep", "Can" },
    Salary = new[] { 120000m, 90000m, 150000m, 110000m, 80000m },
    Age = new[] { 32, 29, 41, 35, 27 }
});

var departmentSalaryTotals = employees
    .GroupBy("Department")
    .Sum("Salary");

Console.WriteLine("Department salary totals");
Print(departmentSalaryTotals);
Console.WriteLine();

var departmentSummary = employees
    .GroupBy("Department")
    .Aggregate(aggregation => aggregation
        .For("Salary").Sum().Average()
        .For("Age").Min().Max());

Console.WriteLine("Department multi-aggregation summary");
Print(departmentSummary);
Console.WriteLine();

var joinEmployees = DataFrame.Create(new
{
    EmployeeId = new[] { 1, 2, 3 },
    Name = new[] { "Ali", "Ayşe", "Mehmet" },
    DepartmentId = new[] { 10, 20, 30 }
});

var departments = DataFrame.Create(new
{
    Id = new[] { 10, 20, 40 },
    Department = new[] { "Engineering", "Finance", "Sales" }
});

var employeeDepartments = joinEmployees
    .LeftJoin(departments)
    .On("DepartmentId", "Id");

Console.WriteLine("Employee departments left join");
Print(employeeDepartments);
Console.WriteLine();

var orders = DataFrame.Create(new
{
    CompanyId = new[] { 1, 1 },
    OrderId = new[] { 100, 101 },
    Customer = new[] { "Ali", "Ayse" }
});

var orderLines = DataFrame.Create(new
{
    CompanyId = new[] { 1, 1 },
    OrderId = new[] { 100, 102 },
    Product = new[] { "Keyboard", "Mouse" }
});

var orderLineMatches = orders
    .InnerJoin(orderLines)
    .On(["CompanyId", "OrderId"]);

Console.WriteLine("Order lines composite key inner join");
Print(orderLineMatches);

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

static void CreateExcelSampleWorkbook(string path)
{
    using var workbook = new XLWorkbook();

    var employees = workbook.AddWorksheet("Employees");
    employees.Cell("A1").Value = "Name";
    employees.Cell("B1").Value = "Department";
    employees.Cell("C1").Value = "Age";
    employees.Cell("D1").Value = "Salary";
    employees.Cell("E1").Value = "Active";
    employees.Cell("F1").Value = "StartDate";

    employees.Cell("A2").Value = "Ali";
    employees.Cell("B2").Value = "Engineering";
    employees.Cell("C2").Value = 34;
    employees.Cell("D2").Value = 125000.50;
    employees.Cell("E2").Value = true;
    employees.Cell("F2").Value = new DateTime(2022, 3, 1);

    employees.Cell("A3").Value = "Ayşe";
    employees.Cell("B3").Value = "Finance";
    employees.Cell("C3").Value = 29;
    employees.Cell("D3").Value = 98000.00;
    employees.Cell("E3").Value = true;
    employees.Cell("F3").Value = new DateTime(2023, 6, 15);

    employees.Cell("A4").Value = "Mehmet";
    employees.Cell("B4").Value = "Sales";
    employees.Cell("D4").Value = 87500.75;
    employees.Cell("E4").Value = false;
    employees.Cell("F4").Value = new DateTime(2021, 11, 20);
    employees.Column("F").Style.DateFormat.Format = "yyyy-mm-dd";

    var departments = workbook.AddWorksheet("Departments");
    departments.Cell("A1").Value = "Department";
    departments.Cell("B1").Value = "HeadCount";
    departments.Cell("A2").Value = "Engineering";
    departments.Cell("B2").Value = 42;
    departments.Cell("A3").Value = "Finance";
    departments.Cell("B3").Value = 18;
    departments.Cell("A4").Value = "Sales";
    departments.Cell("B4").Value = 25;

    var withoutHeader = workbook.AddWorksheet("EmployeesWithoutHeader");
    withoutHeader.Cell("A1").Value = "Ali";
    withoutHeader.Cell("B1").Value = "Engineering";
    withoutHeader.Cell("C1").Value = 34;
    withoutHeader.Cell("A2").Value = "Ayşe";
    withoutHeader.Cell("B2").Value = "Finance";
    withoutHeader.Cell("C2").Value = 29;

    workbook.SaveAs(path);
}
