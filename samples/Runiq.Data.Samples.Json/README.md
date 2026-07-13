# Runiq.Data JSON Sample

## Purpose

This sample demonstrates writing a DataFrame to JSON, reading the same file back, and producing compact JSON when indentation is not needed.

## Scenario

The sample creates an employee DataFrame with string, nullable integer, Boolean, and decimal columns.

## Execution Flow

1. Creates a temporary output directory.
2. Creates an employee DataFrame with `DataFrame.Create`.
3. Writes the DataFrame to `employees.json` with `WriteJson`.
4. Reads `employees.json` back with `DataFrame.ReadJson`.
5. Prints the reloaded DataFrame to the console.
6. Writes `employees-compact.json` with `JsonWriteOptions.WriteIndented = false`.
7. Prints the compact JSON text to the console.
8. Deletes the temporary output directory in best-effort cleanup.

## Expected Output

The console output shows the round-tripped employee rows and then a compact JSON array of objects:

```text
=== WriteJson and ReadJson Round Trip ===
Name | Age | Active | Salary
Ali | 34 | True | 125000.50
Ayse | null | False | 98000.25

=== Compact JSON Output ===
[{"Name":"Ali","Age":34,"Active":true,"Salary":125000.50},{"Name":"Ayse","Age":null,"Active":false,"Salary":98000.25}]
```

## Key APIs

- `DataFrame.Create`
- `DataFrame.WriteJson`
- `DataFrame.ReadJson`
- `JsonWriteOptions`

## Run

```powershell
dotnet run --project samples/Runiq.Data.Samples.Json/Runiq.Data.Samples.Json.csproj
```
