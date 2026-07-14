namespace Runiq.Data.DataFrameTests.Pivoting;

/// <summary>
/// Verifies DataFrame unpivot behavior, validation, ordering, type handling, and source immutability.
/// </summary>
public sealed class DataFrameUnpivotTests
{
    // Verifies that one id column and multiple value columns are converted to variable/value rows.
    [Fact]
    public void Unpivot_WithSingleIdAndMultipleValues_ReturnsUnpivotedDataFrame()
    {
        var df = CreateRevenueDataFrame();

        var result = df.Unpivot(
            idColumns: ["Department"],
            valueColumns: ["Q1", "Q2"],
            variableColumnName: "Quarter",
            valueColumnName: "Revenue");

        Assert.Equal(new[] { "Department", "Quarter", "Revenue" }, ColumnNames(result));
        Assert.Equal(new[] { "Engineering", "Sales", "Engineering", "Sales" }, Values<string>(result, "Department"));
        Assert.Equal(new[] { "Q1", "Q1", "Q2", "Q2" }, Values<string>(result, "Quarter"));
        Assert.Equal(new[] { 100, 80, 120, 90 }, Values<int>(result, "Revenue"));
    }

    // Verifies that multiple id columns are repeated unchanged for each value column group.
    [Fact]
    public void Unpivot_WithMultipleIdColumns_PreservesIdValues()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Sales" },
            Region = new[] { "North", "West" },
            Q1 = new[] { 100, 80 },
            Q2 = new[] { 120, 90 }
        });

        var result = df.Unpivot(["Department", "Region"], ["Q1", "Q2"], "Quarter", "Revenue");

        Assert.Equal(new[] { "Engineering", "Sales", "Engineering", "Sales" }, Values<string>(result, "Department"));
        Assert.Equal(new[] { "North", "West", "North", "West" }, Values<string>(result, "Region"));
    }

    // Verifies that an empty id column list produces only variable and value output columns.
    [Fact]
    public void Unpivot_WithEmptyIdColumns_ReturnsVariableAndValueColumnsOnly()
    {
        var df = CreateRevenueDataFrame();

        var result = df.Unpivot([], ["Q1", "Q2"], "Quarter", "Revenue");

        Assert.Equal(new[] { "Quarter", "Revenue" }, ColumnNames(result));
        Assert.Equal(new[] { "Q1", "Q1", "Q2", "Q2" }, Values<string>(result, "Quarter"));
    }

    // Verifies that value column order and source row order within each value column are preserved.
    [Fact]
    public void Unpivot_PreservesValueColumnOrderAndSourceRowOrder()
    {
        var df = CreateRevenueDataFrame();

        var result = df.Unpivot(["Department"], ["Q2", "Q1"], "Quarter", "Revenue");

        Assert.Equal(new[] { "Q2", "Q2", "Q1", "Q1" }, Values<string>(result, "Quarter"));
        Assert.Equal(new[] { 120, 90, 100, 80 }, Values<int>(result, "Revenue"));
    }

    // Verifies that result columns are ordered as id columns, variable column, and value column.
    [Fact]
    public void Unpivot_PreservesRequestedResultColumnOrder()
    {
        var df = CreateRevenueDataFrame();

        var result = df.Unpivot(["Department"], ["Q1"], "Quarter", "Revenue");

        Assert.Equal(new[] { "Department", "Quarter", "Revenue" }, ColumnNames(result));
    }

    // Verifies that null cells in value columns remain null in the result value column.
    [Fact]
    public void Unpivot_WithNullValueCells_PreservesNulls()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Sales" },
            Q1 = new int?[] { 100, null },
            Q2 = new int?[] { null, 90 }
        });

        var result = df.Unpivot(["Department"], ["Q1", "Q2"], "Quarter", "Revenue");

        Assert.Equal(new int?[] { 100, null, null, 90 }, Values<int?>(result, "Revenue"));
        Assert.Equal(typeof(int?), result.Schema.GetColumn("Revenue").DataType);
    }

    // Verifies that an empty source returns the requested output schema with zero rows.
    [Fact]
    public void Unpivot_WithEmptyDataFrame_ReturnsEmptyResultWithSchema()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = Array.Empty<string>(),
            Q1 = Array.Empty<int>(),
            Q2 = Array.Empty<int>()
        });

        var result = df.Unpivot(["Department"], ["Q1", "Q2"], "Quarter", "Revenue");

        Assert.Equal(0, result.Rows.Count());
        Assert.Equal(new[] { "Department", "Quarter", "Revenue" }, ColumnNames(result));
        Assert.Equal(typeof(int), result.Schema.GetColumn("Revenue").DataType);
    }

    // Verifies that unpivoting returns a new DataFrame and leaves the source schema and values unchanged.
    [Fact]
    public void Unpivot_ReturnsNewInstanceAndDoesNotMutateSource()
    {
        var df = CreateRevenueDataFrame();

        var result = df.Unpivot(["Department"], ["Q1", "Q2"], "Quarter", "Revenue");

        Assert.NotSame(df, result);
        Assert.Equal(new[] { "Department", "Q1", "Q2" }, ColumnNames(df));
        Assert.Equal(new[] { 100, 80 }, Values<int>(df, "Q1"));
    }

    // Verifies that invalid id column names fail fast before result creation.
    [Theory]
    [InlineData(null, typeof(ArgumentNullException))]
    [InlineData("", typeof(ArgumentException))]
    [InlineData("   ", typeof(ArgumentException))]
    [InlineData("Missing", typeof(KeyNotFoundException))]
    public void Unpivot_WithInvalidIdColumnName_Throws(string? columnName, Type exceptionType)
    {
        var df = CreateRevenueDataFrame();

        Assert.Throws(exceptionType, () => df.Unpivot([columnName!], ["Q1"], "Quarter", "Revenue"));
    }

    // Verifies that invalid value column names fail fast before result creation.
    [Theory]
    [InlineData(null, typeof(ArgumentNullException))]
    [InlineData("", typeof(ArgumentException))]
    [InlineData("   ", typeof(ArgumentException))]
    [InlineData("Missing", typeof(KeyNotFoundException))]
    public void Unpivot_WithInvalidValueColumnName_Throws(string? columnName, Type exceptionType)
    {
        var df = CreateRevenueDataFrame();

        Assert.Throws(exceptionType, () => df.Unpivot(["Department"], [columnName!], "Quarter", "Revenue"));
    }

    // Verifies that null collection arguments fail fast.
    [Fact]
    public void Unpivot_WithNullCollections_ThrowsArgumentNullException()
    {
        var df = CreateRevenueDataFrame();

        Assert.Throws<ArgumentNullException>(() => df.Unpivot(null!, ["Q1"], "Quarter", "Revenue"));
        Assert.Throws<ArgumentNullException>(() => df.Unpivot(["Department"], null!, "Quarter", "Revenue"));
    }

    // Verifies that the first implementation rejects an empty value column list.
    [Fact]
    public void Unpivot_WithEmptyValueColumns_ThrowsArgumentException()
    {
        var df = CreateRevenueDataFrame();

        Assert.Throws<ArgumentException>(() => df.Unpivot(["Department"], [], "Quarter", "Revenue"));
    }

    // Verifies that duplicate names in either role list are rejected as ambiguous.
    [Fact]
    public void Unpivot_WithDuplicateRoleNames_ThrowsArgumentException()
    {
        var df = CreateRevenueDataFrame();

        Assert.Throws<ArgumentException>(() => df.Unpivot(["Department", "Department"], ["Q1"], "Quarter", "Revenue"));
        Assert.Throws<ArgumentException>(() => df.Unpivot(["Department"], ["Q1", "Q1"], "Quarter", "Revenue"));
    }

    // Verifies that a source column cannot be assigned to both id and value roles.
    [Fact]
    public void Unpivot_WithColumnInBothRoles_ThrowsArgumentException()
    {
        var df = CreateRevenueDataFrame();

        Assert.Throws<ArgumentException>(() => df.Unpivot(["Department"], ["Department"], "Quarter", "Revenue"));
    }

    // Verifies that output column names cannot conflict with source columns or each other.
    [Fact]
    public void Unpivot_WithOutputColumnNameCollision_ThrowsArgumentException()
    {
        var df = CreateRevenueDataFrame();

        Assert.Throws<ArgumentException>(() => df.Unpivot(["Department"], ["Q1"], "Department", "Revenue"));
        Assert.Throws<ArgumentException>(() => df.Unpivot(["Department"], ["Q1"], "Quarter", "Q1"));
        Assert.Throws<ArgumentException>(() => df.Unpivot(["Department"], ["Q1"], "Result", "Result"));
    }

    // Verifies that compatible numeric value columns produce a common promoted result type.
    [Fact]
    public void Unpivot_WithCompatibleNumericColumns_ProducesCommonResultType()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Sales" },
            Q1 = new[] { 100, 80 },
            Q2 = new[] { 120.5m, 90.5m }
        });

        var result = df.Unpivot(["Department"], ["Q1", "Q2"], "Quarter", "Revenue");

        Assert.Equal(typeof(decimal), result.Schema.GetColumn("Revenue").DataType);
        Assert.Equal(new decimal[] { 100m, 80m, 120.5m, 90.5m }, Values<decimal>(result, "Revenue"));
    }

    // Verifies that incompatible mixed value types fail instead of falling back to strings.
    [Fact]
    public void Unpivot_WithIncompatibleMixedValueTypes_ThrowsArgumentException()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering" },
            Q1 = new[] { 100 },
            Q2 = new[] { "120" }
        });

        Assert.Throws<ArgumentException>(() => df.Unpivot(["Department"], ["Q1", "Q2"], "Quarter", "Revenue"));
    }

    // Verifies that unsupported complex value columns fail instead of using object ToString fallback.
    [Fact]
    public void Unpivot_WithUnsupportedComplexValues_ThrowsArgumentException()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering" },
            Q1 = new object[] { new PivotMarker("Q1") }
        });

        var exception = Assert.Throws<ArgumentException>(() => df.Unpivot(["Department"], ["Q1"], "Quarter", "Revenue"));

        Assert.Contains("unsupported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Verifies that null, empty, and whitespace output names fail before result creation.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Unpivot_WithInvalidOutputColumnName_Throws(string? outputName)
    {
        var df = CreateRevenueDataFrame();

        Assert.ThrowsAny<ArgumentException>(() => df.Unpivot(["Department"], ["Q1"], outputName!, "Revenue"));
        Assert.ThrowsAny<ArgumentException>(() => df.Unpivot(["Department"], ["Q1"], "Quarter", outputName!));
    }

    // Verifies that existing Pivot behavior remains available after adding Unpivot.
    [Fact]
    public void Unpivot_DoesNotRegressExistingPivotOperation()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Engineering" },
            Quarter = new[] { "Q1", "Q2" },
            Revenue = new[] { 100, 120 }
        });

        var result = df.Pivot("Department", "Quarter", "Revenue");

        Assert.Equal(new[] { "Department", "Q1", "Q2" }, ColumnNames(result));
    }

    // Verifies that existing PivotTable behavior remains available after adding Unpivot.
    [Fact]
    public void Unpivot_DoesNotRegressExistingPivotTableOperation()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Engineering" },
            Quarter = new[] { "Q1", "Q1" },
            Revenue = new[] { 100, 50 }
        });

        var result = df.PivotTable("Department", "Quarter", "Revenue").Sum();

        Assert.Equal(150, result["Q1"].GetValue(0));
    }

    /// <summary>
    /// Creates the canonical wide revenue fixture used by unpivot tests.
    /// </summary>
    private static global::Runiq.Data.DataFrame CreateRevenueDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Engineering", "Sales" },
            Q1 = new[] { 100, 80 },
            Q2 = new[] { 120, 90 }
        });
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
    /// Provides a deliberately unsupported value type for fail-fast conversion tests.
    /// </summary>
    private sealed record PivotMarker(string Name);
}
