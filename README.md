# Runiq.Data

[![Tests](https://github.com/runiq-net/Runiq.Data/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/runiq-net/Runiq.Data/actions/workflows/ci.yml)
[![Version](https://img.shields.io/badge/dynamic/xml?url=https%3A%2F%2Fraw.githubusercontent.com%2Fruniq-net%2FRuniq.Data%2Fmain%2Fsrc%2FRuniq.Data%2FRuniq.Data.csproj&query=%2FProject%2FPropertyGroup%2FVersion&label=version&color=blue)](src/Runiq.Data/Runiq.Data.csproj)
[![License](https://img.shields.io/github/license/runiq-net/Runiq.Data.svg)](LICENSE)
[![Target Framework](https://img.shields.io/badge/dynamic/xml?url=https%3A%2F%2Fraw.githubusercontent.com%2Fruniq-net%2FRuniq.Data%2Fmain%2Fsrc%2FRuniq.Data%2FRuniq.Data.csproj&query=%2FProject%2FPropertyGroup%2FTargetFramework&label=.NET&color=512BD4)](https://dotnet.microsoft.com/)

Runiq.Data is an enterprise-focused DataFrame and data processing library for .NET.

It is not just a Pandas clone. It aims to provide DataFrame, Series, schema management, validation, profiling, ETL and AI-ready data processing capabilities for C# applications.

## Installation

Install the stable package from NuGet.org:

```powershell
dotnet add package Runiq.Data
```

Or install `Runiq.Data` from the Visual Studio NuGet Package Manager.

Current IO capabilities include:

- CSV Read
- CSV Write
- Excel Read
- Excel Write
- JSON Read
- JSON Write
- SQL Read
- SQL Write - Existing-table append

SQL Read and existing-table append through ADO.NET DbConnection, DbCommand and DbTransaction. Provider-independent ADO.NET core with contract coverage for SQLite, SQL Server and PostgreSQL.

Selected DataFrame capabilities include:

- Row and column mutation facades
- Filtering and sorting
- Distinct row projection
- Grouped aggregation
- Joins
- Window functions with partition and ordering definitions: RowNumber, Rank, DenseRank, Lag, Lead, FirstValue, LastValue, partition Sum, partition Average, partition Min, partition Max, RunningSum, RunningAverage
- Unpivot for reshaping selected wide columns into variable/value rows
- Non-aggregating pivot with first-seen row and column ordering
- Pivot table aggregations for Sum, Average, Min, and Max

Sample projects:

- Runiq.Data.Samples.Sql - SQL Read and existing-table append with SQLite
- Runiq.Data.Samples.Pivoting - Pivot, PivotTable Sum, and Unpivot reshaping

Pivot table example:

```csharp
var result = df
    .PivotTable(index: "Department", columns: "Quarter", values: "Revenue")
    .Sum();
```

Unpivot example:

```csharp
var result = df.Unpivot(
    idColumns: ["Department"],
    valueColumns: ["Q1", "Q2"],
    variableColumnName: "Quarter",
    valueColumnName: "Revenue");
```
