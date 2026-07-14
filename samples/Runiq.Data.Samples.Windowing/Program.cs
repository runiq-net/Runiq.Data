using System.Globalization;
using Runiq.Data;

var source = DataFrame.Create(new
{
    Employee = new[] { "Ava", "Ben", "Cara", "Dev", "Eli", "Fay" },
    Department = new[] { "Sales", "Sales", "Sales", "Support", "Support", "Support" },
    Month = new[] { 1, 2, 3, 1, 2, 3 },
    Revenue = new[] { 100, 100, 80, 70, 90, 90 }
});

PrintSection(
    "Source Data",
    source,
    "Employee",
    "Department",
    "Month",
    "Revenue");

var ranking = source.Copy();
var rowNumber = ranking
    .Window()
    .PartitionBy("Department")
    .OrderByDescending("Revenue")
    .ThenBy("Employee")
    .RowNumber();
var rank = ranking
    .Window()
    .PartitionBy("Department")
    .OrderByDescending("Revenue")
    .Rank();
var denseRank = ranking
    .Window()
    .PartitionBy("Department")
    .OrderByDescending("Revenue")
    .DenseRank();

ranking.Columns.Add("RowNumber", rowNumber);
ranking.Columns.Add("Rank", rank);
ranking.Columns.Add("DenseRank", denseRank);

PrintSection(
    "Ranking Functions",
    ranking,
    "Employee",
    "Department",
    "Revenue",
    "RowNumber",
    "Rank",
    "DenseRank");

var navigation = source.Copy();
var previousRevenue = navigation
    .Window()
    .PartitionBy("Department")
    .OrderBy("Month")
    .Lag("Revenue");
var nextRevenue = navigation
    .Window()
    .PartitionBy("Department")
    .OrderBy("Month")
    .Lead("Revenue");
var firstRevenue = navigation
    .Window()
    .PartitionBy("Department")
    .OrderBy("Month")
    .FirstValue("Revenue");
var lastRevenue = navigation
    .Window()
    .PartitionBy("Department")
    .OrderBy("Month")
    .LastValue("Revenue");

navigation.Columns.Add("PreviousRevenue", previousRevenue);
navigation.Columns.Add("NextRevenue", nextRevenue);
navigation.Columns.Add("FirstRevenue", firstRevenue);
navigation.Columns.Add("LastRevenue", lastRevenue);

PrintSection(
    "Value Navigation",
    navigation,
    "Employee",
    "Department",
    "Month",
    "Revenue",
    "PreviousRevenue",
    "NextRevenue",
    "FirstRevenue",
    "LastRevenue");

var partitionAggregations = source.Copy();
var departmentTotal = partitionAggregations
    .Window()
    .PartitionBy("Department")
    .Sum("Revenue");
var departmentAverage = partitionAggregations
    .Window()
    .PartitionBy("Department")
    .Average("Revenue");

partitionAggregations.Columns.Add("DepartmentTotal", departmentTotal);
partitionAggregations.Columns.Add("DepartmentAverage", departmentAverage);

PrintSection(
    "Partition Aggregations",
    partitionAggregations,
    "Employee",
    "Department",
    "Revenue",
    "DepartmentTotal",
    "DepartmentAverage");

var running = source.Copy();
var runningTotal = running
    .Window()
    .PartitionBy("Department")
    .OrderBy("Month")
    .RunningSum("Revenue");
var runningAverage = running
    .Window()
    .PartitionBy("Department")
    .OrderBy("Month")
    .RunningAverage("Revenue");

running.Columns.Add("RunningTotal", runningTotal);
running.Columns.Add("RunningAverage", runningAverage);

PrintSection(
    "Running Aggregations",
    running,
    "Employee",
    "Department",
    "Month",
    "Revenue",
    "RunningTotal",
    "RunningAverage");

var moving = source.Copy();
var movingTotal = moving
    .Window()
    .PartitionBy("Department")
    .OrderBy("Month")
    .MovingSum("Revenue", windowSize: 2);
var movingAverage = moving
    .Window()
    .PartitionBy("Department")
    .OrderBy("Month")
    .MovingAverage("Revenue", windowSize: 2);

moving.Columns.Add("MovingTotal", movingTotal);
moving.Columns.Add("MovingAverage", movingAverage);

PrintSection(
    "Moving Aggregations",
    moving,
    "Employee",
    "Department",
    "Month",
    "Revenue",
    "MovingTotal",
    "MovingAverage");

// Prints focused column slices so each window function group stays readable in the console.
static void PrintSection(string title, DataFrame dataFrame, params string[] columnNames)
{
    Console.WriteLine($"=== {title} ===");
    Console.WriteLine(string.Join(" | ", columnNames));

    for (var rowIndex = 0; rowIndex < dataFrame.Rows.Count(); rowIndex++)
    {
        var values = columnNames
            .Select(columnName => FormatValue(dataFrame[columnName].GetValue(rowIndex)));
        Console.WriteLine(string.Join(" | ", values));
    }

    Console.WriteLine();
}

// Uses invariant formatting so README output remains stable across developer machines.
static string FormatValue(object? value)
{
    return value switch
    {
        null => "null",
        double number => number.ToString("0.##", CultureInfo.InvariantCulture),
        float number => number.ToString("0.##", CultureInfo.InvariantCulture),
        decimal number => number.ToString("0.##", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };
}
