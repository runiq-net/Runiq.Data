# Runiq.Data Windowing Sample

## Purpose

This sample demonstrates Runiq.Data Window Functions in one focused console program. Window Functions return one result value for each source row; they do not reduce row count like grouped aggregation.

## Scenario

The sample uses monthly employee revenue by department. `Department` defines independent partitions, and `Month` or `Revenue` defines ordering depending on the window function being demonstrated.

The data intentionally includes repeated revenue values so ranking ties are visible. Each department has multiple months so value navigation, running aggregation, and moving aggregation can show partition boundaries.

## Execution Flow

1. Creates a source DataFrame with `Employee`, `Department`, `Month`, and `Revenue`.
2. Prints the source data.
3. Uses `RowNumber`, `Rank`, and `DenseRank` over department partitions ordered by revenue.
4. Uses `Lag`, `Lead`, `FirstValue`, and `LastValue` over department partitions ordered by month.
5. Uses partition `Sum` and `Average` without reducing row count.
6. Uses `RunningSum` and `RunningAverage` from each partition start through the current ordered row.
7. Uses `MovingSum` and `MovingAverage` with `windowSize: 2`.
8. Prints each function group as a focused table to keep the output readable.

## Expected Output

```text
=== Source Data ===
Employee | Department | Month | Revenue
Ava | Sales | 1 | 100
Ben | Sales | 2 | 100
Cara | Sales | 3 | 80
Dev | Support | 1 | 70
Eli | Support | 2 | 90
Fay | Support | 3 | 90

=== Ranking Functions ===
Employee | Department | Revenue | RowNumber | Rank | DenseRank
Ava | Sales | 100 | 1 | 1 | 1
Ben | Sales | 100 | 2 | 1 | 1
Cara | Sales | 80 | 3 | 3 | 2
Dev | Support | 70 | 3 | 3 | 2
Eli | Support | 90 | 1 | 1 | 1
Fay | Support | 90 | 2 | 1 | 1

=== Value Navigation ===
Employee | Department | Month | Revenue | PreviousRevenue | NextRevenue | FirstRevenue | LastRevenue
Ava | Sales | 1 | 100 | null | 100 | 100 | 80
Ben | Sales | 2 | 100 | 100 | 80 | 100 | 80
Cara | Sales | 3 | 80 | 100 | null | 100 | 80
Dev | Support | 1 | 70 | null | 90 | 70 | 90
Eli | Support | 2 | 90 | 70 | 90 | 70 | 90
Fay | Support | 3 | 90 | 90 | null | 70 | 90

=== Partition Aggregations ===
Employee | Department | Revenue | DepartmentTotal | DepartmentAverage
Ava | Sales | 100 | 280 | 93.33
Ben | Sales | 100 | 280 | 93.33
Cara | Sales | 80 | 280 | 93.33
Dev | Support | 70 | 250 | 83.33
Eli | Support | 90 | 250 | 83.33
Fay | Support | 90 | 250 | 83.33

=== Running Aggregations ===
Employee | Department | Month | Revenue | RunningTotal | RunningAverage
Ava | Sales | 1 | 100 | 100 | 100
Ben | Sales | 2 | 100 | 200 | 100
Cara | Sales | 3 | 80 | 280 | 93.33
Dev | Support | 1 | 70 | 70 | 70
Eli | Support | 2 | 90 | 160 | 80
Fay | Support | 3 | 90 | 250 | 83.33

=== Moving Aggregations ===
Employee | Department | Month | Revenue | MovingTotal | MovingAverage
Ava | Sales | 1 | 100 | 100 | 100
Ben | Sales | 2 | 100 | 200 | 100
Cara | Sales | 3 | 80 | 180 | 90
Dev | Support | 1 | 70 | 70 | 70
Eli | Support | 2 | 90 | 160 | 80
Fay | Support | 3 | 90 | 180 | 90
```

## Key APIs

- `DataFrame.Create`
- `DataFrame.Copy`
- `DataFrame.Window`
- `WindowBuilder.PartitionBy`
- `WindowBuilder.OrderBy`
- `WindowBuilder.OrderByDescending`
- `OrderedWindowBuilder.ThenBy`
- `OrderedWindowBuilder.RowNumber`
- `OrderedWindowBuilder.Rank`
- `OrderedWindowBuilder.DenseRank`
- `OrderedWindowBuilder.Lag`
- `OrderedWindowBuilder.Lead`
- `OrderedWindowBuilder.FirstValue`
- `OrderedWindowBuilder.LastValue`
- `WindowBuilder.Sum`
- `WindowBuilder.Average`
- `WindowBuilder.Min`
- `WindowBuilder.Max`
- `OrderedWindowBuilder.RunningSum`
- `OrderedWindowBuilder.RunningAverage`
- `OrderedWindowBuilder.MovingSum`
- `OrderedWindowBuilder.MovingAverage`
- `ColumnOperations.Add`

## Run

```powershell
dotnet run --project samples/Runiq.Data.Samples.Windowing/Runiq.Data.Samples.Windowing.csproj
```
