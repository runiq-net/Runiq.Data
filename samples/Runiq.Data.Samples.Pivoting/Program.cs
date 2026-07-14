using Runiq.Data;

var revenueByQuarter = DataFrame.Create(new
{
    Department = new[] { "Sales", "Sales", "Engineering", "Engineering", "Support" },
    Quarter = new[] { "Q1", "Q2", "Q1", "Q2", "Q1" },
    Revenue = new decimal?[] { 125000m, 138000m, 210000m, 198000m, 82000m }
});

Console.WriteLine("Source Data — Unique Revenue");
Print(revenueByQuarter);

Console.WriteLine();
Console.WriteLine("Pivot Result");
var pivot = revenueByQuarter.Pivot(
    index: "Department",
    columns: "Quarter",
    values: "Revenue");
Print(pivot);

var revenueTransactions = DataFrame.Create(new
{
    Department = new[] { "Sales", "Sales", "Sales", "Engineering", "Engineering", "Support" },
    Quarter = new[] { "Q1", "Q1", "Q2", "Q1", "Q2", "Q1" },
    Revenue = new decimal?[] { 75000m, 50000m, 138000m, 210000m, 198000m, 82000m }
});

Console.WriteLine();
Console.WriteLine("Source Data — Revenue Transactions");
Print(revenueTransactions);

Console.WriteLine();
Console.WriteLine("PivotTable Sum Result");
var pivotTable = revenueTransactions
    .PivotTable(
        index: "Department",
        columns: "Quarter",
        values: "Revenue")
    .Sum();
Print(pivotTable);

Console.WriteLine();
Console.WriteLine("Unpivot Result");
var unpivoted = pivot.Unpivot(
    idColumns: ["Department"],
    valueColumns: ["Q1", "Q2"],
    variableColumnName: "Quarter",
    valueColumnName: "Revenue");
Print(unpivoted);

// The sample prints null explicitly so missing pivot combinations remain visible
// in the README output copied from the actual run.
static void Print(DataFrame dataFrame)
{
    var columnNames = dataFrame.Columns.Select(static column => column.Name).ToArray();
    Console.WriteLine(string.Join(" | ", columnNames));

    for (var rowIndex = 0; rowIndex < dataFrame.Rows.Count(); rowIndex++)
    {
        var values = columnNames.Select(columnName => dataFrame[columnName].GetValue(rowIndex));
        Console.WriteLine(string.Join(" | ", values.Select(static value => value?.ToString() ?? "null")));
    }
}
