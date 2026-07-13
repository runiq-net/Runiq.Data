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
