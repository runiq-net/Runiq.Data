namespace Runiq.Data.DataFrameTests.Pivoting;

/// <summary>
/// Verifies non-aggregating DataFrame pivot behavior, validation, ordering, and source immutability.
/// </summary>
public sealed class DataFramePivotTests
{
    // Verifies that a simple string pivot spreads column values into first-seen dynamic columns.
    [Fact]
    public void Pivot_WithSimpleStringValues_ReturnsPivotedDataFrame()
    {
        var df = CreateRevenueDataFrame();

        var result = df.Pivot(index: "Department", columns: "Quarter", values: "Revenue");

        Assert.Equal(new[] { "Department", "Q1", "Q2" }, ColumnNames(result));
        Assert.Equal("Engineering", result["Department"].GetValue(0));
        Assert.Equal(100, result["Q1"].GetValue(0));
        Assert.Equal(120, result["Q2"].GetValue(0));
        Assert.Equal("Sales", result["Department"].GetValue(1));
        Assert.Equal(80, result["Q1"].GetValue(1));
        Assert.Equal(90, result["Q2"].GetValue(1));
    }

    // Verifies that result rows preserve the first appearance order of source index values.
    [Fact]
    public void Pivot_PreservesFirstSeenIndexOrder()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Engineering", "Sales", "Engineering" },
            Quarter = new[] { "Q2", "Q2", "Q1", "Q1" },
            Revenue = new[] { 90, 120, 80, 100 }
        });

        var result = df.Pivot("Department", "Quarter", "Revenue");

        Assert.Equal(new[] { "Sales", "Engineering" }, Values<string>(result, "Department"));
    }

    // Verifies that dynamic result columns preserve the first appearance order of source pivot values.
    [Fact]
    public void Pivot_PreservesFirstSeenDynamicColumnOrder()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Engineering", "Sales", "Engineering" },
            Quarter = new[] { "Q2", "Q2", "Q1", "Q1" },
            Revenue = new[] { 90, 120, 80, 100 }
        });

        var result = df.Pivot("Department", "Quarter", "Revenue");

        Assert.Equal(new[] { "Department", "Q2", "Q1" }, ColumnNames(result));
    }

    // Verifies that missing index and pivot column combinations are represented as null cells.
    [Fact]
    public void Pivot_WithMissingCombination_UsesNullCell()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Sales", "Sales" },
            Quarter = new[] { "Q1", "Q1", "Q2" },
            Revenue = new[] { 100, 80, 90 }
        });

        var result = df.Pivot("Department", "Quarter", "Revenue");

        Assert.Null(result["Q2"].GetValue(0));
    }

    // Verifies that null value cells remain null and are not confused with missing combinations.
    [Fact]
    public void Pivot_WithNullValueCell_PreservesNull()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Engineering" },
            Quarter = new[] { "Q1", "Q2" },
            Revenue = new int?[] { 100, null }
        });

        var result = df.Pivot("Department", "Quarter", "Revenue");

        Assert.Equal(100, result["Q1"].GetValue(0));
        Assert.Null(result["Q2"].GetValue(0));
    }

    // Verifies that duplicate index and pivot column combinations fail fast instead of selecting an arbitrary value.
    [Fact]
    public void Pivot_WithDuplicateIndexAndColumnCombination_ThrowsArgumentException()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Engineering" },
            Quarter = new[] { "Q1", "Q1" },
            Revenue = new[] { 100, 101 }
        });

        var exception = Assert.Throws<ArgumentException>(() => df.Pivot("Department", "Quarter", "Revenue"));

        Assert.Contains("Engineering", exception.Message);
        Assert.Contains("Q1", exception.Message);
    }

    // Verifies that duplicate combination failures direct callers to PivotTable for aggregation scenarios.
    [Fact]
    public void Pivot_WithDuplicateIndexAndColumnCombination_MessageMentionsPivotTable()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Engineering" },
            Quarter = new[] { "Q1", "Q1" },
            Revenue = new[] { 100, 101 }
        });

        var exception = Assert.Throws<ArgumentException>(() => df.Pivot("Department", "Quarter", "Revenue"));

        Assert.Contains("PivotTable", exception.Message);
        Assert.Contains("aggregation", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Verifies that invalid index arguments fail before pivot execution.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Pivot_WithInvalidIndexArgument_Throws(string? index)
    {
        var df = CreateRevenueDataFrame();

        Assert.ThrowsAny<ArgumentException>(() => df.Pivot(index!, "Quarter", "Revenue"));
    }

    // Verifies that invalid columns arguments fail before pivot execution.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Pivot_WithInvalidColumnsArgument_Throws(string? columns)
    {
        var df = CreateRevenueDataFrame();

        Assert.ThrowsAny<ArgumentException>(() => df.Pivot("Department", columns!, "Revenue"));
    }

    // Verifies that invalid values arguments fail before pivot execution.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Pivot_WithInvalidValuesArgument_Throws(string? values)
    {
        var df = CreateRevenueDataFrame();

        Assert.ThrowsAny<ArgumentException>(() => df.Pivot("Department", "Quarter", values!));
    }

    // Verifies that a missing index column uses the existing DataFrame missing-column failure behavior.
    [Fact]
    public void Pivot_WithMissingIndexColumn_ThrowsKeyNotFoundException()
    {
        var df = CreateRevenueDataFrame();

        Assert.Throws<KeyNotFoundException>(() => df.Pivot("Missing", "Quarter", "Revenue"));
    }

    // Verifies that a missing columns column uses the existing DataFrame missing-column failure behavior.
    [Fact]
    public void Pivot_WithMissingColumnsColumn_ThrowsKeyNotFoundException()
    {
        var df = CreateRevenueDataFrame();

        Assert.Throws<KeyNotFoundException>(() => df.Pivot("Department", "Missing", "Revenue"));
    }

    // Verifies that a missing values column uses the existing DataFrame missing-column failure behavior.
    [Fact]
    public void Pivot_WithMissingValuesColumn_ThrowsKeyNotFoundException()
    {
        var df = CreateRevenueDataFrame();

        Assert.Throws<KeyNotFoundException>(() => df.Pivot("Department", "Quarter", "Missing"));
    }

    // Verifies that the same source column cannot be assigned to multiple pivot roles.
    [Theory]
    [InlineData("Department", "Department", "Revenue")]
    [InlineData("Department", "Quarter", "Department")]
    [InlineData("Department", "Quarter", "Quarter")]
    public void Pivot_WithDuplicateRoleColumn_ThrowsArgumentException(string index, string columns, string values)
    {
        var df = CreateRevenueDataFrame();

        Assert.Throws<ArgumentException>(() => df.Pivot(index, columns, values));
    }

    // Verifies that null pivot column values fail because they cannot produce safe result column names.
    [Fact]
    public void Pivot_WithNullPivotColumnValue_ThrowsArgumentException()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering" },
            Quarter = new string?[] { null },
            Revenue = new[] { 100 }
        });

        var exception = Assert.Throws<ArgumentException>(() => df.Pivot("Department", "Quarter", "Revenue"));

        Assert.Contains("null", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Verifies that unsupported complex pivot values fail instead of using arbitrary ToString output.
    [Fact]
    public void Pivot_WithUnsupportedComplexPivotValue_ThrowsArgumentException()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering" },
            Quarter = new object[] { new PivotMarker("Q1") },
            Revenue = new[] { 100 }
        });

        var exception = Assert.Throws<ArgumentException>(() => df.Pivot("Department", "Quarter", "Revenue"));

        Assert.Contains("unsupported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Verifies that different pivot values producing the same result column name are rejected.
    [Fact]
    public void Pivot_WithColumnNameConversionCollision_ThrowsArgumentException()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Engineering" },
            Quarter = new object[] { 1, "1" },
            Revenue = new[] { 100, 120 }
        });

        var exception = Assert.Throws<ArgumentException>(() => df.Pivot("Department", "Quarter", "Revenue"));

        Assert.Contains("conflicts", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Verifies that generated pivot column names cannot conflict with the result index column name.
    [Fact]
    public void Pivot_WithPivotColumnNameMatchingIndexColumn_ThrowsArgumentException()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering" },
            Quarter = new[] { "Department" },
            Revenue = new[] { 100 }
        });

        var exception = Assert.Throws<ArgumentException>(() => df.Pivot("Department", "Quarter", "Revenue"));

        Assert.Contains("index column", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Verifies that pivoting returns a separate result and leaves the source DataFrame unchanged.
    [Fact]
    public void Pivot_DoesNotMutateSourceDataFrame()
    {
        var df = CreateRevenueDataFrame();

        var result = df.Pivot("Department", "Quarter", "Revenue");

        Assert.NotSame(df, result);
        Assert.Equal(new[] { "Department", "Quarter", "Revenue" }, ColumnNames(df));
        Assert.Equal(new[] { "Engineering", "Engineering", "Sales", "Sales" }, Values<string>(df, "Department"));
        Assert.Equal(new[] { "Department", "Q1", "Q2" }, ColumnNames(result));
    }

    // Verifies that pivoting always creates a new DataFrame instance.
    [Fact]
    public void Pivot_ReturnsNewDataFrameInstance()
    {
        var df = CreateRevenueDataFrame();

        var result = df.Pivot("Department", "Quarter", "Revenue");

        Assert.NotSame(df, result);
    }

    // Verifies that an empty source returns an empty DataFrame containing only the index column.
    [Fact]
    public void Pivot_WithEmptyDataFrame_ReturnsEmptyIndexOnlyResult()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = Array.Empty<string>(),
            Quarter = Array.Empty<string>(),
            Revenue = Array.Empty<int>()
        });

        var result = df.Pivot("Department", "Quarter", "Revenue");

        Assert.Equal(0, result.Rows.Count());
        Assert.Equal(new[] { "Department" }, ColumnNames(result));
    }

    // Verifies that numeric pivot values use deterministic invariant result column names.
    [Fact]
    public void Pivot_WithNumericPivotValues_UsesDeterministicColumnNames()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Engineering" },
            Quarter = new[] { 1, 2 },
            Revenue = new[] { 100, 120 }
        });

        var result = df.Pivot("Department", "Quarter", "Revenue");

        Assert.Equal(new[] { "Department", "1", "2" }, ColumnNames(result));
    }

    // Verifies that Guid pivot values use deterministic result column names.
    [Fact]
    public void Pivot_WithGuidPivotValues_UsesDeterministicColumnNames()
    {
        var q1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var q2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Engineering" },
            Quarter = new[] { q1, q2 },
            Revenue = new[] { 100, 120 }
        });

        var result = df.Pivot("Department", "Quarter", "Revenue");

        Assert.Equal(new[] { "Department", q1.ToString("D"), q2.ToString("D") }, ColumnNames(result));
    }

    // Verifies that enum pivot values use deterministic result column names.
    [Fact]
    public void Pivot_WithEnumPivotValues_UsesDeterministicColumnNames()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Engineering" },
            Quarter = new[] { FiscalQuarter.Q1, FiscalQuarter.Q2 },
            Revenue = new[] { 100, 120 }
        });

        var result = df.Pivot("Department", "Quarter", "Revenue");

        Assert.Equal(new[] { "Department", "Q1", "Q2" }, ColumnNames(result));
    }

    // Verifies that whitespace pivot result names are rejected as invalid DataFrame column names.
    [Fact]
    public void Pivot_WithWhitespacePivotColumnName_ThrowsArgumentException()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering" },
            Quarter = new[] { "   " },
            Revenue = new[] { 100 }
        });

        var exception = Assert.Throws<ArgumentException>(() => df.Pivot("Department", "Quarter", "Revenue"));

        Assert.Contains("non-empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates the canonical revenue fixture used by most pivot tests.
    /// </summary>
    /// <returns>A DataFrame containing Department, Quarter, and Revenue columns.</returns>
    private static global::Runiq.Data.DataFrame CreateRevenueDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Engineering", "Sales", "Sales" },
            Quarter = new[] { "Q1", "Q2", "Q1", "Q2" },
            Revenue = new[] { 100, 120, 80, 90 }
        });
    }

    /// <summary>
    /// Reads a typed DataFrame column into an array while preserving row order.
    /// </summary>
    /// <typeparam name="T">The expected CLR type for non-null column values.</typeparam>
    /// <param name="df">The DataFrame whose column is read.</param>
    /// <param name="columnName">The column name to read.</param>
    /// <returns>The column values in row order.</returns>
    private static T[] Values<T>(global::Runiq.Data.DataFrame df, string columnName)
    {
        return Enumerable.Range(0, df.Rows.Count())
            .Select(index => (T)df[columnName].GetValue(index)!)
            .ToArray();
    }

    /// <summary>
    /// Reads DataFrame column names in schema order.
    /// </summary>
    /// <param name="df">The DataFrame whose columns are inspected.</param>
    /// <returns>The ordered column names.</returns>
    private static string[] ColumnNames(global::Runiq.Data.DataFrame df)
    {
        return df.Columns.Select(static column => column.Name).ToArray();
    }

    /// <summary>
    /// Provides a deliberately unsupported pivot value type for fail-fast conversion tests.
    /// </summary>
    /// <param name="Name">A marker name that must not be converted through ToString fallback.</param>
    private sealed record PivotMarker(string Name);

    /// <summary>
    /// Provides supported enum values for deterministic pivot column name tests.
    /// </summary>
    private enum FiscalQuarter
    {
        /// <summary>
        /// First fiscal quarter.
        /// </summary>
        Q1,

        /// <summary>
        /// Second fiscal quarter.
        /// </summary>
        Q2
    }
}
