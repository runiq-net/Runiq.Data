# Runiq.Data Pivoting Sample

## Purpose

This sample demonstrates `Pivot`, `PivotTable(...).Sum()`, and `Unpivot` together in one revenue reshaping flow.

## Scenario

The sample uses quarterly revenue for corporate departments. The unique revenue data shows a non-aggregating pivot where each `Department` and `Quarter` combination appears at most once. The transaction data includes duplicate `Sales` and `Q1` rows so `PivotTable(...).Sum()` can combine them. The pivoted wide result is then converted back to long form with `Unpivot`.

## Execution Flow

1. Creates long-format quarterly revenue where each `Department` and `Quarter` pair is unique.
2. Prints `Source Data — Unique Revenue`.
3. Calls `Pivot(index: "Department", columns: "Quarter", values: "Revenue")`.
4. Prints `Pivot Result`, including dynamic `Q1` and `Q2` columns and a `null` missing combination.
5. Creates long-format revenue transactions with duplicate `Sales` and `Q1` rows.
6. Prints `Source Data — Revenue Transactions`.
7. Calls `PivotTable(index: "Department", columns: "Quarter", values: "Revenue").Sum()`.
8. Prints `PivotTable Sum Result`, where duplicate transaction values are aggregated.
9. Calls `Unpivot` on the pivot result with `Department` as the id column and `Q1`, `Q2` as value columns.
10. Prints `Unpivot Result`, preserving value column order first and source row order second.

Long format
→ Pivot
→ Aggregated PivotTable
→ Unpivot back to long format

## Expected Output

```text
Source Data — Unique Revenue
Department | Quarter | Revenue
Sales | Q1 | 125000
Sales | Q2 | 138000
Engineering | Q1 | 210000
Engineering | Q2 | 198000
Support | Q1 | 82000

Pivot Result
Department | Q1 | Q2
Sales | 125000 | 138000
Engineering | 210000 | 198000
Support | 82000 | null

Source Data — Revenue Transactions
Department | Quarter | Revenue
Sales | Q1 | 75000
Sales | Q1 | 50000
Sales | Q2 | 138000
Engineering | Q1 | 210000
Engineering | Q2 | 198000
Support | Q1 | 82000

PivotTable Sum Result
Department | Q1 | Q2
Sales | 125000 | 138000
Engineering | 210000 | 198000
Support | 82000 | null

Unpivot Result
Department | Quarter | Revenue
Sales | Q1 | 125000
Engineering | Q1 | 210000
Support | Q1 | 82000
Sales | Q2 | 138000
Engineering | Q2 | 198000
Support | Q2 | null
```

## Key APIs

- `DataFrame.Create`
- `DataFrame.Pivot`
- `DataFrame.PivotTable`
- `PivotTableBuilder.Sum`
- `DataFrame.Unpivot`

`Pivot` reshapes unique index and column combinations without aggregation. `PivotTable(...).Sum()` allows duplicate combinations and aggregates their values. `Unpivot` converts selected wide value columns back into variable and value rows.

## Run

```powershell
dotnet run --project samples/Runiq.Data.Samples.Pivoting/Runiq.Data.Samples.Pivoting.csproj
```
