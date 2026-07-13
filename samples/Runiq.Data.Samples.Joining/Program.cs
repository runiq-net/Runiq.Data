using Runiq.Data;

var employees = DataFrame.Create(new
{
    EmployeeId = new[] { 1, 2, 3 },
    Name = new[] { "Ali", "Ayşe", "Mehmet" },
    DepartmentId = new[] { 10, 20, 30 }
});

var departments = DataFrame.Create(new
{
    Id = new[] { 10, 30, 40 },
    Department = new[] { "Engineering", "Sales", "Support" }
});

Console.WriteLine("=== Employees ===");
Print(employees);
Console.WriteLine();
Console.WriteLine("=== Departments ===");
Print(departments);

Console.WriteLine();
Console.WriteLine("=== Inner Join ===");
Print(employees.InnerJoin(departments).On("DepartmentId", "Id"));

Console.WriteLine();
Console.WriteLine("=== Left Join ===");
Print(employees.LeftJoin(departments).On("DepartmentId", "Id"));

Console.WriteLine();
Console.WriteLine("=== Right Join ===");
Print(employees.RightJoin(departments).On("DepartmentId", "Id"));

Console.WriteLine();
Console.WriteLine("=== Full Join ===");
Print(employees.FullJoin(departments).On("DepartmentId", "Id"));

var assignments = DataFrame.Create(new
{
    DepartmentId = new[] { 10, 10, 20 },
    LocationId = new[] { 1, 2, 1 },
    Assignment = new[] { "Platform", "Data", "Reporting" }
});

var budgets = DataFrame.Create(new
{
    DepartmentId = new[] { 10, 20, 30 },
    LocationId = new[] { 1, 1, 1 },
    Budget = new[] { 500000m, 200000m, 150000m }
});

Console.WriteLine();
Console.WriteLine("=== Composite Key Inner Join: DepartmentId + LocationId ===");
Print(assignments.InnerJoin(budgets).On(["DepartmentId", "LocationId"]));

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
