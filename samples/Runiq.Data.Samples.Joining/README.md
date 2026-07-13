# Runiq.Data Joining Sample

## Purpose

This sample demonstrates the main DataFrame join types and key matching modes.

## Scenario

The sample joins employee rows to department rows using different key names, then joins assignment rows to budget rows using a composite key.

## Execution Flow

1. Creates the left employee DataFrame and the right department DataFrame.
2. Prints both source DataFrames.
3. Runs an inner join with `InnerJoin(...).On("DepartmentId", "Id")`.
4. Runs a left join with `LeftJoin(...).On("DepartmentId", "Id")`.
5. Runs a right join with `RightJoin(...).On("DepartmentId", "Id")`.
6. Runs a full join with `FullJoin(...).On("DepartmentId", "Id")`.
7. Creates assignment and budget DataFrames with `DepartmentId` and `LocationId`.
8. Runs a composite key inner join with `On(["DepartmentId", "LocationId"])`.
9. Prints every join result under a separate console heading.

## Expected Output

The console output shows matching employee and department rows, Ayşe as a left-side unmatched row with null right-side columns, Support as a right-side unmatched row with null left-side columns, and assignment-budget matches for the composite key.

## Key APIs

- `DataFrame.Create`
- `DataFrame.InnerJoin`
- `DataFrame.LeftJoin`
- `DataFrame.RightJoin`
- `DataFrame.FullJoin`
- `DataFrameJoin.On`

## Run

```powershell
dotnet run --project samples/Runiq.Data.Samples.Joining/Runiq.Data.Samples.Joining.csproj
```
