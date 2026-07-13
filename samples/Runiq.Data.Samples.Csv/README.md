# Runiq.Data CSV Sample

## Demonstrates

- Reading CSV files
- Header modes and explicit column names
- Type inference and nullable columns
- Writing CSV files
- Headerless output and custom delimiters
- CSV round-trip
- Turkish text
- Null and empty string output

## Run

```powershell
dotnet run --project samples/Runiq.Data.Samples.Csv
```

## Key APIs

- `DataFrame.ReadCsv`
- `DataFrame.WriteCsv`
- `CsvReadOptions`
- `CsvWriteOptions`
- `CsvHeaderMode`
