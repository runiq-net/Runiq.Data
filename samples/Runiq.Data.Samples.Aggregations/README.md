# Runiq.Data Aggregations Sample

## Purpose

This sample demonstrates global aggregation over numeric DataFrame columns.

## Scenario

The sample uses employee ages and salaries, including nullable cells, to show the source data and the non-null rows used for aggregation.

## Execution Flow

1. Creates a DataFrame with numeric and nullable `Age` and `Salary` columns.
2. Prints the source employees, including null values.
3. Filters rows to keep only records where both `Age` and `Salary` are non-null.
4. Prints the aggregation input DataFrame.
5. Prints row count with `Rows.Count`.
6. Calculates salary total with `Sum`.
7. Calculates salary average with `Average`.
8. Calculates minimum age with `Min`.
9. Calculates maximum age with `Max`.
10. Prints each result with a descriptive label.

## Expected Output

The console output shows the original data with null cells, the filtered non-null aggregation input, row count, salary sum and average, and age minimum and maximum. Null source rows are visible, but they are filtered out before aggregation because aggregation APIs require non-null input values.

## Key APIs

- `DataFrame.Create`
- `DataFrame.Filter`
- `DataFrame.Rows.Count`
- `DataFrame.Sum`
- `DataFrame.Average`
- `DataFrame.Min`
- `DataFrame.Max`

## Run

```powershell
dotnet run --project samples/Runiq.Data.Samples.Aggregations/Runiq.Data.Samples.Aggregations.csproj
```
