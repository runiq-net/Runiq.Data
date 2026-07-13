using Runiq.Data;

var dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
var csvPath = Path.Combine(dataDirectory, "employees.csv");
var headerlessPath = Path.Combine(dataDirectory, "employees-without-header.csv");

Console.WriteLine("=== ReadCsv with Header and Type Inference ===");
var employees = DataFrame.ReadCsv(csvPath);
Print(employees);
PrintSchema(employees);

Console.WriteLine();
Console.WriteLine("=== Headerless Read with Names ===");
var employeesWithoutHeader = DataFrame.ReadCsv(
    headerlessPath,
    new CsvReadOptions
    {
        Header = CsvHeaderMode.Absent,
        Names = ["Name", "Department", "Age"]
    });
Print(employeesWithoutHeader);

var tempDirectory = Path.Combine(Path.GetTempPath(), "Runiq.Data.Samples.Csv", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDirectory);

try
{
    var roundTripPath = Path.Combine(tempDirectory, "employees-round-trip.csv");
    var headerlessOutputPath = Path.Combine(tempDirectory, "employees-semicolon-without-header.csv");
    var nullAndEmptyPath = Path.Combine(tempDirectory, "null-and-empty.csv");

    Console.WriteLine();
    Console.WriteLine("=== WriteCsv and Round Trip ===");
    employees.WriteCsv(roundTripPath);
    var reloaded = DataFrame.ReadCsv(roundTripPath);
    Print(reloaded);

    Console.WriteLine();
    Console.WriteLine("=== Headerless Write with Custom Delimiter ===");
    employees.WriteCsv(
        headerlessOutputPath,
        new CsvWriteOptions
        {
            IncludeHeader = false,
            Delimiter = ';'
        });

    var reloadedHeaderless = DataFrame.ReadCsv(
        headerlessOutputPath,
        new CsvReadOptions
        {
            Header = CsvHeaderMode.Absent,
            Names = ["Name", "Department", "Age", "Salary", "Active"],
            Delimiter = ';'
        });
    Print(reloadedHeaderless);

    Console.WriteLine();
    Console.WriteLine("=== Turkish Text, Null, and Empty String ===");
    var notes = DataFrame.Create(new
    {
        Name = new[] { "Ayse", "Cagri", "Ipek" },
        NativeText = new[] { "Ayşe", "Çağrı", "İpek" },
        Note = new string?[] { null, string.Empty, "Merhaba" }
    });
    notes.WriteCsv(nullAndEmptyPath);
    Print(DataFrame.ReadCsv(nullAndEmptyPath));
    Console.WriteLine("The null note writes as an unquoted empty field; the empty string writes as a quoted empty field.");
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

static void PrintSchema(DataFrame dataFrame)
{
    Console.WriteLine("Schema");
    foreach (var column in dataFrame.Schema.Columns)
    {
        Console.WriteLine($"{column.Name}: {column.DataType.Name}, Nullable: {column.IsNullable}");
    }
}
