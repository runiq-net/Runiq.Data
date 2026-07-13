# Runiq.Data Core Sample

## Purpose

This sample demonstrates the core DataFrame workflow: creating a DataFrame, inspecting its structure, reading individual cells, taking leading rows, and creating an independent copy.

## Scenario

The sample uses a small employee dataset with names, nullable ages, and departments.

## Execution Flow

1. Creates a DataFrame from an anonymous object with `DataFrame.Create`.
2. Prints the original DataFrame values.
3. Prints schema columns, CLR data types, ordinals, and nullability.
4. Prints row and column counts with `Rows.Count` and `Columns.Count`.
5. Reads the first employee name through cell access.
6. Prints the first two records with `Head(2)`.
7. Prints the first two records with `Take(2)`.
8. Creates an independent copy with `Copy`.
9. Updates the copy and prints both the original and copied DataFrames to show that the original is unchanged.

## Expected Output

The console output shows the original employee rows, schema details, row and column counts, the first cell value, leading rows from `Head` and `Take`, and a copied DataFrame that can be changed independently.

## Key APIs

- `DataFrame.Create`
- `DataFrame.Schema`
- `DataFrame.Rows.Count`
- `DataFrame.Columns.Count`
- `DataFrame.Head`
- `DataFrame.Take`
- `DataFrame.Copy`

## Run

```powershell
dotnet run --project samples/Runiq.Data.Samples.Core/Runiq.Data.Samples.Core.csproj
```
