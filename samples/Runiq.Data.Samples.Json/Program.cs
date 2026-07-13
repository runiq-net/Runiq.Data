using Runiq.Data;

var tempDirectory = Path.Combine(Path.GetTempPath(), "Runiq.Data.Samples.Json", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDirectory);

try
{
    var jsonPath = Path.Combine(tempDirectory, "employees.json");
    var compactJsonPath = Path.Combine(tempDirectory, "employees-compact.json");

    Console.WriteLine("=== WriteJson and ReadJson Round Trip ===");
    var employees = DataFrame.Create(new
    {
        Name = new[] { "Ali", "Ayse" },
        Age = new int?[] { 34, null },
        Active = new[] { true, false },
        Salary = new[] { 125000.50m, 98000.25m }
    });

    employees.WriteJson(jsonPath);
    var reloaded = DataFrame.ReadJson(jsonPath);
    Print(reloaded);

    Console.WriteLine();
    Console.WriteLine("=== Compact JSON Output ===");
    employees.WriteJson(
        compactJsonPath,
        new JsonWriteOptions
        {
            WriteIndented = false
        });
    Console.WriteLine(File.ReadAllText(compactJsonPath));
}
finally
{
    try
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
    catch (IOException)
    {
        Console.WriteLine($"Temporary cleanup skipped: {tempDirectory}");
    }
    catch (UnauthorizedAccessException)
    {
        Console.WriteLine($"Temporary cleanup skipped: {tempDirectory}");
    }
}

// Prints a compact table so the sample focuses on the JSON write/read flow instead of formatting.
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
