using Runiq.Data;

var employees = DataFrame.Create(new
{
    Name = new[] { "Ali", "Ayse", "Mehmet" },
    Age = new[] { 34, 29, 41 },
    Department = new[] { "Engineering", "Finance", "Sales" }
});

Console.WriteLine("=== Original DataFrame ===");
Print(employees);
Console.WriteLine($"Rows: {employees.Rows.Count()}, Columns: {employees.Columns.Count()}");

Console.WriteLine();
Console.WriteLine("=== Row Add ===");
employees.Rows.Add(new { Name = "Zeynep", Age = 31, Department = "Support" });
Print(employees);
Console.WriteLine($"Rows: {employees.Rows.Count()}");

Console.WriteLine();
Console.WriteLine("=== Row Update ===");
employees.Rows.Update(1, new { Name = "Ayse", Age = 30, Department = "Operations" });
Print(employees);

Console.WriteLine();
Console.WriteLine("=== Row Access ===");
var firstRow = employees.GetRow(0);
Console.WriteLine($"{firstRow["Name"]} works in {firstRow["Department"]}");

Console.WriteLine();
Console.WriteLine("=== Row Remove ===");
employees.Rows.Remove(2);
Print(employees);
Console.WriteLine($"Rows: {employees.Rows.Count()}");

Console.WriteLine();
Console.WriteLine("=== Column Add ===");
employees.Columns.Add("Active", new[] { true, true, false });
Print(employees);
Console.WriteLine($"Columns: {employees.Columns.Count()}");

Console.WriteLine();
Console.WriteLine("=== Column Rename ===");
employees.Columns.Rename("Department", "Team");
Print(employees);

Console.WriteLine();
Console.WriteLine("=== Column Remove ===");
employees.Columns.Remove("Active");
Print(employees);
Console.WriteLine($"Columns: {employees.Columns.Count()}");

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
