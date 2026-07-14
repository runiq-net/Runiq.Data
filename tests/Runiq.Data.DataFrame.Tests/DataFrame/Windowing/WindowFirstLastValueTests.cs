using Runiq.Data.Series;

namespace Runiq.Data.DataFrameTests.Windowing;

/// <summary>
/// Verifies window FirstValue and LastValue partitioning, ordering, alignment, type, validation, and mutation contracts.
/// </summary>
public sealed class WindowFirstLastValueTests
{
    // Verifies that global FirstValue returns the first value in ordered row sequence.
    [Fact]
    public void FirstValue_WithGlobalOrdering_ReturnsFirstOrderedValue()
    {
        var df = CreateEmployeeDataFrame();

        var firstSalary = df.Window()
            .OrderBy("CreatedAt")
            .FirstValue("Salary");

        Assert.Equal(new[] { 100, 100, 100, 100, 100 }, IntValues(firstSalary));
    }

    // Verifies that global LastValue returns the last value from the full ordered row sequence.
    [Fact]
    public void LastValue_WithGlobalOrdering_ReturnsLastOrderedValue()
    {
        var df = CreateEmployeeDataFrame();

        var lastSalary = df.Window()
            .OrderBy("CreatedAt")
            .LastValue("Salary");

        Assert.Equal(new[] { 150, 150, 150, 150, 150 }, IntValues(lastSalary));
    }

    // Verifies that partitioned FirstValue and LastValue are calculated independently per partition.
    [Fact]
    public void FirstValueAndLastValue_WithPartition_ReturnIndependentPartitionValues()
    {
        var df = CreateEmployeeDataFrame();

        var firstSalary = df.Window()
            .PartitionBy("Department")
            .OrderBy("CreatedAt")
            .FirstValue("Salary");
        var lastSalary = df.Window()
            .PartitionBy("Department")
            .OrderBy("CreatedAt")
            .LastValue("Salary");

        Assert.Equal(new[] { 100, 100, 100, 95, 150 }, IntValues(firstSalary));
        Assert.Equal(new[] { 120, 120, 120, 95, 150 }, IntValues(lastSalary));
    }

    // Verifies that descending ordering reverses the selected first and last boundary rows.
    [Fact]
    public void FirstValueAndLastValue_WithDescendingOrdering_UseDescendingBoundaries()
    {
        var df = CreateEmployeeDataFrame();

        var firstSalary = df.Window()
            .PartitionBy("Department")
            .OrderByDescending("CreatedAt")
            .FirstValue("Salary");
        var lastSalary = df.Window()
            .PartitionBy("Department")
            .OrderByDescending("CreatedAt")
            .LastValue("Salary");

        Assert.Equal(new[] { 120, 120, 120, 95, 150 }, IntValues(firstSalary));
        Assert.Equal(new[] { 100, 100, 100, 95, 150 }, IntValues(lastSalary));
    }

    // Verifies that composite ordering uses all configured ordering columns for boundary selection.
    [Fact]
    public void FirstValueAndLastValue_WithCompositeOrdering_SelectRowsByAllOrderingColumns()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales", "Sales", "Sales" },
            Year = new[] { 2024, 2024, 2023, 2024 },
            Month = new[] { 2, 1, 12, 3 },
            Name = new[] { "Second2024", "First2024", "Last2023", "Third2024" }
        });

        var firstName = df.Window()
            .PartitionBy("Department")
            .OrderBy("Year")
            .ThenBy("Month")
            .FirstValue("Name");
        var lastName = df.Window()
            .PartitionBy("Department")
            .OrderBy("Year")
            .ThenBy("Month")
            .LastValue("Name");

        Assert.Equal(new[] { "Last2023", "Last2023", "Last2023", "Last2023" }, StringValues(firstName));
        Assert.Equal(new[] { "Third2024", "Third2024", "Third2024", "Third2024" }, StringValues(lastName));
    }

    // Verifies that fully equal ordering keys preserve source-row order for first and last boundaries.
    [Fact]
    public void FirstValueAndLastValue_WithEqualOrderingKeys_PreserveSourceRowStability()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            SortKey = new[] { 1, 1, 1 },
            Name = new[] { "First", "Second", "Third" }
        });

        var firstName = df.Window().OrderBy("SortKey").FirstValue("Name");
        var lastName = df.Window().OrderBy("SortKey").LastValue("Name");

        Assert.Equal(new[] { "First", "First", "First" }, StringValues(firstName));
        Assert.Equal(new[] { "Third", "Third", "Third" }, StringValues(lastName));
    }

    // Verifies that boundary results remain aligned to the original source row positions.
    [Fact]
    public void FirstValueAndLastValue_ReturnValuesAlignedToSourceRows()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Third", "First", "Second" },
            CreatedAt = new[] { 3, 1, 2 },
            Salary = new[] { 80, 100, 90 }
        });

        var firstSalary = df.Window().OrderBy("CreatedAt").FirstValue("Salary");
        var lastSalary = df.Window().OrderBy("CreatedAt").LastValue("Salary");

        Assert.Equal(new[] { 100, 100, 100 }, IntValues(firstSalary));
        Assert.Equal(new[] { 80, 80, 80 }, IntValues(lastSalary));
    }

    // Verifies that FirstValue and LastValue do not mutate source rows, source columns, or schema.
    [Fact]
    public void FirstValueAndLastValue_DoNotMutateSourceDataFrame()
    {
        var df = CreateEmployeeDataFrame();
        var schema = df.Schema;
        var columnNames = ColumnNames(df);
        var salaries = IntColumn(df, "Salary");

        _ = df.Window().PartitionBy("Department").OrderBy("CreatedAt").FirstValue("Salary");
        _ = df.Window().PartitionBy("Department").OrderBy("CreatedAt").LastValue("Salary");

        Assert.Same(schema, df.Schema);
        Assert.Equal(columnNames, ColumnNames(df));
        Assert.Equal(salaries, IntColumn(df, "Salary"));
    }

    // Verifies that a null first target value is propagated without null-skipping behavior.
    [Fact]
    public void FirstValue_WithNullFirstTargetValue_PreservesNull()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = new[] { 1, 2, 3 },
            Salary = new int?[] { null, 100, 90 }
        });

        var firstSalary = df.Window().OrderBy("CreatedAt").FirstValue("Salary");

        Assert.Equal(typeof(int?), firstSalary.DataType);
        Assert.Equal(new int?[] { null, null, null }, NullableIntValues(firstSalary));
    }

    // Verifies that a null last target value is propagated without null-skipping behavior.
    [Fact]
    public void LastValue_WithNullLastTargetValue_PreservesNull()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = new[] { 1, 2, 3 },
            Salary = new int?[] { 100, 90, null }
        });

        var lastSalary = df.Window().OrderBy("CreatedAt").LastValue("Salary");

        Assert.Equal(typeof(int?), lastSalary.DataType);
        Assert.Equal(new int?[] { null, null, null }, NullableIntValues(lastSalary));
    }

    // Verifies that nullable and reference type source columns preserve their result types.
    [Fact]
    public void FirstValueAndLastValue_WithNullableAndReferenceTargets_PreserveSourceTypes()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = new[] { 1, 2 },
            Score = new int?[] { 10, null },
            Name = new[] { "First", "Second" }
        });

        var firstScore = df.Window().OrderBy("CreatedAt").FirstValue("Score");
        var lastName = df.Window().OrderBy("CreatedAt").LastValue("Name");

        Assert.Equal(typeof(int?), firstScore.DataType);
        Assert.Equal(typeof(string), lastName.DataType);
        Assert.True(firstScore.IsNullable);
        Assert.True(lastName.IsNullable);
        Assert.Equal(new int?[] { 10, 10 }, NullableIntValues(firstScore));
        Assert.Equal(new[] { "Second", "Second" }, StringValues(lastName));
    }

    // Verifies that common supported target column types are copied from ordered boundary rows.
    [Fact]
    public void FirstValueAndLastValue_WithSupportedTargetTypes_CopyBoundaryValues()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = new[] { 1, 2 },
            Amount = new[] { 10.5m, 20.5m },
            OccurredAt = new[] { new DateTime(2024, 1, 1), new DateTime(2024, 1, 2) },
            EventId = new[] { firstId, secondId }
        });

        var firstAmount = df.Window().OrderBy("CreatedAt").FirstValue("Amount");
        var lastOccurredAt = df.Window().OrderBy("CreatedAt").LastValue("OccurredAt");
        var lastEventId = df.Window().OrderBy("CreatedAt").LastValue("EventId");

        Assert.Equal(new[] { 10.5m, 10.5m }, DecimalValues(firstAmount));
        Assert.Equal(new[] { new DateTime(2024, 1, 2), new DateTime(2024, 1, 2) }, DateTimeValues(lastOccurredAt));
        Assert.Equal(new[] { secondId, secondId }, GuidValues(lastEventId));
    }

    // Verifies that empty DataFrames produce empty result series with the exact source column type.
    [Fact]
    public void FirstValueAndLastValue_WithEmptyDataFrame_ReturnEmptyTypedSeries()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = Array.Empty<int>(),
            Salary = Array.Empty<int>()
        });

        var firstSalary = df.Window().OrderBy("CreatedAt").FirstValue("Salary");
        var lastSalary = df.Window().OrderBy("CreatedAt").LastValue("Salary");

        Assert.Equal(typeof(int), firstSalary.DataType);
        Assert.Equal(typeof(int), lastSalary.DataType);
        Assert.Equal(0, firstSalary.Count);
        Assert.Equal(0, lastSalary.Count);
    }

    // Verifies that a single-row partition returns the same value for both boundary functions.
    [Fact]
    public void FirstValueAndLastValue_WithSingleRowPartition_ReturnSameValue()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Ops" },
            CreatedAt = new[] { 1, 1 },
            Salary = new[] { 100, 90 }
        });

        var firstSalary = df.Window().PartitionBy("Department").OrderBy("CreatedAt").FirstValue("Salary");
        var lastSalary = df.Window().PartitionBy("Department").OrderBy("CreatedAt").LastValue("Salary");

        Assert.Equal(new[] { 100, 90 }, IntValues(firstSalary));
        Assert.Equal(new[] { 100, 90 }, IntValues(lastSalary));
    }

    // Verifies that invalid target column names fail before boundary value calculation.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FirstValueAndLastValue_WithInvalidTargetColumnName_Throw(string? columnName)
    {
        var df = CreateEmployeeDataFrame();

        Assert.ThrowsAny<ArgumentException>(() => df.Window().OrderBy("CreatedAt").FirstValue(columnName!));
        Assert.ThrowsAny<ArgumentException>(() => df.Window().OrderBy("CreatedAt").LastValue(columnName!));
    }

    // Verifies that missing target columns use the existing column lookup failure.
    [Fact]
    public void FirstValueAndLastValue_WithMissingTargetColumn_ThrowKeyNotFoundException()
    {
        var df = CreateEmployeeDataFrame();

        Assert.Throws<KeyNotFoundException>(() => df.Window().OrderBy("CreatedAt").FirstValue("Missing"));
        Assert.Throws<KeyNotFoundException>(() => df.Window().OrderBy("CreatedAt").LastValue("Missing"));
    }

    // Verifies that FirstValue and LastValue outputs can be appended through the existing Columns.Add API.
    [Fact]
    public void FirstValueAndLastValueResults_CanBeAddedAsColumns()
    {
        var df = CreateEmployeeDataFrame();
        var firstSalary = df.Window().PartitionBy("Department").OrderBy("CreatedAt").FirstValue("Salary");
        var lastSalary = df.Window().PartitionBy("Department").OrderBy("CreatedAt").LastValue("Salary");

        df.Columns.Add("FirstSalary", firstSalary);
        df.Columns.Add("LastSalary", lastSalary);

        Assert.Equal(new[] { 100, 100, 100, 95, 150 }, IntValues(df["FirstSalary"]));
        Assert.Equal(new[] { 120, 120, 120, 95, 150 }, IntValues(df["LastSalary"]));
    }

    /// <summary>
    /// Creates a deterministic employee table used to verify ordered boundary values and source-row alignment.
    /// </summary>
    private static global::Runiq.Data.DataFrame CreateEmployeeDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales", "Sales", "Ops", "Finance" },
            Name = new[] { "Ali", "Ayse", "Mehmet", "Zeynep", "Can" },
            Salary = new[] { 100, 90, 120, 95, 150 },
            CreatedAt = new[]
            {
                new DateTime(2024, 1, 1),
                new DateTime(2024, 1, 2),
                new DateTime(2024, 1, 3),
                new DateTime(2024, 1, 4),
                new DateTime(2024, 1, 5)
            }
        });
    }

    /// <summary>
    /// Reads integer values from an untyped result series for boundary assertions.
    /// </summary>
    private static int[] IntValues(ISeries series)
    {
        return Enumerable.Range(0, series.Count)
            .Select(index => (int)series.GetValue(index)!)
            .ToArray();
    }

    /// <summary>
    /// Reads nullable integer values from an untyped result series for null propagation assertions.
    /// </summary>
    private static int?[] NullableIntValues(ISeries series)
    {
        return Enumerable.Range(0, series.Count)
            .Select(index => (int?)series.GetValue(index))
            .ToArray();
    }

    /// <summary>
    /// Reads decimal values from an untyped result series for type preservation assertions.
    /// </summary>
    private static decimal[] DecimalValues(ISeries series)
    {
        return Enumerable.Range(0, series.Count)
            .Select(index => (decimal)series.GetValue(index)!)
            .ToArray();
    }

    /// <summary>
    /// Reads DateTime values from an untyped result series for type preservation assertions.
    /// </summary>
    private static DateTime[] DateTimeValues(ISeries series)
    {
        return Enumerable.Range(0, series.Count)
            .Select(index => (DateTime)series.GetValue(index)!)
            .ToArray();
    }

    /// <summary>
    /// Reads Guid values from an untyped result series for type preservation assertions.
    /// </summary>
    private static Guid[] GuidValues(ISeries series)
    {
        return Enumerable.Range(0, series.Count)
            .Select(index => (Guid)series.GetValue(index)!)
            .ToArray();
    }

    /// <summary>
    /// Reads string values from an untyped result series while preserving reference null values.
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
}
