using Runiq.Data.Series;
using Runiq.Data.Windowing;

namespace Runiq.Data.DataFrameTests.Windowing;

/// <summary>
/// Verifies window partition aggregation alignment, type, validation, null, and mutation contracts.
/// </summary>
public sealed class WindowPartitionAggregationTests
{
    // Verifies that global window aggregations treat the whole DataFrame as one partition.
    [Fact]
    public void PartitionAggregations_WithGlobalWindow_ReturnAggregateForEveryRow()
    {
        var df = CreateEmployeeDataFrame();

        var sum = df.Window().Sum("Salary");
        var average = df.Window().Average("Salary");
        var min = df.Window().Min("Salary");
        var max = df.Window().Max("Salary");

        Assert.Equal(new[] { 555, 555, 555, 555, 555 }, IntValues(sum));
        Assert.Equal(new[] { 111d, 111d, 111d, 111d, 111d }, DoubleValues(average));
        Assert.Equal(new[] { 90, 90, 90, 90, 90 }, IntValues(min));
        Assert.Equal(new[] { 150, 150, 150, 150, 150 }, IntValues(max));
    }

    // Verifies that partitioned window aggregations calculate each partition independently.
    [Fact]
    public void PartitionAggregations_WithPartition_ReturnIndependentPartitionResults()
    {
        var df = CreateEmployeeDataFrame();

        var sum = df.Window().PartitionBy("Department").Sum("Salary");
        var average = df.Window().PartitionBy("Department").Average("Salary");
        var min = df.Window().PartitionBy("Department").Min("Salary");
        var max = df.Window().PartitionBy("Department").Max("Salary");

        Assert.Equal(new[] { 310, 310, 310, 95, 150 }, IntValues(sum));
        Assert.Equal(new[] { 310d / 3d, 310d / 3d, 310d / 3d, 95d, 150d }, DoubleValues(average));
        Assert.Equal(new[] { 90, 90, 90, 95, 150 }, IntValues(min));
        Assert.Equal(new[] { 120, 120, 120, 95, 150 }, IntValues(max));
    }

    // Verifies that composite partition keys use the existing PartitionBy semantics.
    [Fact]
    public void PartitionAggregations_WithCompositePartition_ReturnCompositePartitionResults()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Country = new[] { "TR", "TR", "TR", "US" },
            Department = new[] { "Sales", "Sales", "Ops", "Sales" },
            Salary = new[] { 100, 90, 80, 70 }
        });

        var sum = df.Window().PartitionBy("Country", "Department").Sum("Salary");

        Assert.Equal(new[] { 190, 190, 80, 70 }, IntValues(sum));
    }

    // Verifies that aggregate results remain aligned to original source row positions.
    [Fact]
    public void PartitionAggregations_ReturnValuesAlignedToSourceRows()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Ops", "Sales", "Ops" },
            Salary = new[] { 100, 80, 90, 70 }
        });

        var sum = df.Window().PartitionBy("Department").Sum("Salary");

        Assert.Equal(new[] { 190, 150, 190, 150 }, IntValues(sum));
    }

    // Verifies that null partition keys are valid window partitions and are grouped together.
    [Fact]
    public void PartitionAggregations_WithNullPartitionKey_GroupNullKeysTogether()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new string?[] { null, "Sales", null, "Sales" },
            Salary = new[] { 100, 80, 90, 70 }
        });

        var sum = df.Window().PartitionBy("Department").Sum("Salary");

        Assert.Equal(new[] { 190, 150, 190, 150 }, IntValues(sum));
    }

    // Verifies that null aggregate values use the existing fail-fast DataFrame aggregation behavior.
    [Theory]
    [MemberData(nameof(WindowAggregationActions))]
    public void PartitionAggregations_WithNullValue_ThrowArgumentException(Func<WindowBuilder, string, ISeries> aggregate)
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales" },
            Salary = new int?[] { 100, null }
        });

        Assert.Throws<ArgumentException>(() => aggregate(df.Window().PartitionBy("Department"), "Salary"));
    }

    // Verifies that Sum follows the existing numeric promotion contract for window results.
    [Fact]
    public void Sum_WithSmallIntegerColumn_ReturnsPromotedIntSeries()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales" },
            Salary = new byte[] { 10, 20 }
        });

        var sum = df.Window().PartitionBy("Department").Sum("Salary");

        Assert.Equal(typeof(int), sum.DataType);
        Assert.Equal(new[] { 30, 30 }, IntValues(sum));
    }

    // Verifies that Average returns double results for numeric window partitions.
    [Fact]
    public void Average_WithNumericColumn_ReturnsDoubleSeries()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales" },
            Salary = new[] { 10m, 20m }
        });

        var average = df.Window().PartitionBy("Department").Average("Salary");

        Assert.Equal(typeof(double), average.DataType);
        Assert.Equal(new[] { 15d, 15d }, DoubleValues(average));
    }

    // Verifies that Min and Max preserve comparable source types such as string and DateTime.
    [Fact]
    public void MinAndMax_WithComparableColumns_PreserveSourceTypes()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales", "Sales" },
            Name = new[] { "Zeynep", "Ali", "Mehmet" },
            CreatedAt = new[]
            {
                new DateTime(2024, 1, 2),
                new DateTime(2024, 1, 1),
                new DateTime(2024, 1, 3)
            }
        });

        var minName = df.Window().PartitionBy("Department").Min("Name");
        var maxCreatedAt = df.Window().PartitionBy("Department").Max("CreatedAt");

        Assert.Equal(typeof(string), minName.DataType);
        Assert.Equal(typeof(DateTime), maxCreatedAt.DataType);
        Assert.Equal(new[] { "Ali", "Ali", "Ali" }, StringValues(minName));
        Assert.Equal(
            new[] { new DateTime(2024, 1, 3), new DateTime(2024, 1, 3), new DateTime(2024, 1, 3) },
            DateTimeValues(maxCreatedAt));
    }

    // Verifies that unsupported aggregation types fail before returning a result.
    [Fact]
    public void PartitionAggregations_WithUnsupportedValues_ThrowArgumentException()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales" },
            Payload = new[] { new Payload(1), new Payload(2) }
        });

        Assert.Throws<ArgumentException>(() => df.Window().PartitionBy("Department").Sum("Payload"));
        Assert.Throws<ArgumentException>(() => df.Window().PartitionBy("Department").Average("Payload"));
        Assert.Throws<ArgumentException>(() => df.Window().PartitionBy("Department").Min("Payload"));
        Assert.Throws<ArgumentException>(() => df.Window().PartitionBy("Department").Max("Payload"));
    }

    // Verifies that empty DataFrames return empty series with the operation result type.
    [Fact]
    public void PartitionAggregations_WithEmptyDataFrame_ReturnEmptyTypedSeries()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = Array.Empty<string>(),
            Salary = Array.Empty<int>(),
            Name = Array.Empty<string>()
        });

        var sum = df.Window().PartitionBy("Department").Sum("Salary");
        var average = df.Window().PartitionBy("Department").Average("Salary");
        var min = df.Window().PartitionBy("Department").Min("Name");
        var max = df.Window().PartitionBy("Department").Max("Name");

        Assert.Equal(typeof(int), sum.DataType);
        Assert.Equal(typeof(double), average.DataType);
        Assert.Equal(typeof(string), min.DataType);
        Assert.Equal(typeof(string), max.DataType);
        Assert.Equal(0, sum.Count);
        Assert.Equal(0, average.Count);
        Assert.Equal(0, min.Count);
        Assert.Equal(0, max.Count);
    }

    // Verifies that single-row partitions return that row's value as the aggregate result.
    [Fact]
    public void PartitionAggregations_WithSingleRowPartitions_ReturnOwnValues()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Ops" },
            Salary = new[] { 100, 90 }
        });

        var sum = df.Window().PartitionBy("Department").Sum("Salary");
        var average = df.Window().PartitionBy("Department").Average("Salary");
        var min = df.Window().PartitionBy("Department").Min("Salary");
        var max = df.Window().PartitionBy("Department").Max("Salary");

        Assert.Equal(new[] { 100, 90 }, IntValues(sum));
        Assert.Equal(new[] { 100d, 90d }, DoubleValues(average));
        Assert.Equal(new[] { 100, 90 }, IntValues(min));
        Assert.Equal(new[] { 100, 90 }, IntValues(max));
    }

    // Verifies that window aggregations do not mutate source rows, source columns, or schema.
    [Fact]
    public void PartitionAggregations_DoNotMutateSourceDataFrame()
    {
        var df = CreateEmployeeDataFrame();
        var schema = df.Schema;
        var columnNames = ColumnNames(df);
        var salaries = IntColumn(df, "Salary");

        _ = df.Window().PartitionBy("Department").Sum("Salary");
        _ = df.Window().PartitionBy("Department").Average("Salary");
        _ = df.Window().PartitionBy("Department").Min("Salary");
        _ = df.Window().PartitionBy("Department").Max("Salary");

        Assert.Same(schema, df.Schema);
        Assert.Equal(columnNames, ColumnNames(df));
        Assert.Equal(salaries, IntColumn(df, "Salary"));
    }

    // Verifies that invalid target column names use the existing column validation behavior.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PartitionAggregations_WithInvalidTargetColumnName_Throw(string? columnName)
    {
        var df = CreateEmployeeDataFrame();

        Assert.ThrowsAny<ArgumentException>(() => df.Window().Sum(columnName!));
        Assert.ThrowsAny<ArgumentException>(() => df.Window().Average(columnName!));
        Assert.ThrowsAny<ArgumentException>(() => df.Window().Min(columnName!));
        Assert.ThrowsAny<ArgumentException>(() => df.Window().Max(columnName!));
    }

    // Verifies that missing target columns use the existing column lookup failure.
    [Fact]
    public void PartitionAggregations_WithMissingTargetColumn_ThrowKeyNotFoundException()
    {
        var df = CreateEmployeeDataFrame();

        Assert.Throws<KeyNotFoundException>(() => df.Window().Sum("Missing"));
        Assert.Throws<KeyNotFoundException>(() => df.Window().Average("Missing"));
        Assert.Throws<KeyNotFoundException>(() => df.Window().Min("Missing"));
        Assert.Throws<KeyNotFoundException>(() => df.Window().Max("Missing"));
    }

    // Verifies that partition aggregation results can be appended through the existing Columns.Add API.
    [Fact]
    public void PartitionAggregationResults_CanBeAddedAsColumns()
    {
        var df = CreateEmployeeDataFrame();
        var sum = df.Window().PartitionBy("Department").Sum("Salary");

        df.Columns.Add("DepartmentTotal", sum);

        Assert.Equal(new[] { 310, 310, 310, 95, 150 }, IntValues(df["DepartmentTotal"]));
    }

    /// <summary>
    /// Supplies all window partition aggregate terminal operations for shared validation tests.
    /// </summary>
    public static TheoryData<Func<WindowBuilder, string, ISeries>> WindowAggregationActions()
    {
        return new TheoryData<Func<WindowBuilder, string, ISeries>>
        {
            (window, columnName) => window.Sum(columnName),
            (window, columnName) => window.Average(columnName),
            (window, columnName) => window.Min(columnName),
            (window, columnName) => window.Max(columnName)
        };
    }

    /// <summary>
    /// Creates a deterministic employee table used to verify aggregate partitioning and alignment.
    /// </summary>
    private static global::Runiq.Data.DataFrame CreateEmployeeDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales", "Sales", "Ops", "Finance" },
            Salary = new[] { 100, 90, 120, 95, 150 }
        });
    }

    /// <summary>
    /// Reads integer values from an untyped result series for aggregate assertions.
    /// </summary>
    private static int[] IntValues(ISeries series)
    {
        return Enumerable.Range(0, series.Count)
            .Select(index => (int)series.GetValue(index)!)
            .ToArray();
    }

    /// <summary>
    /// Reads double values from an untyped result series for average assertions.
    /// </summary>
    private static double[] DoubleValues(ISeries series)
    {
        return Enumerable.Range(0, series.Count)
            .Select(index => (double)series.GetValue(index)!)
            .ToArray();
    }

    /// <summary>
    /// Reads DateTime values from an untyped result series for comparable aggregation assertions.
    /// </summary>
    private static DateTime[] DateTimeValues(ISeries series)
    {
        return Enumerable.Range(0, series.Count)
            .Select(index => (DateTime)series.GetValue(index)!)
            .ToArray();
    }

    /// <summary>
    /// Reads string values from an untyped result series for comparable aggregation assertions.
    /// </summary>
    private static string?[] StringValues(ISeries series)
    {
        return Enumerable.Range(0, series.Count)
            .Select(index => (string?)series.GetValue(index))
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
