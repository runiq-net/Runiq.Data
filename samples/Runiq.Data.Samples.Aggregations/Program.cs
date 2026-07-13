using Runiq.Data;

var employees = DataFrame.Create(new
{
    Name = new[] { "Ali", "Ayse", "Mehmet", "Zeynep" },
    Age = new int?[] { 34, 29, null, 41 },
    Salary = new decimal?[] { 125000.50m, 98000.25m, null, 150000.00m }
});

Console.WriteLine("=== Employees ===");
Print(employees);

Console.WriteLine();
Console.WriteLine("=== Aggregation Input: Non-null Salary and Age ===");
var aggregationInput = employees.Filter(row => row["Salary"].Value is not null && row["Age"].Value is not null);
Print(aggregationInput);

Console.WriteLine();
Console.WriteLine("=== Aggregations ===");
Console.WriteLine($"Rows: {aggregationInput.Rows.Count()}");
Console.WriteLine($"Salary Sum: {aggregationInput.Sum("Salary")}");
Console.WriteLine($"Salary Average: {aggregationInput.Average("Salary")}");
Console.WriteLine($"Age Min: {aggregationInput.Min("Age")}");
Console.WriteLine($"Age Max: {aggregationInput.Max("Age")}");
Console.WriteLine("Rows with null Salary or Age are visible in the source data and filtered out before aggregation.");

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
