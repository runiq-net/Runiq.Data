# Runiq.Data Excel Sample

## Purpose

This sample demonstrates reading Excel worksheets into DataFrames and writing DataFrames back to `.xlsx` workbooks. It covers default worksheet reading, worksheet selection, header modes, native Excel cell types, headerless export, and Excel round-trip behavior.

## Scenario

The sample uses an Excel workbook with `Employees`, `Departments`, and `EmployeesWithoutHeader` worksheets. The data includes text, Turkish characters, nullable cells, numeric values, Boolean values, and DateTime values.

## Execution Flow

1. Reads the default worksheet from the source workbook.
2. Prints employee rows and the inferred schema.
3. Reads the `Departments` worksheet by name.
4. Prints the department rows.
5. Reads worksheet index `1`.
6. Prints the same department worksheet selected by index.
7. Reads a headerless worksheet using explicit column names.
8. Prints the headerless employee rows.
9. Writes the employee DataFrame to a temporary `.xlsx` workbook.
10. Uses a custom worksheet name, `EmployeesExport`.
11. Reads the generated workbook back.
12. Writes a second workbook without headers.
13. Reads the headerless workbook using `ExcelHeaderMode.Absent` and `Names`.
14. Prints the round-trip results.
15. Deletes temporary workbook files.

## Expected Output

The console output shows native string, numeric, Boolean and DateTime values, nullable cells, Turkish characters, custom worksheet selection, headerless reload, and the same values after Excel round-trip.

## Key APIs

- `DataFrame.ReadExcel`
- `DataFrame.WriteExcel`
- `ExcelReadOptions`
- `ExcelWriteOptions`
- `ExcelHeaderMode`

## Run

```powershell
dotnet run --project samples/Runiq.Data.Samples.Excel/Runiq.Data.Samples.Excel.csproj
```
