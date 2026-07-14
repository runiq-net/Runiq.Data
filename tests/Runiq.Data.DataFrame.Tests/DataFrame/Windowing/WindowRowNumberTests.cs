using Runiq.Data.Series;
using Runiq.Data.Windowing;

namespace Runiq.Data.DataFrameTests.Windowing;

/// <summary>
/// Verifies window RowNumber partitioning, ordering, alignment, validation, and mutation contracts.
/// </summary>
public sealed class WindowRowNumberTests
{
    // Verifies that global RowNumber assigns one-based values by ascending ordering.
    [Fact]
    public void RowNumber_WithGlobalAscendingOrdering_ReturnsOneBasedValues()
    {
        var df = CreateEmployeeDataFrame();

        var rowNumbers = df.Window()
            .OrderBy("Salary")
            .RowNumber();

        Assert.Equal(new[] { 4, 1, 3, 2, 5 }, Values(rowNumbers));
    }

    // Verifies that global RowNumber assigns one-based values by descending ordering.
    [Fact]
    public void RowNumber_WithGlobalDescendingOrdering_ReturnsOneBasedValues()
    {
        var df = CreateEmployeeDataFrame();

        var rowNumbers = df.Window()
            .OrderByDescending("Salary")
            .RowNumber();

        Assert.Equal(new[] { 2, 5, 3, 4, 1 }, Values(rowNumbers));
    }

    // Verifies that partitioned RowNumber restarts at one for each key value.
    [Fact]
    public void RowNumber_WithPartition_RestartsInsideEachPartition()
    {
        var df = CreateEmployeeDataFrame();

        var rowNumbers = df.Window()
            .PartitionBy("Department")
            .OrderByDescending("Salary")
            .RowNumber();

        Assert.Equal(new[] { 1, 3, 2, 1, 1 }, Values(rowNumbers));
    }

    // Verifies that multiple partitions are calculated independently while staying source-row aligned.
    [Fact]
    public void RowNumber_WithMultiplePartitions_ReturnsAlignedValues()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales", "Sales", "Ops", "Ops", "Ops" },
            Region = new[] { "West", "East", "West", "West", "East", "East" },
            Salary = new[] { 100, 300, 200, 30, 20, 10 }
        });

        var rowNumbers = df.Window()
            .PartitionBy("Department", "Region")
            .OrderBy("Salary")
            .RowNumber();

        Assert.Equal(new[] { 1, 1, 2, 1, 2, 1 }, Values(rowNumbers));
    }

    // Verifies that composite ordering uses later ordering columns only when earlier values tie.
    [Fact]
    public void RowNumber_WithCompositeOrdering_UsesThenByColumns()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales", "Sales", "Sales" },
            Salary = new[] { 100, 100, 200, 100 },
            Name = new[] { "Zeynep", "Ali", "Mehmet", "Ayse" }
        });

        var rowNumbers = df.Window()
            .PartitionBy("Department")
            .OrderByDescending("Salary")
            .ThenBy("Name")
            .RowNumber();

        Assert.Equal(new[] { 4, 2, 1, 3 }, Values(rowNumbers));
    }

    // Verifies that ascending and descending ordering clauses can be combined in one definition.
    [Fact]
    public void RowNumber_WithAscendingAndDescendingOrderingCombination_OrdersByEachDirection()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales", "Sales", "Sales" },
            Grade = new[] { 2, 1, 1, 2 },
            Salary = new[] { 100, 100, 200, 300 }
        });

        var rowNumbers = df.Window()
            .OrderBy("Grade")
            .ThenByDescending("Salary")
            .RowNumber();

        Assert.Equal(new[] { 4, 2, 1, 3 }, Values(rowNumbers));
    }

    // Verifies that equal ordering values preserve source-row order as the stable tie-breaker.
    [Fact]
    public void RowNumber_WithTies_PreservesSourceRowOrder()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales", "Sales" },
            Salary = new[] { 100, 100, 100 },
            Name = new[] { "First", "Second", "Third" }
        });

        var rowNumbers = df.Window()
            .PartitionBy("Department")
            .OrderBy("Salary")
            .RowNumber();

        Assert.Equal(new[] { 1, 2, 3 }, Values(rowNumbers));
    }

    // Verifies that RowNumber returns an empty aligned result for an empty DataFrame.
    [Fact]
    public void RowNumber_WithEmptyDataFrame_ReturnsEmptySeries()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = Array.Empty<string>(),
            Salary = Array.Empty<int>()
        });

        var rowNumbers = df.Window()
            .PartitionBy("Department")
            .OrderBy("Salary")
            .RowNumber();

        Assert.Empty(Values(rowNumbers));
    }

    // Verifies that null partition keys are treated as a valid partition value.
    [Fact]
    public void RowNumber_WithNullPartitionKey_GroupsNullValuesTogether()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new string?[] { null, "Sales", null, "Sales" },
            Salary = new[] { 200, 50, 100, 100 }
        });

        var rowNumbers = df.Window()
            .PartitionBy("Department")
            .OrderBy("Salary")
            .RowNumber();

        Assert.Equal(new[] { 2, 1, 1, 2 }, Values(rowNumbers));
    }

    // Verifies that null ordering values fail through the existing sorting validation semantics.
    [Fact]
    public void RowNumber_WithNullOrderingValue_Throws()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales" },
            Salary = new int?[] { 100, null }
        });

        var exception = Assert.Throws<ArgumentException>(() => df.Window().OrderBy("Salary"));

        Assert.Contains("Salary", exception.Message);
        Assert.Contains("null", exception.Message);
    }

    // Verifies that invalid partition names fail fast before row-number calculation.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PartitionBy_WithInvalidColumnName_Throws(string? columnName)
    {
        var df = CreateEmployeeDataFrame();

        Assert.ThrowsAny<ArgumentException>(() => df.Window().PartitionBy(columnName!));
    }

    // Verifies that invalid ordering names fail fast before row-number calculation.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OrderBy_WithInvalidColumnName_Throws(string? columnName)
    {
        var df = CreateEmployeeDataFrame();

        Assert.ThrowsAny<ArgumentException>(() => df.Window().OrderBy(columnName!));
    }

    // Verifies that missing partition and ordering columns use existing column lookup failures.
    [Fact]
    public void RowNumber_WithMissingColumns_ThrowsKeyNotFoundException()
    {
        var df = CreateEmployeeDataFrame();

        Assert.Throws<KeyNotFoundException>(() => df.Window().PartitionBy("Missing"));
        Assert.Throws<KeyNotFoundException>(() => df.Window().OrderBy("Missing"));
    }

    // Verifies that duplicate ordering columns are rejected instead of creating ambiguous sort precedence.
    [Fact]
    public void RowNumber_WithDuplicateOrderingColumn_Throws()
    {
        var df = CreateEmployeeDataFrame();

        var exception = Assert.Throws<ArgumentException>(() => df.Window()
            .OrderBy("Salary")
            .ThenByDescending("Salary"));

        Assert.Contains("Salary", exception.Message);
        Assert.Contains("more than once", exception.Message);
    }

    // Verifies that unsupported ordering values fail through the existing sorting validation semantics.
    [Fact]
    public void RowNumber_WithUnsupportedOrderingValues_Throws()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales" },
            Payload = new[] { new SortPayload(2), new SortPayload(1) }
        });

        var exception = Assert.Throws<ArgumentException>(() => df.Window().OrderBy("Payload"));

        Assert.Contains("Payload", exception.Message);
        Assert.Contains("cannot be compared", exception.Message);
    }

    // Verifies that ThenBy is unavailable before a primary OrderBy in the fluent API surface.
    [Fact]
    public void WindowBuilder_DoesNotExposeThenByBeforePrimaryOrdering()
    {
        Assert.Null(typeof(WindowBuilder).GetMethod(nameof(OrderedWindowBuilder.ThenBy), [typeof(string)]));
        Assert.Null(typeof(WindowBuilder).GetMethod(nameof(OrderedWindowBuilder.ThenByDescending), [typeof(string)]));
        Assert.NotNull(typeof(OrderedWindowBuilder).GetMethod(nameof(OrderedWindowBuilder.ThenBy), [typeof(string)]));
        Assert.NotNull(typeof(OrderedWindowBuilder).GetMethod(nameof(OrderedWindowBuilder.ThenByDescending), [typeof(string)]));
    }

    // Verifies that RowNumber does not mutate source rows, source columns, or schema.
    [Fact]
    public void RowNumber_DoesNotMutateSourceDataFrame()
    {
        var df = CreateEmployeeDataFrame();
        var schema = df.Schema;
        var columnNames = ColumnNames(df);
        var departments = Departments(df);
        var salaries = Salaries(df);

        _ = df.Window()
            .PartitionBy("Department")
            .OrderByDescending("Salary")
            .RowNumber();

        Assert.Same(schema, df.Schema);
        Assert.Equal(columnNames, ColumnNames(df));
        Assert.Equal(departments, Departments(df));
        Assert.Equal(salaries, Salaries(df));
    }

    // Verifies that RowNumber output can be appended through the existing Columns.Add API.
    [Fact]
    public void RowNumber_ResultCanBeAddedAsColumn()
    {
        var df = CreateEmployeeDataFrame();
        var rowNumbers = df.Window()
            .PartitionBy("Department")
            .OrderByDescending("Salary")
            .RowNumber();

        df.Columns.Add("RowNumber", rowNumbers);

        Assert.Equal(new[] { "Department", "Name", "Salary", "CreatedAt", "RowNumber" }, ColumnNames(df));
        Assert.Equal(new[] { 1, 3, 2, 1, 1 }, IntColumn(df, "RowNumber"));
    }

    private static global::Runiq.Data.DataFrame CreateEmployeeDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales", "Sales", "Ops", "Finance" },
            Name = new[] { "Ali", "Ayse", "Mehmet", "Zeynep", "Can" },
            Salary = new[] { 120000, 90000, 110000, 95000, 150000 },
            CreatedAt = new[]
            {
                new DateTime(2024, 1, 3),
                new DateTime(2024, 1, 1),
                new DateTime(2024, 1, 2),
                new DateTime(2024, 1, 4),
                new DateTime(2024, 1, 5)
            }
        });
    }

    private static int[] Values(Series<int> series)
    {
        return series.Values.ToArray();
    }

    private static string[] ColumnNames(global::Runiq.Data.DataFrame df)
    {
        return df.Columns.Select(static column => column.Name).ToArray();
    }

    private static string[] Departments(global::Runiq.Data.DataFrame df)
    {
        return Enumerable.Range(0, df.Rows.Count())
            .Select(index => (string)df["Department"].GetValue(index)!)
            .ToArray();
    }

    private static int[] Salaries(global::Runiq.Data.DataFrame df)
    {
        return IntColumn(df, "Salary");
    }

    private static int[] IntColumn(global::Runiq.Data.DataFrame df, string columnName)
    {
        return Enumerable.Range(0, df.Rows.Count())
            .Select(index => (int)df[columnName].GetValue(index)!)
            .ToArray();
    }

    /// <summary>
    /// Represents a deliberately non-comparable value used to verify fail-fast ordering behavior.
    /// </summary>
    private sealed record SortPayload(int Value);
}
