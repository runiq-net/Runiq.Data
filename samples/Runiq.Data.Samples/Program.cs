using Runiq.Data;

Console.WriteLine("Runiq.Data samples");
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
