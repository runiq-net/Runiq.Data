using Runiq.Data.Series;

namespace Runiq.Data.DataFrameTests.Windowing;

/// <summary>
/// Verifies ordered window RunningSum and RunningAverage partitioning, ordering, alignment, type, validation, and mutation contracts.
/// </summary>
public sealed class WindowRunningAggregationTests
{
    // Verifies that global RunningSum and RunningAverage use all prior ordered rows in the DataFrame.
    [Fact]
    public void RunningAggregations_WithGlobalOrdering_ReturnRunningValues()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = new[] { 3, 1, 2 },
            Amount = new[] { 30, 10, 20 }
        });

        var runningSum = df.Window().OrderBy("CreatedAt").RunningSum("Amount");
        var runningAverage = df.Window().OrderBy("CreatedAt").RunningAverage("Amount");

        Assert.Equal(new[] { 60, 10, 30 }, IntValues(runningSum));
        Assert.Equal(new[] { 20d, 10d, 15d }, DoubleValues(runningAverage));
    }

    // Verifies that partitioned RunningSum and RunningAverage restart for each partition.
    [Fact]
    public void RunningAggregations_WithPartition_RestartForEachPartition()
    {
        var df = CreateRevenueDataFrame();

        var runningSum = df.Window()
            .PartitionBy("Department")
            .OrderBy("Month")
            .RunningSum("Revenue");
        var runningAverage = df.Window()
            .PartitionBy("Department")
            .OrderBy("Month")
            .RunningAverage("Revenue");

        Assert.Equal(new[] { 100, 300, 400, 50, 125 }, IntValues(runningSum));
        Assert.Equal(new[] { 100d, 150d, 400d / 3d, 50d, 62.5d }, DoubleValues(runningAverage));
    }

    // Verifies that descending ordering changes the running sequence inside each partition.
    [Fact]
    public void RunningAggregations_WithDescendingOrdering_UseDescendingSequence()
    {
        var df = CreateRevenueDataFrame();

        var runningSum = df.Window()
            .PartitionBy("Department")
            .OrderByDescending("Month")
            .RunningSum("Revenue");

        Assert.Equal(new[] { 400, 300, 100, 125, 75 }, IntValues(runningSum));
    }

    // Verifies that composite ordering uses all configured ordering columns for running windows.
    [Fact]
    public void RunningAggregations_WithCompositeOrdering_UseAllOrderingColumns()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales", "Sales", "Sales" },
            Year = new[] { 2024, 2024, 2023, 2024 },
            Month = new[] { 2, 1, 12, 3 },
            Revenue = new[] { 20, 10, 5, 30 }
        });

        var runningSum = df.Window()
            .PartitionBy("Department")
            .OrderBy("Year")
            .ThenBy("Month")
            .RunningSum("Revenue");

        Assert.Equal(new[] { 35, 15, 5, 65 }, IntValues(runningSum));
    }

    // Verifies that fully equal ordering keys use source-row order as the running tie-breaker.
    [Fact]
    public void RunningAggregations_WithEqualOrderingKeys_PreserveSourceRowStability()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            SortKey = new[] { 1, 1, 1 },
            Amount = new[] { 10, 20, 30 }
        });

        var runningSum = df.Window().OrderBy("SortKey").RunningSum("Amount");

        Assert.Equal(new[] { 10, 30, 60 }, IntValues(runningSum));
    }

    // Verifies that running results remain aligned to the original source row positions.
    [Fact]
    public void RunningAggregations_ReturnValuesAlignedToSourceRows()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Third", "First", "Second" },
            CreatedAt = new[] { 3, 1, 2 },
            Amount = new[] { 30, 10, 20 }
        });

        var runningSum = df.Window().OrderBy("CreatedAt").RunningSum("Amount");

        Assert.Equal(new[] { 60, 10, 30 }, IntValues(runningSum));
    }

    // Verifies that null target values use the existing fail-fast aggregation behavior.
    [Fact]
    public void RunningAggregations_WithNullTargetValue_ThrowArgumentException()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = new[] { 1, 2 },
            Amount = new int?[] { 10, null }
        });

        Assert.Throws<ArgumentException>(() => df.Window().OrderBy("CreatedAt").RunningSum("Amount"));
        Assert.Throws<ArgumentException>(() => df.Window().OrderBy("CreatedAt").RunningAverage("Amount"));
    }

    // Verifies that RunningSum follows the existing numeric promotion contract.
    [Fact]
    public void RunningSum_WithSmallIntegerColumn_ReturnsPromotedIntSeries()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = new[] { 1, 2 },
            Amount = new byte[] { 10, 20 }
        });

        var runningSum = df.Window().OrderBy("CreatedAt").RunningSum("Amount");

        Assert.Equal(typeof(int), runningSum.DataType);
        Assert.Equal(new[] { 10, 30 }, IntValues(runningSum));
    }

    // Verifies that RunningAverage returns double results for numeric columns.
    [Fact]
    public void RunningAverage_WithNumericColumn_ReturnsDoubleSeries()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = new[] { 1, 2 },
            Amount = new[] { 10m, 20m }
        });

        var runningAverage = df.Window().OrderBy("CreatedAt").RunningAverage("Amount");

        Assert.Equal(typeof(double), runningAverage.DataType);
        Assert.Equal(new[] { 10d, 15d }, DoubleValues(runningAverage));
    }

    // Verifies that empty DataFrames produce empty running aggregate series with operation result types.
    [Fact]
    public void RunningAggregations_WithEmptyDataFrame_ReturnEmptyTypedSeries()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = Array.Empty<int>(),
            Amount = Array.Empty<int>()
        });

        var runningSum = df.Window().OrderBy("CreatedAt").RunningSum("Amount");
        var runningAverage = df.Window().OrderBy("CreatedAt").RunningAverage("Amount");

        Assert.Equal(typeof(int), runningSum.DataType);
        Assert.Equal(typeof(double), runningAverage.DataType);
        Assert.Equal(0, runningSum.Count);
        Assert.Equal(0, runningAverage.Count);
    }

    // Verifies that single-row partitions return that row's value as the running result.
    [Fact]
    public void RunningAggregations_WithSingleRowPartitions_ReturnOwnValues()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Ops" },
            Month = new[] { 1, 1 },
            Revenue = new[] { 100, 90 }
        });

        var runningSum = df.Window().PartitionBy("Department").OrderBy("Month").RunningSum("Revenue");
        var runningAverage = df.Window().PartitionBy("Department").OrderBy("Month").RunningAverage("Revenue");

        Assert.Equal(new[] { 100, 90 }, IntValues(runningSum));
        Assert.Equal(new[] { 100d, 90d }, DoubleValues(runningAverage));
    }

    // Verifies that unsupported target values fail instead of using string conversion or fallback logic.
    [Fact]
    public void RunningAggregations_WithUnsupportedValues_ThrowArgumentException()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = new[] { 1, 2 },
            Payload = new[] { new Payload(1), new Payload(2) }
        });

        Assert.Throws<ArgumentException>(() => df.Window().OrderBy("CreatedAt").RunningSum("Payload"));
        Assert.Throws<ArgumentException>(() => df.Window().OrderBy("CreatedAt").RunningAverage("Payload"));
    }

    // Verifies that invalid target column names fail before running aggregation calculation.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RunningAggregations_WithInvalidTargetColumnName_Throw(string? columnName)
    {
        var df = CreateRevenueDataFrame();

        Assert.ThrowsAny<ArgumentException>(() => df.Window().OrderBy("Month").RunningSum(columnName!));
        Assert.ThrowsAny<ArgumentException>(() => df.Window().OrderBy("Month").RunningAverage(columnName!));
    }

    // Verifies that missing target columns use the existing column lookup failure.
    [Fact]
    public void RunningAggregations_WithMissingTargetColumn_ThrowKeyNotFoundException()
    {
        var df = CreateRevenueDataFrame();

        Assert.Throws<KeyNotFoundException>(() => df.Window().OrderBy("Month").RunningSum("Missing"));
        Assert.Throws<KeyNotFoundException>(() => df.Window().OrderBy("Month").RunningAverage("Missing"));
    }

    // Verifies that RunningSum and RunningAverage do not mutate source rows, source columns, or schema.
    [Fact]
    public void RunningAggregations_DoNotMutateSourceDataFrame()
    {
        var df = CreateRevenueDataFrame();
        var schema = df.Schema;
        var columnNames = ColumnNames(df);
        var revenue = IntColumn(df, "Revenue");

        _ = df.Window().PartitionBy("Department").OrderBy("Month").RunningSum("Revenue");
        _ = df.Window().PartitionBy("Department").OrderBy("Month").RunningAverage("Revenue");

        Assert.Same(schema, df.Schema);
        Assert.Equal(columnNames, ColumnNames(df));
        Assert.Equal(revenue, IntColumn(df, "Revenue"));
    }

    // Verifies that running aggregate outputs can be appended through the existing Columns.Add API.
    [Fact]
    public void RunningAggregationResults_CanBeAddedAsColumns()
    {
        var df = CreateRevenueDataFrame();
        var runningSum = df.Window().PartitionBy("Department").OrderBy("Month").RunningSum("Revenue");

        df.Columns.Add("RunningTotal", runningSum);

        Assert.Equal(new[] { 100, 300, 400, 50, 125 }, IntValues(df["RunningTotal"]));
    }

    /// <summary>
    /// Creates a deterministic revenue table used to verify ordered running aggregation behavior.
    /// </summary>
    private static global::Runiq.Data.DataFrame CreateRevenueDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales", "Sales", "Ops", "Ops" },
            Month = new[] { 1, 2, 3, 1, 2 },
            Revenue = new[] { 100, 200, 100, 50, 75 }
        });
    }

    /// <summary>
    /// Reads integer values from an untyped result series for running sum assertions.
    /// </summary>
    private static int[] IntValues(ISeries series)
    {
        return Enumerable.Range(0, series.Count)
            .Select(index => (int)series.GetValue(index)!)
            .ToArray();
    }

    /// <summary>
    /// Reads double values from an untyped result series for running average assertions.
    /// </summary>
    private static double[] DoubleValues(ISeries series)
    {
        return Enumerable.Range(0, series.Count)
            .Select(index => (double)series.GetValue(index)!)
            .ToArray();
    }

    /// <summary>
    /// Captures DataFrame column names before window execution to verify non-mutating behavior.
    /// </summary>
    private static string[] ColumnNames(global::Runiq.Data.DataFrame df)
    {
        return df.Columns.Select(static column => column.Name).ToArray();
    }

    /// <summary>
    /// Reads integer column values before and after window execution to verify source value stability.
    /// </summary>
    private static int[] IntColumn(global::Runiq.Data.DataFrame df, string columnName)
    {
        return Enumerable.Range(0, df.Rows.Count())
            .Select(index => (int)df[columnName].GetValue(index)!)
            .ToArray();
    }

    /// <summary>
    /// Represents an unsupported aggregate value used to verify fail-fast type validation.
    /// </summary>
    private sealed record Payload(int Value);
}
