# Runiq.Data Grouping Sample

## Purpose

This sample demonstrates grouped aggregation with both single aggregation calls and the multi-aggregation builder.

## Scenario

The sample uses employees across several departments with salary and age values. One row contains null aggregation values so the source data and the filtered grouping input can be compared.

## Execution Flow

1. Creates a DataFrame with multiple departments, employee names, salaries, and ages.
2. Prints the source DataFrame, including the row with null salary and age values.
3. Filters rows to keep non-null `Salary` and `Age` values for grouped aggregation.
4. Prints the grouping input DataFrame.
5. Groups by `Department` with `GroupBy`.
6. Runs a single grouped salary aggregation with `Sum`.
7. Prints the department salary totals.
8. Uses `Aggregate` with the builder API.
9. Defines multiple aggregations for `Salary` with `Sum` and `Average`.
10. Defines aggregations for another column, `Age`, with `Min` and `Max`.
11. Prints the multi-aggregation result in declaration order.

## Expected Output

The console output shows department salary totals and a grouped summary with columns like:

```text
Department | Salary_Sum | Salary_Average | Age_Min | Age_Max
```

The multi-aggregation columns appear in the same order they are declared in the builder.

## Key APIs

- `DataFrame.Create`
- `DataFrame.Filter`
- `DataFrame.GroupBy`
- `GroupedDataFrame.Sum`
- `GroupedDataFrame.Aggregate`
- `GroupAggregationBuilder.For`

## Run

```powershell
dotnet run --project samples/Runiq.Data.Samples.Grouping/Runiq.Data.Samples.Grouping.csproj
```
