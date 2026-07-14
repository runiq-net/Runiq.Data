using Runiq.Data.Series;

namespace Runiq.Data.DataFrameTests.Windowing;

/// <summary>
/// Verifies ordered window MovingSum and MovingAverage partitioning, ordering, alignment, type, validation, and mutation contracts.
/// </summary>
public sealed class WindowMovingAggregationTests
{
    // Verifies that global MovingSum and MovingAverage use the configured trailing ordered window.
    [Fact]
    public void MovingAggregations_WithGlobalOrdering_ReturnMovingValues()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = new[] { 4, 1, 3, 2 },
            Amount = new[] { 40, 10, 30, 20 }
        });

        var movingSum = df.Window().OrderBy("CreatedAt").MovingSum("Amount", windowSize: 3);
        var movingAverage = df.Window().OrderBy("CreatedAt").MovingAverage("Amount", windowSize: 3);

        Assert.Equal(new[] { 90, 10, 60, 30 }, IntValues(movingSum));
        Assert.Equal(new[] { 30d, 10d, 20d, 15d }, DoubleValues(movingAverage));
    }

    // Verifies that partitioned MovingSum and MovingAverage restart their windows for each partition.
    [Fact]
    public void MovingAggregations_WithPartition_RestartForEachPartition()
    {
        var df = CreateRevenueDataFrame();

        var movingSum = df.Window()
            .PartitionBy("Department")
            .OrderBy("Month")
            .MovingSum("Revenue", windowSize: 2);
        var movingAverage = df.Window()
            .PartitionBy("Department")
            .OrderBy("Month")
            .MovingAverage("Revenue", windowSize: 2);

        Assert.Equal(new[] { 100, 300, 300, 50, 125 }, IntValues(movingSum));
        Assert.Equal(new[] { 100d, 150d, 150d, 50d, 62.5d }, DoubleValues(movingAverage));
    }

    // Verifies that window size one aggregates only the current ordered row.
    [Fact]
    public void MovingAggregations_WithWindowSizeOne_ReturnCurrentRowValues()
    {
        var df = CreateRevenueDataFrame();

        var movingSum = df.Window().PartitionBy("Department").OrderBy("Month").MovingSum("Revenue", windowSize: 1);
        var movingAverage = df.Window().PartitionBy("Department").OrderBy("Month").MovingAverage("Revenue", windowSize: 1);

        Assert.Equal(new[] { 100, 200, 100, 50, 75 }, IntValues(movingSum));
        Assert.Equal(new[] { 100d, 200d, 100d, 50d, 75d }, DoubleValues(movingAverage));
    }

    // Verifies two-row and three-row moving windows produce different trailing aggregates.
    [Fact]
    public void MovingAggregations_WithTwoAndThreeRowWindows_ReturnExpectedValues()
    {
        var df = CreateRevenueDataFrame();

        var twoRows = df.Window().PartitionBy("Department").OrderBy("Month").MovingSum("Revenue", windowSize: 2);
        var threeRows = df.Window().PartitionBy("Department").OrderBy("Month").MovingSum("Revenue", windowSize: 3);

        Assert.Equal(new[] { 100, 300, 300, 50, 125 }, IntValues(twoRows));
        Assert.Equal(new[] { 100, 300, 400, 50, 125 }, IntValues(threeRows));
    }

    // Verifies that a window larger than the partition uses all available partition history.
    [Fact]
    public void MovingAggregations_WithWindowLargerThanPartition_UseAvailableHistory()
    {
        var df = CreateRevenueDataFrame();

        var movingSum = df.Window().PartitionBy("Department").OrderBy("Month").MovingSum("Revenue", windowSize: 10);

        Assert.Equal(new[] { 100, 300, 400, 50, 125 }, IntValues(movingSum));
    }

    // Verifies that early partition rows use partial windows instead of returning null.
    [Fact]
    public void MovingAggregations_ForEarlyRows_UsePartialWindow()
    {
        var df = CreateRevenueDataFrame();

        var movingAverage = df.Window().PartitionBy("Department").OrderBy("Month").MovingAverage("Revenue", windowSize: 3);

        Assert.Equal(new[] { 100d, 150d, 400d / 3d, 50d, 62.5d }, DoubleValues(movingAverage));
    }

    // Verifies that descending ordering changes the moving sequence inside each partition.
    [Fact]
    public void MovingAggregations_WithDescendingOrdering_UseDescendingSequence()
    {
        var df = CreateRevenueDataFrame();

        var movingSum = df.Window()
            .PartitionBy("Department")
            .OrderByDescending("Month")
            .MovingSum("Revenue", windowSize: 2);

        Assert.Equal(new[] { 300, 300, 100, 125, 75 }, IntValues(movingSum));
    }

    // Verifies that composite ordering uses all configured ordering columns for moving windows.
    [Fact]
    public void MovingAggregations_WithCompositeOrdering_UseAllOrderingColumns()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales", "Sales", "Sales" },
            Year = new[] { 2024, 2024, 2023, 2024 },
            Month = new[] { 2, 1, 12, 3 },
            Revenue = new[] { 20, 10, 5, 30 }
        });

        var movingSum = df.Window()
            .PartitionBy("Department")
            .OrderBy("Year")
            .ThenBy("Month")
            .MovingSum("Revenue", windowSize: 2);

        Assert.Equal(new[] { 30, 15, 5, 50 }, IntValues(movingSum));
    }

    // Verifies that fully equal ordering keys use source-row order as the moving tie-breaker.
    [Fact]
    public void MovingAggregations_WithEqualOrderingKeys_PreserveSourceRowStability()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            SortKey = new[] { 1, 1, 1 },
            Amount = new[] { 10, 20, 30 }
        });

        var movingSum = df.Window().OrderBy("SortKey").MovingSum("Amount", windowSize: 2);

        Assert.Equal(new[] { 10, 30, 50 }, IntValues(movingSum));
    }

    // Verifies that moving results remain aligned to the original source row positions.
    [Fact]
    public void MovingAggregations_ReturnValuesAlignedToSourceRows()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Third", "First", "Second" },
            CreatedAt = new[] { 3, 1, 2 },
            Amount = new[] { 30, 10, 20 }
        });

        var movingSum = df.Window().OrderBy("CreatedAt").MovingSum("Amount", windowSize: 2);

        Assert.Equal(new[] { 50, 10, 30 }, IntValues(movingSum));
    }

    // Verifies that null target values use the existing fail-fast aggregation behavior.
    [Fact]
    public void MovingAggregations_WithNullTargetValue_ThrowArgumentException()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = new[] { 1, 2 },
            Amount = new int?[] { 10, null }
        });

        Assert.Throws<ArgumentException>(() => df.Window().OrderBy("CreatedAt").MovingSum("Amount", windowSize: 2));
        Assert.Throws<ArgumentException>(() => df.Window().OrderBy("CreatedAt").MovingAverage("Amount", windowSize: 2));
    }

    // Verifies that MovingSum follows the existing numeric promotion contract.
    [Fact]
    public void MovingSum_WithSmallIntegerColumn_ReturnsPromotedIntSeries()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = new[] { 1, 2 },
            Amount = new byte[] { 10, 20 }
        });

        var movingSum = df.Window().OrderBy("CreatedAt").MovingSum("Amount", windowSize: 2);

        Assert.Equal(typeof(int), movingSum.DataType);
        Assert.Equal(new[] { 10, 30 }, IntValues(movingSum));
    }

    // Verifies that MovingAverage returns double results for numeric columns.
    [Fact]
    public void MovingAverage_WithNumericColumn_ReturnsDoubleSeries()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = new[] { 1, 2 },
            Amount = new[] { 10m, 20m }
        });

        var movingAverage = df.Window().OrderBy("CreatedAt").MovingAverage("Amount", windowSize: 2);

        Assert.Equal(typeof(double), movingAverage.DataType);
        Assert.Equal(new[] { 10d, 15d }, DoubleValues(movingAverage));
    }

    // Verifies that empty DataFrames produce empty moving aggregate series with operation result types.
    [Fact]
    public void MovingAggregations_WithEmptyDataFrame_ReturnEmptyTypedSeries()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = Array.Empty<int>(),
            Amount = Array.Empty<int>()
        });

        var movingSum = df.Window().OrderBy("CreatedAt").MovingSum("Amount", windowSize: 2);
        var movingAverage = df.Window().OrderBy("CreatedAt").MovingAverage("Amount", windowSize: 2);

        Assert.Equal(typeof(int), movingSum.DataType);
        Assert.Equal(typeof(double), movingAverage.DataType);
        Assert.Equal(0, movingSum.Count);
        Assert.Equal(0, movingAverage.Count);
    }

    // Verifies that single-row partitions return that row's value as the moving result.
    [Fact]
    public void MovingAggregations_WithSingleRowPartitions_ReturnOwnValues()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Ops" },
            Month = new[] { 1, 1 },
            Revenue = new[] { 100, 90 }
        });

        var movingSum = df.Window().PartitionBy("Department").OrderBy("Month").MovingSum("Revenue", windowSize: 3);
        var movingAverage = df.Window().PartitionBy("Department").OrderBy("Month").MovingAverage("Revenue", windowSize: 3);

        Assert.Equal(new[] { 100, 90 }, IntValues(movingSum));
        Assert.Equal(new[] { 100d, 90d }, DoubleValues(movingAverage));
    }

    // Verifies that zero and negative window sizes fail before moving aggregation calculation.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MovingAggregations_WithInvalidWindowSize_Throw(int windowSize)
    {
        var df = CreateRevenueDataFrame();

        Assert.Throws<ArgumentException>(() => df.Window().OrderBy("Month").MovingSum("Revenue", windowSize));
        Assert.Throws<ArgumentException>(() => df.Window().OrderBy("Month").MovingAverage("Revenue", windowSize));
    }

    // Verifies that unsupported target values fail instead of using string conversion or fallback logic.
    [Fact]
    public void MovingAggregations_WithUnsupportedValues_ThrowArgumentException()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = new[] { 1, 2 },
            Payload = new[] { new Payload(1), new Payload(2) }
        });

        Assert.Throws<ArgumentException>(() => df.Window().OrderBy("CreatedAt").MovingSum("Payload", windowSize: 2));
        Assert.Throws<ArgumentException>(() => df.Window().OrderBy("CreatedAt").MovingAverage("Payload", windowSize: 2));
    }

    // Verifies that invalid target column names fail before moving aggregation calculation.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MovingAggregations_WithInvalidTargetColumnName_Throw(string? columnName)
    {
        var df = CreateRevenueDataFrame();

        Assert.ThrowsAny<ArgumentException>(() => df.Window().OrderBy("Month").MovingSum(columnName!, windowSize: 2));
        Assert.ThrowsAny<ArgumentException>(() => df.Window().OrderBy("Month").MovingAverage(columnName!, windowSize: 2));
    }

    // Verifies that missing target columns use the existing column lookup failure.
    [Fact]
    public void MovingAggregations_WithMissingTargetColumn_ThrowKeyNotFoundException()
    {
        var df = CreateRevenueDataFrame();

        Assert.Throws<KeyNotFoundException>(() => df.Window().OrderBy("Month").MovingSum("Missing", windowSize: 2));
        Assert.Throws<KeyNotFoundException>(() => df.Window().OrderBy("Month").MovingAverage("Missing", windowSize: 2));
    }

    // Verifies that MovingSum and MovingAverage do not mutate source rows, source columns, or schema.
    [Fact]
    public void MovingAggregations_DoNotMutateSourceDataFrame()
    {
        var df = CreateRevenueDataFrame();
        var schema = df.Schema;
        var columnNames = ColumnNames(df);
        var revenue = IntColumn(df, "Revenue");

        _ = df.Window().PartitionBy("Department").OrderBy("Month").MovingSum("Revenue", windowSize: 2);
        _ = df.Window().PartitionBy("Department").OrderBy("Month").MovingAverage("Revenue", windowSize: 2);

        Assert.Same(schema, df.Schema);
        Assert.Equal(columnNames, ColumnNames(df));
        Assert.Equal(revenue, IntColumn(df, "Revenue"));
    }

    // Verifies that moving aggregate outputs can be appended through the existing Columns.Add API.
    [Fact]
    public void MovingAggregationResults_CanBeAddedAsColumns()
    {
        var df = CreateRevenueDataFrame();
        var movingSum = df.Window().PartitionBy("Department").OrderBy("Month").MovingSum("Revenue", windowSize: 2);

        df.Columns.Add("MovingTotal", movingSum);

        Assert.Equal(new[] { 100, 300, 300, 50, 125 }, IntValues(df["MovingTotal"]));
    }

    /// <summary>
    /// Creates a deterministic revenue table used to verify ordered moving aggregation behavior.
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
    /// Reads integer values from an untyped result series for moving sum assertions.
    /// </summary>
    private static int[] IntValues(ISeries series)
    {
        return Enumerable.Range(0, series.Count)
            .Select(index => (int)series.GetValue(index)!)
            .ToArray();
    }

    /// <summary>
    /// Reads double values from an untyped result series for moving average assertions.
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
