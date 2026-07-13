using Runiq.Data;

var employees = DataFrame.Create(new
{
    Name = new[] { "Ali", "Ayse", "Mehmet", "Zeynep", "Ali" },
    Age = new[] { 34, 29, 41, 31, 34 },
    Department = new[] { "Engineering", "Finance", "Engineering", "Support", "Engineering" },
    Salary = new[] { 125000m, 98000m, 150000m, 90000m, 125000m }
});

Console.WriteLine("=== Original DataFrame ===");
Print(employees);

Console.WriteLine();
Console.WriteLine("=== Filter: Salary >= 100000 ===");
var highEarners = employees.Filter(row => row["Salary"] >= 100000m);
Print(highEarners);

Console.WriteLine();
Console.WriteLine("=== SortBy Name ===");
var sortedByName = employees.Copy();
sortedByName.Rows.SortBy("Name");
Print(sortedByName);

Console.WriteLine();
Console.WriteLine("=== SortByDescending Salary ===");
var sortedBySalary = highEarners.Copy();
sortedBySalary.Rows.SortByDescending("Salary");
Print(sortedBySalary);

Console.WriteLine();
Console.WriteLine("=== Distinct Rows ===");
Print(employees.Distinct());

Console.WriteLine();
Console.WriteLine("=== Distinct Departments ===");
Print(employees.Distinct("Department"));

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
