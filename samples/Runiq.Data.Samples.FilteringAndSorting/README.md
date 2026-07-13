# Runiq.Data Filtering and Sorting Sample

## Purpose

This sample demonstrates how to select rows, sort rows, and remove duplicates from a DataFrame.

## Scenario

The sample uses an employee dataset with repeated rows and different salary values so filtering, sorting, and distinct results are easy to compare.

## Execution Flow

1. Creates a DataFrame with repeated employee data and numeric salary values.
2. Prints the original DataFrame.
3. Filters rows with `Filter` where salary is at least `100000`.
4. Copies the original DataFrame, sorts it by `Name` with `Rows.SortBy`, and prints the result.
5. Copies the filtered DataFrame, sorts it by `Salary` with `Rows.SortByDescending`, and prints the result.
6. Removes duplicate full rows with `Distinct` and prints the result.
7. Removes duplicate department keys with `Distinct("Department")` and prints the result.

## Expected Output

The console output shows filtered high-salary records, rows sorted ascending by name, filtered rows sorted descending by salary, duplicate full rows removed, and one representative row per department.

## Key APIs

- `DataFrame.Create`
- `DataFrame.Filter`
- `DataFrame.Copy`
- `DataFrame.Rows.SortBy`
- `DataFrame.Rows.SortByDescending`
- `DataFrame.Distinct`

## Run

```powershell
dotnet run --project samples/Runiq.Data.Samples.FilteringAndSorting/Runiq.Data.Samples.FilteringAndSorting.csproj
```
