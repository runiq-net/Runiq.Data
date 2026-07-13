# Runiq.Data CSV Sample

## Purpose

This sample demonstrates reading CSV files into DataFrames, customizing header handling, writing CSV files, and verifying output through read/write round-trips.

## Scenario

The sample uses employee CSV files containing names, departments, ages, salaries, active states, nullable values, and Turkish characters.

## Execution Flow

1. Reads `employees.csv` with `DataFrame.ReadCsv` using default header and delimiter settings.
2. Prints the inferred DataFrame values and schema.
3. Reads `employees-without-header.csv` with `CsvReadOptions`, `CsvHeaderMode.Absent`, and explicit `Names`.
4. Creates a temporary output directory.
5. Writes the employee DataFrame to a temporary CSV file with `WriteCsv`.
6. Reads the generated CSV file back with `ReadCsv` and prints the round-trip result.
7. Writes a headerless semicolon-delimited CSV file with `CsvWriteOptions`.
8. Reads the headerless semicolon file back with explicit names and a custom delimiter.
9. Creates a small DataFrame with Turkish text, a null string, and an empty string.
10. Writes and reads that DataFrame to show the null and empty string distinction.
11. Deletes the temporary output directory in best-effort cleanup.

## Expected Output

The console output shows header-based CSV loading, explicit names applied to headerless data, inferred numeric and Boolean values, nullable age values, Turkish characters, values loaded again after CSV round-trips, and the visible difference between a null cell and an empty string cell.

## Key APIs

- `DataFrame.ReadCsv`
- `DataFrame.WriteCsv`
- `DataFrame.Create`
- `CsvReadOptions`
- `CsvWriteOptions`
- `CsvHeaderMode`

## Run

```powershell
dotnet run --project samples/Runiq.Data.Samples.Csv/Runiq.Data.Samples.Csv.csproj
```
