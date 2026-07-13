using Runiq.Data;

var employees = DataFrame.Create(new
{
    Department = new[] { "Engineering", "Finance", "Engineering", "Finance", "Sales" },
    Employee = new[] { "Ali", "Ayse", "Mehmet", "Zeynep", "Cagri" },
    Salary = new decimal?[] { 120000m, 90000m, 150000m, null, 80000m },
    Age = new int?[] { 32, 29, 41, null, 27 }
});

Console.WriteLine("=== Employees ===");
Print(employees);

Console.WriteLine();
Console.WriteLine("=== Grouping Input: Non-null Salary and Age ===");
var groupingInput = employees.Filter(row => row["Salary"].Value is not null && row["Age"].Value is not null);
Print(groupingInput);

Console.WriteLine();
Console.WriteLine("=== GroupBy Department, Sum Salary ===");
var salaryTotals = groupingInput
    .GroupBy("Department")
    .Sum("Salary");
Print(salaryTotals);

Console.WriteLine();
Console.WriteLine("=== Grouped Multi Aggregation ===");
var summary = groupingInput
    .GroupBy("Department")
    .Aggregate(aggregation => aggregation
        .For("Salary").Sum().Average()
        .For("Age").Min().Max());
Print(summary);

Console.WriteLine();
Console.WriteLine("Column order follows aggregation declaration order: Salary_Sum, Salary_Average, Age_Min, Age_Max.");

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
