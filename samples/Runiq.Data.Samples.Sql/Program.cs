using System.Globalization;
using Microsoft.Data.Sqlite;
using Runiq.Data;
using Runiq.Data.IO;

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

// SQLite in-memory databases exist only while the owning connection stays open.
CreateEmployeesTable(connection);
InsertInitialEmployees(connection);

var initialEmployees = ReadAllEmployees(connection);
Print("Initial Employees", initialEmployees);

var newEmployees = DataFrame.Create(new
{
    Id = new[] { 3, 4 },
    Name = new[] { "Mehmet", "Zeynep" },
    Department = new[] { "Engineering", "Sales" },
    Salary = new double?[] { 98000.25, null },
    Active = new[] { false, true }
});

newEmployees.WriteSql(connection, "Employees");

var afterAppend = ReadAllEmployees(connection);
Print("After WriteSql Append", afterAppend);

using var engineeringCommand = connection.CreateCommand();
engineeringCommand.CommandText =
    """
    SELECT Id, Name, Department, Salary, Active, Source
    FROM Employees
    WHERE Department = @department
    ORDER BY Id
    """;
engineeringCommand.Parameters.Add("@department", SqliteType.Text).Value = "Engineering";

var engineeringEmployees = DataFrame.ReadSql(engineeringCommand);
Print("Engineering Employees", engineeringEmployees);

using (var transaction = connection.BeginTransaction())
{
    var temporaryEmployee = DataFrame.Create(new
    {
        Id = new[] { 5 },
        Name = new[] { "Temporary Employee" },
        Department = new[] { "Operations" },
        Salary = new[] { 75000.00 },
        Active = new[] { true }
    });

    // The caller owns the external transaction; Runiq.Data writes inside it
    // without committing or rolling it back.
    temporaryEmployee.WriteSql(
        connection,
        "Employees",
        new SqlWriteOptions
        {
            Transaction = transaction
        });

    using var temporaryCommand = connection.CreateCommand();
    temporaryCommand.Transaction = transaction;
    temporaryCommand.CommandText =
        """
        SELECT Id, Name, Department, Salary, Active, Source
        FROM Employees
        WHERE Id = 5
        ORDER BY Id
        """;

    var insideTransaction = DataFrame.ReadSql(temporaryCommand);
    Print("Inside External Transaction", insideTransaction);

    transaction.Rollback();
}

var afterRollback = ReadAllEmployees(connection);
Print("After External Transaction Rollback", afterRollback);

/// <summary>
/// Creates the sample Employees table in the caller-owned open SQLite connection.
/// </summary>
static void CreateEmployeesTable(SqliteConnection connection)
{
    using var command = connection.CreateCommand();
    command.CommandText =
        """
        CREATE TABLE Employees (
            Id INTEGER NOT NULL PRIMARY KEY,
            Name TEXT NOT NULL,
            Department TEXT NOT NULL,
            Salary REAL NULL,
            Active INTEGER NOT NULL,
            Source TEXT NOT NULL DEFAULT 'database'
        );
        """;
    command.ExecuteNonQuery();
}

/// <summary>
/// Inserts the initial database-owned rows that are preserved by the append sample.
/// </summary>
static void InsertInitialEmployees(SqliteConnection connection)
{
    using var command = connection.CreateCommand();
    command.CommandText =
        """
        INSERT INTO Employees (Id, Name, Department, Salary, Active, Source) VALUES
            (1, 'Ali', 'Engineering', 125000.50, 1, 'database'),
            (2, 'Ayşe', 'Finance', 110000.00, 1, 'database');
        """;
    command.ExecuteNonQuery();
}

/// <summary>
/// Reads the complete Employees table in a deterministic order for repeatable output.
/// </summary>
static DataFrame ReadAllEmployees(SqliteConnection connection)
{
    return DataFrame.ReadSql(
        connection,
        """
        SELECT Id, Name, Department, Salary, Active, Source
        FROM Employees
        ORDER BY Id
        """);
}

/// <summary>
/// Prints a compact DataFrame table so the sample stays focused on SQL usage.
/// </summary>
static void Print(string title, DataFrame dataFrame)
{
    Console.WriteLine($"=== {title} ===");

    var columnNames = dataFrame.Columns.Select(static column => column.Name).ToArray();
    Console.WriteLine(string.Join(" | ", columnNames));

    for (var rowIndex = 0; rowIndex < dataFrame.Rows.Count(); rowIndex++)
    {
        var values = columnNames.Select(columnName => FormatValue(dataFrame[columnName].GetValue(rowIndex)));
        Console.WriteLine(string.Join(" | ", values));
    }

    Console.WriteLine();
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
