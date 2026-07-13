# Runiq.Data Excel Sample

## Purpose

This sample demonstrates reading Excel worksheets into DataFrames with default worksheet selection, explicit sheet selection, and headerless worksheet options.

## Scenario

The sample uses an Excel workbook with `Employees`, `Departments`, and `EmployeesWithoutHeader` worksheets. The data includes text, Turkish characters, nullable cells, numeric values, Boolean values, and DateTime values.

## Execution Flow

1. Reads the default first worksheet with `DataFrame.ReadExcel`.
2. Prints employee rows and the inferred schema.
3. Reads the `Departments` worksheet by name with `ExcelReadOptions.SheetName`.
4. Prints the department rows.
5. Reads worksheet index `1` with `ExcelReadOptions.SheetIndex`.
6. Prints the same department worksheet selected by index.
7. Reads the `EmployeesWithoutHeader` worksheet with `ExcelHeaderMode.Absent` and explicit `Names`.
8. Prints the headerless employee rows.

## Expected Output

The console output shows first worksheet employee data, selected department worksheet data, headerless data with explicit column names, DateTime values, nullable age cells, numeric and Boolean values, and Turkish characters.

## Key APIs

- `DataFrame.ReadExcel`
- `ExcelReadOptions`
- `ExcelHeaderMode`

## Run

```powershell
dotnet run --project samples/Runiq.Data.Samples.Excel/Runiq.Data.Samples.Excel.csproj
```
