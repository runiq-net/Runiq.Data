namespace Runiq.Data.DataFrameTests.Pivoting;

/// <summary>
/// Verifies aggregated PivotTable behavior, validation, ordering, and source immutability.
/// </summary>
public sealed class DataFramePivotTableTests
{
    // Verifies that Sum aggregates duplicate index and pivot combinations without rejecting them.
    [Fact]
    public void PivotTableSum_WithDuplicateCombinations_AggregatesValues()
    {
        var df = CreateDuplicateRevenueDataFrame();

        var result = df.PivotTable(index: "Department", columns: "Quarter", values: "Revenue").Sum();

        Assert.Equal(new[] { "Department", "Q1", "Q2" }, ColumnNames(result));
        Assert.Equal("Engineering", result["Department"].GetValue(0));
        Assert.Equal(150, result["Q1"].GetValue(0));
        Assert.Equal(120, result["Q2"].GetValue(0));
        Assert.Equal("Sales", result["Department"].GetValue(1));
        Assert.Equal(80, result["Q1"].GetValue(1));
        Assert.Null(result["Q2"].GetValue(1));
    }

    // Verifies that Average, Min, and Max aggregate duplicate combinations with existing aggregation semantics.
    [Fact]
    public void PivotTableTerminalAggregations_WithDuplicateCombinations_AggregateValues()
    {
        var df = CreateDuplicateRevenueDataFrame();

        var average = df.PivotTable("Department", "Quarter", "Revenue").Average();
        var min = df.PivotTable("Department", "Quarter", "Revenue").Min();
        var max = df.PivotTable("Department", "Quarter", "Revenue").Max();

        Assert.Equal(75d, average["Q1"].GetValue(0));
        Assert.Equal(50, min["Q1"].GetValue(0));
        Assert.Equal(100, max["Q1"].GetValue(0));
    }

    // Verifies that non-duplicate input produces the expected pivot table shape and values.
    [Fact]
    public void PivotTableSum_WithoutDuplicateCombinations_ReturnsPivotedValues()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Engineering", "Sales" },
            Quarter = new[] { "Q1", "Q2", "Q1" },
            Revenue = new[] { 100, 120, 80 }
        });

        var result = df.PivotTable("Department", "Quarter", "Revenue").Sum();

        Assert.Equal(new object?[] { 100, 80 }, Values<object?>(result, "Q1"));
        Assert.Equal(new object?[] { 120, null }, Values<object?>(result, "Q2"));
    }

    // Verifies that first-seen index rows and dynamic pivot columns are preserved in the result.
    [Fact]
    public void PivotTableSum_PreservesFirstSeenRowAndColumnOrder()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Engineering", "Sales", "Engineering" },
            Quarter = new[] { "Q2", "Q2", "Q1", "Q1" },
            Revenue = new[] { 90, 120, 80, 100 }
        });

        var result = df.PivotTable("Department", "Quarter", "Revenue").Sum();

        Assert.Equal(new[] { "Department", "Q2", "Q1" }, ColumnNames(result));
        Assert.Equal(new[] { "Sales", "Engineering" }, Values<string>(result, "Department"));
    }

    // Verifies that PivotTable returns independent results and leaves the source DataFrame unchanged.
    [Fact]
    public void PivotTableTerminalCalls_ReturnIndependentResultsAndDoNotMutateSource()
    {
        var df = CreateDuplicateRevenueDataFrame();
        var builder = df.PivotTable("Department", "Quarter", "Revenue");

        var first = builder.Sum();
        var second = builder.Sum();

        Assert.NotSame(first, second);
        Assert.Equal(new[] { "Department", "Quarter", "Revenue" }, ColumnNames(df));
        Assert.Equal(new[] { "Engineering", "Engineering", "Engineering", "Sales" }, Values<string>(df, "Department"));
    }

    // Verifies that an empty source with valid columns returns an empty result containing only the index column.
    [Fact]
    public void PivotTable_WithEmptyDataFrame_ReturnsEmptyIndexOnlyResultForEveryAggregation()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = Array.Empty<string>(),
            Quarter = Array.Empty<string>(),
            Revenue = Array.Empty<int>()
        });
        var builder = df.PivotTable("Department", "Quarter", "Revenue");

        AssertEmptyIndexOnly(builder.Sum());
        AssertEmptyIndexOnly(builder.Average());
        AssertEmptyIndexOnly(builder.Min());
        AssertEmptyIndexOnly(builder.Max());
    }

    // Verifies that invalid role names and missing columns fail before PivotTable execution.
    [Theory]
    [InlineData(null, "Quarter", "Revenue", typeof(ArgumentNullException))]
    [InlineData("", "Quarter", "Revenue", typeof(ArgumentException))]
    [InlineData("   ", "Quarter", "Revenue", typeof(ArgumentException))]
    [InlineData("Missing", "Quarter", "Revenue", typeof(KeyNotFoundException))]
    [InlineData("Department", null, "Revenue", typeof(ArgumentNullException))]
    [InlineData("Department", "", "Revenue", typeof(ArgumentException))]
    [InlineData("Department", "Missing", "Revenue", typeof(KeyNotFoundException))]
    [InlineData("Department", "Quarter", null, typeof(ArgumentNullException))]
    [InlineData("Department", "Quarter", "", typeof(ArgumentException))]
    [InlineData("Department", "Quarter", "Missing", typeof(KeyNotFoundException))]
    public void PivotTable_WithInvalidArguments_Throws(string? index, string? columns, string? values, Type exceptionType)
    {
        var df = CreateDuplicateRevenueDataFrame();

        Assert.Throws(exceptionType, () => df.PivotTable(index!, columns!, values!));
    }

    // Verifies that the same source column cannot be assigned to more than one PivotTable role.
    [Theory]
    [InlineData("Department", "Department", "Revenue")]
    [InlineData("Department", "Quarter", "Department")]
    [InlineData("Department", "Quarter", "Quarter")]
    public void PivotTable_WithDuplicateRoleColumn_ThrowsArgumentException(string index, string columns, string values)
    {
        var df = CreateDuplicateRevenueDataFrame();

        Assert.Throws<ArgumentException>(() => df.PivotTable(index, columns, values));
    }

    // Verifies that null and unsupported pivot values fail because they cannot produce safe output columns.
    [Fact]
    public void PivotTable_WithInvalidPivotValues_ThrowsArgumentException()
    {
        var nullPivot = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering" },
            Quarter = new string?[] { null },
            Revenue = new[] { 100 }
        });
        var unsupportedPivot = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering" },
            Quarter = new object[] { new PivotMarker("Q1") },
            Revenue = new[] { 100 }
        });

        Assert.Throws<ArgumentException>(() => nullPivot.PivotTable("Department", "Quarter", "Revenue").Sum());
        Assert.Throws<ArgumentException>(() => unsupportedPivot.PivotTable("Department", "Quarter", "Revenue").Sum());
    }

    // Verifies that generated output column collisions fail fast for PivotTable like they do for Pivot.
    [Fact]
    public void PivotTable_WithOutputColumnCollision_ThrowsArgumentException()
    {
        var conversionCollision = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Engineering" },
            Quarter = new object[] { 1, "1" },
            Revenue = new[] { 100, 120 }
        });
        var indexCollision = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering" },
            Quarter = new[] { "Department" },
            Revenue = new[] { 100 }
        });

        Assert.Throws<ArgumentException>(() => conversionCollision.PivotTable("Department", "Quarter", "Revenue").Sum());
        Assert.Throws<ArgumentException>(() => indexCollision.PivotTable("Department", "Quarter", "Revenue").Sum());
    }

    // Verifies that numeric promotion, null handling, and unsupported values use existing aggregation behavior.
    [Fact]
    public void PivotTableAggregations_UseExistingAggregationSemantics()
    {
        var numeric = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Engineering" },
            Quarter = new[] { "Q1", "Q1" },
            Value = new byte[] { 1, 2 }
        });
        var nullValue = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Engineering" },
            Quarter = new[] { "Q1", "Q1" },
            Value = new int?[] { 1, null }
        });
        var unsupported = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering" },
            Quarter = new[] { "Q1" },
            Value = new object[] { new PivotMarker("Value") }
        });
        var comparable = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Engineering" },
            Quarter = new[] { "Q1", "Q1" },
            Value = new[] { "beta", "alpha" }
        });
        var incompatible = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Engineering" },
            Quarter = new[] { "Q1", "Q1" },
            Value = new IComparable[] { 1, "alpha" }
        });

        Assert.Equal(3, numeric.PivotTable("Department", "Quarter", "Value").Sum()["Q1"].GetValue(0));
        Assert.Equal("alpha", comparable.PivotTable("Department", "Quarter", "Value").Min()["Q1"].GetValue(0));
        Assert.Throws<ArgumentException>(() => nullValue.PivotTable("Department", "Quarter", "Value").Sum());
        Assert.Throws<ArgumentException>(() => unsupported.PivotTable("Department", "Quarter", "Value").Min());
        Assert.Throws<ArgumentException>(() => incompatible.PivotTable("Department", "Quarter", "Value").Min());
    }

    // Verifies that PivotTable and Pivot share deterministic pivot column formatting rules.
    [Fact]
    public void PivotTable_WithSupportedPivotValues_UsesPivotColumnFormatting()
    {
        var quarterId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Engineering" },
            Quarter = new object[] { 1, quarterId },
            Revenue = new[] { 100, 120 }
        });

        var result = df.PivotTable("Department", "Quarter", "Revenue").Sum();

        Assert.Equal(new[] { "Department", "1", quarterId.ToString("D") }, ColumnNames(result));
    }

    // Verifies that PivotTable exposes only the agreed builder terminal API surface.
    [Fact]
    public void PivotTableBuilder_DoesNotExposeOutOfScopeTerminalApis()
    {
        var builderType = typeof(global::Runiq.Data.PivotTableBuilder);
        var publicMethodNames = builderType.GetMethods()
            .Where(method => method.DeclaringType == builderType)
            .Select(static method => method.Name)
            .OrderBy(static name => name)
            .ToArray();

        Assert.Equal(new[] { "Average", "Max", "Min", "Sum" }, publicMethodNames);
        Assert.Null(typeof(global::Runiq.Data.DataFrame).GetMethod("PivotTable", [typeof(string), typeof(string), typeof(string), typeof(string)]));
    }

    /// <summary>
    /// Creates the canonical duplicate revenue fixture used by PivotTable aggregation tests.
    /// </summary>
    private static global::Runiq.Data.DataFrame CreateDuplicateRevenueDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Engineering", "Engineering", "Sales" },
            Quarter = new[] { "Q1", "Q1", "Q2", "Q1" },
            Revenue = new[] { 100, 50, 120, 80 }
        });
    }

    /// <summary>
    /// Verifies the empty PivotTable result shape shared by all terminal aggregations.
    /// </summary>
    private static void AssertEmptyIndexOnly(global::Runiq.Data.DataFrame result)
    {
        Assert.Equal(0, result.Rows.Count());
        Assert.Equal(new[] { "Department" }, ColumnNames(result));
    }

    /// <summary>
    /// Reads a typed DataFrame column into an array while preserving row order.
    /// </summary>
    private static T[] Values<T>(global::Runiq.Data.DataFrame df, string columnName)
    {
        return Enumerable.Range(0, df.Rows.Count())
            .Select(index => (T)df[columnName].GetValue(index)!)
            .ToArray();
    }

    /// <summary>
    /// Reads DataFrame column names in schema order.
    /// </summary>
    private static string[] ColumnNames(global::Runiq.Data.DataFrame df)
    {
        return df.Columns.Select(static column => column.Name).ToArray();
    }

    /// <summary>
    /// Provides a deliberately unsupported pivot value type for fail-fast conversion tests.
    /// </summary>
    private sealed record PivotMarker(string Name);
}
