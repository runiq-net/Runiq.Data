using Runiq.Data;

var employees = DataFrame.Create(new
{
    Name = new[] { "Ali", "Ayse", "Mehmet" },
    Age = new int?[] { 34, 29, null },
    Department = new[] { "Engineering", "Finance", "Sales" }
});

Console.WriteLine("=== Original DataFrame ===");
Print(employees);

Console.WriteLine();
Console.WriteLine("=== Schema ===");
foreach (var column in employees.Schema.Columns)
{
    Console.WriteLine($"{column.Ordinal}: {column.Name} ({column.DataType.Name}, Nullable: {column.IsNullable})");
}

Console.WriteLine();
Console.WriteLine("=== Counts and Cell Access ===");
Console.WriteLine($"Rows: {employees.Rows.Count()}");
Console.WriteLine($"Columns: {employees.Columns.Count()}");
Console.WriteLine($"First employee: {employees["Name"].GetValue(0)}");

Console.WriteLine();
Console.WriteLine("=== Head(2) ===");
Print(employees.Head(2));

Console.WriteLine();
Console.WriteLine("=== Take(2) ===");
Print(employees.Take(2));

Console.WriteLine();
Console.WriteLine("=== Copy ===");
var copy = employees.Copy();
copy.Rows.Update(0, new { Name = "Zeynep", Age = (int?)31, Department = "Support" });
Console.WriteLine("Original after mutating the copy");
Print(employees);
Console.WriteLine("Mutated copy");
Print(copy);

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
