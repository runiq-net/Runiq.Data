# Runiq.Data SQL Sample

## Purpose

This sample demonstrates that SQL query results can be loaded into a DataFrame through ADO.NET and that DataFrame rows can be appended to an existing SQL table through parameterized commands.

SQLite is used only as an easily runnable sample provider. The Runiq.Data production API depends on `DbConnection`, `DbCommand`, and `DbTransaction` abstractions rather than SQLite-specific types.

## Scenario

The sample creates an in-memory SQLite `Employees` table with `Id`, `Name`, `Department`, `Salary`, `Active`, and `Source` columns. The initial database rows are read with the connection overload, then a new DataFrame without the `Source` column is appended to the existing table.

The database fills the omitted `Source` column with its default `database` value. The sample then runs a parameterized Engineering query with the command overload and demonstrates a caller-owned external transaction that is rolled back by the caller after a temporary employee is observed inside the transaction.

## Execution Flow

1. Creates a SQLite in-memory connection.
2. Opens the connection.
3. Creates the `Employees` table.
4. Inserts the initial database rows.
5. Uses `DataFrame.ReadSql(connection, commandText)` to read existing employees.
6. Prints the result with the `Initial Employees` heading.
7. Creates a DataFrame representing new employees.
8. Omits the `Source` column from the new DataFrame so the database default is used.
9. Appends new rows with `DataFrame.WriteSql(connection, "Employees")`.
10. Uses `DataFrame.ReadSql` to read all employees again.
11. Prints the result with the `After WriteSql Append` heading.
12. Uses a parameterized `DbCommand` to read Engineering employees.
13. Prints the result with the `Engineering Employees` heading.
14. Starts an external transaction.
15. Appends a temporary employee with `DataFrame.WriteSql(connection, "Employees", options)` inside the external transaction.
16. Queries the temporary employee inside the same transaction to show that it is visible.
17. Rolls back the transaction as the caller.
18. Verifies with `ReadSql` that the temporary employee did not remain in the table after rollback.
19. Prints the final result with the `After External Transaction Rollback` heading.
20. Disposes caller-owned resources through `using` declarations.
21. Leaves no file or database artifact after the sample exits.

## Expected Output

```text
=== Initial Employees ===
Id | Name | Department | Salary | Active | Source
1 | Ali | Engineering | 125000.5 | 1 | database
2 | Ayşe | Finance | 110000 | 1 | database

=== After WriteSql Append ===
Id | Name | Department | Salary | Active | Source
1 | Ali | Engineering | 125000.5 | 1 | database
2 | Ayşe | Finance | 110000 | 1 | database
3 | Mehmet | Engineering | 98000.25 | 0 | database
4 | Zeynep | Sales | null | 1 | database

=== Engineering Employees ===
Id | Name | Department | Salary | Active | Source
1 | Ali | Engineering | 125000.5 | 1 | database
3 | Mehmet | Engineering | 98000.25 | 0 | database

=== Inside External Transaction ===
Id | Name | Department | Salary | Active | Source
5 | Temporary Employee | Operations | 75000 | 1 | database

=== After External Transaction Rollback ===
Id | Name | Department | Salary | Active | Source
1 | Ali | Engineering | 125000.5 | 1 | database
2 | Ayşe | Finance | 110000 | 1 | database
3 | Mehmet | Engineering | 98000.25 | 0 | database
4 | Zeynep | Sales | null | 1 | database
```

## Key APIs

- `DataFrame.ReadSql(DbConnection connection, string commandText)`
- `DataFrame.ReadSql(DbCommand command)`
- `DataFrame.WriteSql(DbConnection connection, string tableName)`
- `DataFrame.WriteSql(DbConnection connection, string tableName, SqlWriteOptions options)`

External transaction usage:

```csharp
using var transaction = connection.BeginTransaction();

df.WriteSql(
    connection,
    "Employees",
    new SqlWriteOptions
    {
        Transaction = transaction
    });

transaction.Rollback();
```

`WriteSql` only appends rows to an existing table. Table creation, replace, upsert, and bulk insert are not part of this API. Table and column names must be simple SQL identifiers.

By default, `WriteSql` uses an internal transaction. When an external transaction is provided, transaction ownership remains with the caller; Runiq.Data does not commit, roll back, or dispose it.

## Run

```powershell
dotnet run --project samples/Runiq.Data.Samples.Sql/Runiq.Data.Samples.Sql.csproj
```
