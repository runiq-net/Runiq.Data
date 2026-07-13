# Runiq.Data Rows and Columns Sample

## Purpose

This sample demonstrates mutable row and column operations through the DataFrame facade APIs.

## Scenario

The sample starts with an employee DataFrame containing names, ages, and departments, then mutates rows and columns step by step.

## Execution Flow

1. Creates the starting DataFrame with `DataFrame.Create`.
2. Prints the initial DataFrame and its row and column counts.
3. Adds a new row with `Rows.Add` and prints the updated DataFrame.
4. Updates an existing row with `Rows.Update` and prints the updated DataFrame.
5. Reads the first row with `GetRow` and prints selected values.
6. Removes a row with `Rows.Remove` and prints the updated DataFrame and row count.
7. Adds an `Active` column with `Columns.Add` and prints the updated DataFrame and column count.
8. Renames `Department` to `Team` with `Columns.Rename` and prints the updated DataFrame.
9. Removes the `Active` column with `Columns.Remove` and prints the final DataFrame and column count.

## Expected Output

The console output shows each mutation under a separate heading. Row additions, updates, removals, column additions, renames, and removals are visible immediately after each operation.

## Key APIs

- `DataFrame.Create`
- `DataFrame.GetRow`
- `DataFrame.Rows.Add`
- `DataFrame.Rows.Update`
- `DataFrame.Rows.Remove`
- `DataFrame.Rows.Count`
- `DataFrame.Columns.Add`
- `DataFrame.Columns.Rename`
- `DataFrame.Columns.Remove`
- `DataFrame.Columns.Count`

## Run

```powershell
dotnet run --project samples/Runiq.Data.Samples.RowsAndColumns/Runiq.Data.Samples.RowsAndColumns.csproj
```
