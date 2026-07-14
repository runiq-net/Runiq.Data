# Runiq.Data

[![Tests](https://github.com/runiq-net/Runiq.Data/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/runiq-net/Runiq.Data/actions/workflows/ci.yml)
[![Version](https://img.shields.io/badge/dynamic/xml?url=https%3A%2F%2Fraw.githubusercontent.com%2Fruniq-net%2FRuniq.Data%2Fmain%2Fsrc%2FRuniq.Data%2FRuniq.Data.csproj&query=%2FProject%2FPropertyGroup%2FVersion&label=version&color=blue)](src/Runiq.Data/Runiq.Data.csproj)
[![License](https://img.shields.io/github/license/runiq-net/Runiq.Data.svg)](LICENSE)
[![Target Framework](https://img.shields.io/badge/dynamic/xml?url=https%3A%2F%2Fraw.githubusercontent.com%2Fruniq-net%2FRuniq.Data%2Fmain%2Fsrc%2FRuniq.Data%2FRuniq.Data.csproj&query=%2FProject%2FPropertyGroup%2FTargetFramework&label=.NET&color=512BD4)](https://dotnet.microsoft.com/)

Runiq.Data is a DataFrame and data processing library built for .NET developers.

It lets you create, transform, analyze, reshape, import, and export structured data using C# and the .NET ecosystem.

## Why Runiq.Data?

- Process structured data without leaving C# and .NET
- Create, filter, sort, group, aggregate, join, pivot, and reshape DataFrames
- Use ranking, navigation, partition, running, and moving window calculations
- Read and write CSV, Excel, and JSON files
- Read SQL query results and append DataFrame rows to existing SQL tables

## Installation

NuGet CLI:

```powershell
dotnet add package Runiq.Data
```

Package Manager Console:

```powershell
Install-Package Runiq.Data
```

## Quick Start

```csharp
using Runiq.Data;

var data = DataFrame.Create(new
{
    Department = new[] { "Engineering", "Sales", "Engineering" },
    Revenue = new[] { 120000, 80000, 150000 }
});

var result = data
    .GroupBy("Department")
    .Aggregate(aggregation => aggregation
        .For("Revenue")
        .Sum()
        .Average());

Console.WriteLine(result);
```

## What you can build

### Shape and transform data

Create DataFrames, add or remove rows and columns, filter, sort, join, copy, pivot, and unpivot structured data.

### Group and analyze

Use Sum, Average, Min, Max, GroupBy, grouped aggregations, PivotTable, and distinct operations.

### Calculate across rows

Use RowNumber, Rank, DenseRank, Lag, Lead, FirstValue, LastValue, partition aggregations, running calculations, and moving calculations.

### Connect your data

Read and write CSV, Excel, and JSON files. Read SQL query results and append DataFrame rows to existing SQL tables.

## Short examples

### Pivot

```csharp
var result = data.Pivot(
    index: "Department",
    columns: "Quarter",
    values: "Revenue");
```

### Window function

```csharp
var rank = data
    .Window()
    .PartitionBy("Department")
    .OrderByDescending("Revenue")
    .Rank();

data.Columns.Add("Rank", rank);
```

### SQL

```csharp
var data = DataFrame.ReadSql(connection, query);

data.WriteSql(
    connection,
    "Reporting.MonthlyRevenue");
```

## Documentation

Read the full documentation at [runiq.net/docs/data](https://runiq.net/docs/data).

## Samples

- [CSV](samples/Runiq.Data.Samples.Csv) - Read, write, and round-trip CSV files
- [Excel](samples/Runiq.Data.Samples.Excel) - Read and write `.xlsx` workbooks
- [JSON](samples/Runiq.Data.Samples.Json) - Write, read, and format JSON data
- [SQL](samples/Runiq.Data.Samples.Sql) - Read SQL query results and append rows with SQLite
- [Pivoting](samples/Runiq.Data.Samples.Pivoting) - Pivot, PivotTable, and Unpivot
- [Windowing](samples/Runiq.Data.Samples.Windowing) - Ranking, navigation, partition, running, and moving calculations

## SQL Support

SQL support uses provider-independent ADO.NET APIs. You can read query results from a `DbConnection` and command text, read from a `DbCommand`, and append DataFrame rows to an existing table through a `DbConnection`.

SQL behavior is validated with SQLite, SQL Server, and PostgreSQL. SQL writing appends to existing tables; it does not create, replace, upsert, or bulk insert tables.

## Validation

- CI runs through the repository GitHub Actions workflow
- Full solution test suite passes
- Zero compiler and analyzer warnings
- SQL behavior validated with SQLite, SQL Server, and PostgreSQL

## Development

```powershell
dotnet build Runiq.Data.sln
dotnet test Runiq.Data.sln
```

## License

Runiq.Data is licensed under the [MIT License](LICENSE).
