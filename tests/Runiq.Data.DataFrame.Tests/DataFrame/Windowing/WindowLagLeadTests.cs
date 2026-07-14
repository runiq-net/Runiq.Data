using Runiq.Data.Series;

namespace Runiq.Data.DataFrameTests.Windowing;

/// <summary>
/// Verifies window Lag and Lead partitioning, ordering, alignment, type, validation, and mutation contracts.
/// </summary>
public sealed class WindowLagLeadTests
{
    // Verifies that global Lag returns the previous value in ordered row sequence.
    [Fact]
    public void Lag_WithGlobalOrdering_ReturnsPreviousValue()
    {
        var df = CreateEmployeeDataFrame();

        var previousSalary = df.Window()
            .OrderBy("CreatedAt")
            .Lag("Salary");

        Assert.Equal(new int?[] { null, 100, 90, 120, 95 }, NullableIntValues(previousSalary));
    }

    // Verifies that global Lead returns the next value in ordered row sequence.
    [Fact]
    public void Lead_WithGlobalOrdering_ReturnsNextValue()
    {
        var df = CreateEmployeeDataFrame();

        var nextSalary = df.Window()
            .OrderBy("CreatedAt")
            .Lead("Salary");

        Assert.Equal(new int?[] { 90, 120, 95, 150, null }, NullableIntValues(nextSalary));
    }

    // Verifies that partitioned Lag and Lead do not read values across partition boundaries.
    [Fact]
    public void LagAndLead_WithPartition_DoNotCrossPartitionBoundaries()
    {
        var df = CreateEmployeeDataFrame();

        var previousSalary = df.Window()
            .PartitionBy("Department")
            .OrderBy("CreatedAt")
            .Lag("Salary");
        var nextSalary = df.Window()
            .PartitionBy("Department")
            .OrderBy("CreatedAt")
            .Lead("Salary");

        Assert.Equal(new int?[] { null, 100, 90, null, null }, NullableIntValues(previousSalary));
        Assert.Equal(new int?[] { 90, 120, null, null, null }, NullableIntValues(nextSalary));
    }

    // Verifies that each partition boundary returns null for missing Lag and Lead rows.
    [Fact]
    public void LagAndLead_WithPartitionBoundary_ReturnNull()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales", "Ops", "Ops" },
            CreatedAt = new[] { 1, 2, 1, 2 },
            Salary = new[] { 100, 90, 80, 70 }
        });

        var previousSalary = df.Window()
            .PartitionBy("Department")
            .OrderBy("CreatedAt")
            .Lag("Salary");
        var nextSalary = df.Window()
            .PartitionBy("Department")
            .OrderBy("CreatedAt")
            .Lead("Salary");

        Assert.Equal(new int?[] { null, 100, null, 80 }, NullableIntValues(previousSalary));
        Assert.Equal(new int?[] { 90, null, 70, null }, NullableIntValues(nextSalary));
    }

    // Verifies that offset two and larger offsets select values from the requested ordered distance.
    [Fact]
    public void LagAndLead_WithOffsetTwo_SelectRequestedDistance()
    {
        var df = CreateEmployeeDataFrame();

        var twoRowsBefore = df.Window()
            .OrderBy("CreatedAt")
            .Lag("Salary", offset: 2);
        var twoRowsAfter = df.Window()
            .OrderBy("CreatedAt")
            .Lead("Salary", offset: 2);

        Assert.Equal(new int?[] { null, null, 100, 90, 120 }, NullableIntValues(twoRowsBefore));
        Assert.Equal(new int?[] { 120, 95, 150, null, null }, NullableIntValues(twoRowsAfter));
    }

    // Verifies that offsets larger than a partition size produce null values for that partition.
    [Fact]
    public void Lag_WithOffsetLargerThanPartition_ReturnsNulls()
    {
        var df = CreateEmployeeDataFrame();

        var previousSalary = df.Window()
            .PartitionBy("Department")
            .OrderBy("CreatedAt")
            .Lag("Salary", offset: 4);

        Assert.Equal(new int?[] { null, null, null, null, null }, NullableIntValues(previousSalary));
    }

    // Verifies that composite ordering selects Lag and Lead source rows by all ordering columns.
    [Fact]
    public void LagAndLead_WithCompositeOrdering_SelectRowsByAllOrderingColumns()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales", "Sales", "Sales" },
            Salary = new[] { 100, 100, 200, 100 },
            Name = new[] { "Zeynep", "Ali", "Mehmet", "Ayse" }
        });

        var previousName = df.Window()
            .PartitionBy("Department")
            .OrderByDescending("Salary")
            .ThenBy("Name")
            .Lag("Name");
        var nextName = df.Window()
            .PartitionBy("Department")
            .OrderByDescending("Salary")
            .ThenBy("Name")
            .Lead("Name");

        Assert.Equal(new string?[] { "Ayse", "Mehmet", null, "Ali" }, StringValues(previousName));
        Assert.Equal(new string?[] { null, "Ayse", "Ali", "Zeynep" }, StringValues(nextName));
    }

    // Verifies that ascending and descending ordering clauses can be combined for Lag and Lead.
    [Fact]
    public void LagAndLead_WithAscendingAndDescendingOrderingCombination_OrderByEachDirection()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Grade = new[] { 2, 1, 1, 2 },
            Salary = new[] { 100, 100, 200, 300 },
            Name = new[] { "Fourth", "Second", "First", "Third" }
        });

        var previousName = df.Window()
            .OrderBy("Grade")
            .ThenByDescending("Salary")
            .Lag("Name");
        var nextName = df.Window()
            .OrderBy("Grade")
            .ThenByDescending("Salary")
            .Lead("Name");

        Assert.Equal(new string?[] { "Third", "First", null, "Second" }, StringValues(previousName));
        Assert.Equal(new string?[] { null, "Third", "Second", "Fourth" }, StringValues(nextName));
    }

    // Verifies that fully equal ordering keys preserve the existing stable source-row tie-breaker.
    [Fact]
    public void LagAndLead_WithEqualOrderingKeys_PreserveSourceRowStability()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            SortKey = new[] { 1, 1, 1 },
            Name = new[] { "First", "Second", "Third" }
        });

        var previousName = df.Window()
            .OrderBy("SortKey")
            .Lag("Name");
        var nextName = df.Window()
            .OrderBy("SortKey")
            .Lead("Name");

        Assert.Equal(new string?[] { null, "First", "Second" }, StringValues(previousName));
        Assert.Equal(new string?[] { "Second", "Third", null }, StringValues(nextName));
    }

    // Verifies that Lag results remain aligned to the original source row positions.
    [Fact]
    public void Lag_ReturnsValuesAlignedToSourceRows()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Third", "First", "Second" },
            CreatedAt = new[] { 3, 1, 2 },
            Salary = new[] { 80, 100, 90 }
        });

        var previousSalary = df.Window()
            .OrderBy("CreatedAt")
            .Lag("Salary");

        Assert.Equal(new int?[] { 90, null, 100 }, NullableIntValues(previousSalary));
    }

    // Verifies that Lag and Lead do not mutate source rows, source columns, or schema.
    [Fact]
    public void LagAndLead_DoNotMutateSourceDataFrame()
    {
        var df = CreateEmployeeDataFrame();
        var schema = df.Schema;
        var columnNames = ColumnNames(df);
        var salaries = IntColumn(df, "Salary");

        _ = df.Window()
            .PartitionBy("Department")
            .OrderBy("CreatedAt")
            .Lag("Salary");
        _ = df.Window()
            .PartitionBy("Department")
            .OrderBy("CreatedAt")
            .Lead("Salary");

        Assert.Same(schema, df.Schema);
        Assert.Equal(columnNames, ColumnNames(df));
        Assert.Equal(salaries, IntColumn(df, "Salary"));
    }

    // Verifies that null target values are shifted without replacement or conversion.
    [Fact]
    public void Lag_WithNullTargetValues_PreservesNulls()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = new[] { 1, 2, 3 },
            Salary = new int?[] { 100, null, 90 }
        });

        var previousSalary = df.Window()
            .OrderBy("CreatedAt")
            .Lag("Salary");

        Assert.Equal(typeof(int?), previousSalary.DataType);
        Assert.Equal(new int?[] { null, 100, null }, NullableIntValues(previousSalary));
    }

    // Verifies that non-nullable value-type source columns produce nullable result series.
    [Fact]
    public void Lag_WithValueTypeTarget_ReturnsNullableValueTypeSeries()
    {
        var df = CreateEmployeeDataFrame();

        var previousSalary = df.Window()
            .OrderBy("CreatedAt")
            .Lag("Salary");

        Assert.Equal(typeof(int?), previousSalary.DataType);
        Assert.True(previousSalary.IsNullable);
    }

    // Verifies that reference-type source columns keep their existing result type.
    [Fact]
    public void Lag_WithReferenceTypeTarget_PreservesReferenceTypeSeries()
    {
        var df = CreateEmployeeDataFrame();

        var previousName = df.Window()
            .OrderBy("CreatedAt")
            .Lag("Name");

        Assert.Equal(typeof(string), previousName.DataType);
        Assert.True(previousName.IsNullable);
    }

    // Verifies that common supported target column types can be shifted through Lag.
    [Fact]
    public void Lag_WithSupportedTargetTypes_ShiftsValues()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = new[] { 1, 2 },
            Name = new[] { "First", "Second" },
            Amount = new[] { 10.5m, 20.5m },
            OccurredAt = new[] { new DateTime(2024, 1, 1), new DateTime(2024, 1, 2) },
            EventId = new[] { firstId, secondId }
        });

        var previousName = df.Window().OrderBy("CreatedAt").Lag("Name");
        var previousAmount = df.Window().OrderBy("CreatedAt").Lag("Amount");
        var previousOccurredAt = df.Window().OrderBy("CreatedAt").Lag("OccurredAt");
        var previousEventId = df.Window().OrderBy("CreatedAt").Lag("EventId");

        Assert.Equal(new string?[] { null, "First" }, StringValues(previousName));
        Assert.Equal(new decimal?[] { null, 10.5m }, NullableDecimalValues(previousAmount));
        Assert.Equal(new DateTime?[] { null, new DateTime(2024, 1, 1) }, NullableDateTimeValues(previousOccurredAt));
        Assert.Equal(new Guid?[] { null, firstId }, NullableGuidValues(previousEventId));
    }

    // Verifies that mutable cell values follow the existing DataFrame cell ownership semantics.
    [Fact]
    public void Lag_WithMutableCellValues_ShiftsExistingCellValues()
    {
        var firstPayload = new byte[] { 1 };
        var secondPayload = new byte[] { 2 };
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = new[] { 1, 2 },
            Payload = new[] { firstPayload, secondPayload }
        });

        var previousPayload = df.Window()
            .OrderBy("CreatedAt")
            .Lag("Payload");

        Assert.Null(previousPayload.GetValue(0));
        Assert.Same(firstPayload, previousPayload.GetValue(1));
    }

    // Verifies that empty DataFrames produce an empty result series with the correct nullable type.
    [Fact]
    public void Lag_WithEmptyDataFrame_ReturnsEmptyTypedSeries()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            CreatedAt = Array.Empty<int>(),
            Salary = Array.Empty<int>()
        });

        var previousSalary = df.Window()
            .OrderBy("CreatedAt")
            .Lag("Salary");

        Assert.Equal(typeof(int?), previousSalary.DataType);
        Assert.Equal(0, previousSalary.Count);
    }

    // Verifies that a single-row partition returns null for both Lag and Lead.
    [Fact]
    public void LagAndLead_WithSingleRowPartition_ReturnNull()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Ops" },
            CreatedAt = new[] { 1, 1 },
            Salary = new[] { 100, 90 }
        });

        var previousSalary = df.Window()
            .PartitionBy("Department")
            .OrderBy("CreatedAt")
            .Lag("Salary");
        var nextSalary = df.Window()
            .PartitionBy("Department")
            .OrderBy("CreatedAt")
            .Lead("Salary");

        Assert.Equal(new int?[] { null, null }, NullableIntValues(previousSalary));
        Assert.Equal(new int?[] { null, null }, NullableIntValues(nextSalary));
    }

    // Verifies that zero and negative offsets fail before shift calculation.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void LagAndLead_WithInvalidOffset_Throw(int offset)
    {
        var df = CreateEmployeeDataFrame();

        Assert.Throws<ArgumentException>(() => df.Window().OrderBy("CreatedAt").Lag("Salary", offset));
        Assert.Throws<ArgumentException>(() => df.Window().OrderBy("CreatedAt").Lead("Salary", offset));
    }

    // Verifies that invalid target column names fail before shift calculation.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LagAndLead_WithInvalidTargetColumnName_Throw(string? columnName)
    {
        var df = CreateEmployeeDataFrame();

        Assert.ThrowsAny<ArgumentException>(() => df.Window().OrderBy("CreatedAt").Lag(columnName!));
        Assert.ThrowsAny<ArgumentException>(() => df.Window().OrderBy("CreatedAt").Lead(columnName!));
    }

    // Verifies that missing target columns use the existing column lookup failure.
    [Fact]
    public void LagAndLead_WithMissingTargetColumn_ThrowKeyNotFoundException()
    {
        var df = CreateEmployeeDataFrame();

        Assert.Throws<KeyNotFoundException>(() => df.Window().OrderBy("CreatedAt").Lag("Missing"));
        Assert.Throws<KeyNotFoundException>(() => df.Window().OrderBy("CreatedAt").Lead("Missing"));
    }

    // Verifies that Lag and Lead outputs can be appended through the existing Columns.Add API.
    [Fact]
    public void LagAndLeadResults_CanBeAddedAsColumns()
    {
        var df = CreateEmployeeDataFrame();
        var previousSalary = df.Window()
            .PartitionBy("Department")
            .OrderBy("CreatedAt")
            .Lag("Salary");
        var nextSalary = df.Window()
            .PartitionBy("Department")
            .OrderBy("CreatedAt")
            .Lead("Salary");

        df.Columns.Add("PreviousSalary", previousSalary);
        df.Columns.Add("NextSalary", nextSalary);

        Assert.Equal(new int?[] { null, 100, 90, null, null }, NullableIntValues(df["PreviousSalary"]));
        Assert.Equal(new int?[] { 90, 120, null, null, null }, NullableIntValues(df["NextSalary"]));
    }

    /// <summary>
    /// Creates a deterministic employee table used to verify ordered shifts and source-row alignment.
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
    /// Reads nullable integer values from an untyped result series for contract assertions.
    /// </summary>
    private static int?[] NullableIntValues(ISeries series)
    {
        return Enumerable.Range(0, series.Count)
            .Select(index => (int?)series.GetValue(index))
            .ToArray();
    }

    /// <summary>
    /// Reads nullable decimal values from an untyped result series for type preservation assertions.
    /// </summary>
    private static decimal?[] NullableDecimalValues(ISeries series)
    {
        return Enumerable.Range(0, series.Count)
            .Select(index => (decimal?)series.GetValue(index))
            .ToArray();
    }

    /// <summary>
    /// Reads nullable DateTime values from an untyped result series for type preservation assertions.
    /// </summary>
    private static DateTime?[] NullableDateTimeValues(ISeries series)
    {
        return Enumerable.Range(0, series.Count)
            .Select(index => (DateTime?)series.GetValue(index))
            .ToArray();
    }

    /// <summary>
    /// Reads nullable Guid values from an untyped result series for type preservation assertions.
    /// </summary>
    private static Guid?[] NullableGuidValues(ISeries series)
    {
        return Enumerable.Range(0, series.Count)
            .Select(index => (Guid?)series.GetValue(index))
            .ToArray();
    }

    /// <summary>
    /// Reads string values from an untyped result series while preserving null boundary values.
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
