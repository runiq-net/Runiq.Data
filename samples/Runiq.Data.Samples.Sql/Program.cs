using System.Globalization;
using Microsoft.Data.Sqlite;
using Runiq.Data;

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

// SQLite in-memory databases exist only while the owning connection stays open.
CreateEmployeesTable(connection);

Console.WriteLine("=== All Employees ===");
var allEmployees = DataFrame.ReadSql(
    connection,
    """
    SELECT Id, Name, Department, Salary, Active
    FROM Employees
    ORDER BY Id
    """);
Print(allEmployees);

Console.WriteLine();
Console.WriteLine("=== Engineering Employees ===");
using var engineeringCommand = connection.CreateCommand();
engineeringCommand.CommandText =
    """
    SELECT Id, Name, Salary
    FROM Employees
    WHERE Department = @department
    ORDER BY Id
    """;
engineeringCommand.Parameters.Add("@department", SqliteType.Text).Value = "Engineering";

var engineeringEmployees = DataFrame.ReadSql(engineeringCommand);
Print(engineeringEmployees);

/// <summary>
/// Creates and populates the sample Employees table in the caller-owned open SQLite connection.
/// </summary>
static void CreateEmployeesTable(SqliteConnection connection)
{
    using var command = connection.CreateCommand();
    command.CommandText =
        """
        CREATE TABLE Employees (
            Id INTEGER NOT NULL,
            Name TEXT NOT NULL,
            Department TEXT NOT NULL,
            Salary REAL NULL,
            Active INTEGER NOT NULL
        );

        INSERT INTO Employees (Id, Name, Department, Salary, Active) VALUES
            (1, 'Ali', 'Engineering', 125000.50, 1),
            (2, 'Ayşe', 'Finance', 110000.00, 1),
            (3, 'Mehmet', 'Engineering', 98000.25, 0),
            (4, 'Zeynep', 'Sales', NULL, 1);
        """;
    command.ExecuteNonQuery();
}

/// <summary>
/// Prints a compact DataFrame table so the sample stays focused on SQL Read usage.
/// </summary>
static void Print(DataFrame dataFrame)
{
    var columnNames = dataFrame.Columns.Select(static column => column.Name).ToArray();
    Console.WriteLine(string.Join(" | ", columnNames));

    for (var rowIndex = 0; rowIndex < dataFrame.Rows.Count(); rowIndex++)
    {
        var values = columnNames.Select(columnName => FormatValue(dataFrame[columnName].GetValue(rowIndex)));
        Console.WriteLine(string.Join(" | ", values));
    }
}

/// <summary>
/// Formats provider-returned CLR values with invariant output for repeatable sample documentation.
/// </summary>
static string FormatValue(object? value)
{
    return value switch
    {
        null => "null",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };
}
