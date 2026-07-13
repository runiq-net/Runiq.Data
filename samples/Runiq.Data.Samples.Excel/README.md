# Runiq.Data Excel Sample

## Demonstrates

- Reading the first worksheet
- Selecting worksheets by name and index
- Headerless worksheets with explicit names
- Native text, Boolean, numeric, nullable, and DateTime values
- Turkish text
- Multiple worksheets

Hidden worksheets can be read with `SheetName` when the workbook includes one.

## Run

```powershell
dotnet run --project samples/Runiq.Data.Samples.Excel
```

## Key APIs

- `DataFrame.ReadExcel`
- `ExcelReadOptions`
- `ExcelHeaderMode`
