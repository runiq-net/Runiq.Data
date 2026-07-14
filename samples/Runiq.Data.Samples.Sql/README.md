# Runiq.Data SQL Sample

## Purpose

This sample demonstrates loading SQL query results into a DataFrame through ADO.NET. It uses SQLite only as an easily runnable provider, while the Runiq.Data production API depends on `DbConnection` and `DbCommand`, not on SQLite-specific types.

## Scenario

The sample creates an in-memory SQLite `Employees` table with `Id`, `Name`, `Department`, `Salary`, and `Active` columns.

It runs two queries:

1. Reads all employees with `DataFrame.ReadSql(DbConnection connection, string commandText)`.
2. Reads Engineering employees with `DataFrame.ReadSql(DbCommand command)` and a parameterized `@department` command parameter.

## Execution Flow

1. Creates a SQLite in-memory connection.
2. Keeps the connection open for the lifetime of the in-memory database.
3. Creates the `Employees` table.
4. Inserts sample employee rows.
5. Uses `DataFrame.ReadSql(connection, commandText)` to read all employees.
6. Prints the result to the console.
7. Creates a parameterized `DbCommand`.
8. Filters Engineering employees with the `Department` parameter.
9. Uses `DataFrame.ReadSql(command)`.
10. Prints the filtered result to the console.
11. Disposes the command and connection through `using` declarations.
12. Leaves no file or database artifact after the sample exits.

## Expected Output

```text
=== All Employees ===
Id | Name | Department | Salary | Active
1 | Ali | Engineering | 125000.5 | 1
2 | Ayşe | Finance | 110000 | 1
3 | Mehmet | Engineering | 98000.25 | 0
4 | Zeynep | Sales | null | 1

=== Engineering Employees ===
Id | Name | Salary
1 | Ali | 125000.5
3 | Mehmet | 98000.25
```

## Key APIs

- `DataFrame.ReadSql(DbConnection connection, string commandText)`
- `DataFrame.ReadSql(DbCommand command)`

Parameterized command usage:

```csharp
using var command = connection.CreateCommand();
command.CommandText = "SELECT Id, Name, Salary FROM Employees WHERE Department = @department ORDER BY Id";
command.Parameters.Add("@department", SqliteType.Text).Value = "Engineering";

var df = DataFrame.ReadSql(command);
```

## Run

```powershell
dotnet run --project samples/Runiq.Data.Samples.Sql/Runiq.Data.Samples.Sql.csproj
```
