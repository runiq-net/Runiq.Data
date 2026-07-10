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

static void Print(DataFrame dataFrame)
{
    var columnNames = dataFrame.Columns.Select(static column => column.Name).ToArray();
    Console.WriteLine(string.Join(" | ", columnNames));

    for (var rowIndex = 0; rowIndex < dataFrame.Rows.Count(); rowIndex++)
    {
        var values = columnNames.Select(columnName => dataFrame[columnName].GetValue(rowIndex));
        Console.WriteLine(string.Join(" | ", values));
    }
}
