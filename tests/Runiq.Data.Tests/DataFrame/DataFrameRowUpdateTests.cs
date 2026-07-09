namespace Runiq.Data.Tests.DataFrame;

/// <summary>
/// Verifies direct mutable row replacement behavior on DataFrame.
/// </summary>
public sealed class DataFrameRowUpdateTests
{
    /// <summary>
    /// Verifies that RowsUpdate replaces the requested row and leaves surrounding rows intact.
    /// </summary>
    [Fact]
    public void RowsUpdate_ReplacesRequestedRowAndPreservesOtherRows()
    {
        // Verifies full-row replacement at the requested zero-based index.
        var df = CreatePeopleDataFrame();

        df.Rows.Update(1, new { Name = "Zeynep", Age = 29, Salary = 110000m, IsActive = true });

        Assert.Equal("Ali", df.GetRow(0)["Name"]);
        Assert.Equal("Zeynep", df.GetRow(1)["Name"]);
        Assert.Equal(29, df.GetRow(1)["Age"]);
        Assert.Equal(110000m, df.GetRow(1)["Salary"]);
        Assert.Equal(true, df.GetRow(1)["IsActive"]);
        Assert.Equal("Mehmet", df.GetRow(2)["Name"]);
    }

    /// <summary>
    /// Verifies that RowsUpdate mutates the current DataFrame instance.
    /// </summary>
    [Fact]
    public void RowsUpdate_MutatesCurrentDataFrameInstance()
    {
        // Verifies that RowsUpdate changes the existing object instead of returning a branch.
        var df = CreatePeopleDataFrame();
        var sameInstance = df;

        df.Rows.Update(1, new { Name = "Zeynep", Age = 29, Salary = 110000m, IsActive = true });

        Assert.Same(df, sameInstance);
        Assert.Equal("Zeynep", sameInstance.GetRow(1)["Name"]);
    }

    /// <summary>
    /// Verifies that RowsUpdate preserves row count, column count, order, schema, and column types.
    /// </summary>
    [Fact]
    public void RowsUpdate_PreservesShapeSchemaOrderAndTypes()
    {
        // Verifies that mutable row replacement does not reshape the DataFrame.
        var df = CreatePeopleDataFrame();
        var schema = df.Schema;

        df.Rows.Update(1, new { Name = "Zeynep", Age = 29, Salary = 110000m, IsActive = true });

        Assert.Equal(3, df.RowCount);
        Assert.Equal(4, df.ColumnCount);
        Assert.Equal(new[] { "Name", "Age", "Salary", "IsActive" }, ColumnNames(df));
        Assert.Equal(new[] { "Name", "Age", "Salary", "IsActive" }, SchemaNames(df));
        Assert.Same(schema, df.Schema);
        Assert.Equal(typeof(string), df.Schema.GetColumn("Name").DataType);
        Assert.Equal(typeof(int), df.Schema.GetColumn("Age").DataType);
        Assert.Equal(typeof(decimal), df.Schema.GetColumn("Salary").DataType);
        Assert.Equal(typeof(bool), df.Schema.GetColumn("IsActive").DataType);
        Assert.True(df.Schema.GetColumn("Name").IsNullable);
        Assert.False(df.Schema.GetColumn("Age").IsNullable);
    }

    /// <summary>
    /// Verifies that the updated row is readable through GetRow.
    /// </summary>
    [Fact]
    public void RowsUpdate_UpdatedRowCanBeReadThroughGetRow()
    {
        // Verifies that row access observes the updated values.
        var df = CreatePeopleDataFrame();

        df.Rows.Update(1, new { Name = "Zeynep", Age = 29, Salary = 110000m, IsActive = true });

        var row = df.GetRow(1);
        Assert.Equal("Zeynep", row["Name"]);
        Assert.Equal(29, row["Age"]);
    }

    /// <summary>
    /// Verifies that updating the first row works.
    /// </summary>
    [Fact]
    public void RowsUpdate_WithFirstIndex_ReplacesFirstRow()
    {
        // Verifies that index zero is a valid update target.
        var df = CreatePeopleDataFrame();

        df.Rows.Update(0, new { Name = "Zeynep", Age = 29, Salary = 110000m, IsActive = true });

        Assert.Equal("Zeynep", df.GetRow(0)["Name"]);
        Assert.Equal("Ayse", df.GetRow(1)["Name"]);
    }

    /// <summary>
    /// Verifies that updating the last row works.
    /// </summary>
    [Fact]
    public void RowsUpdate_WithLastIndex_ReplacesLastRow()
    {
        // Verifies that the final valid row index is a valid update target.
        var df = CreatePeopleDataFrame();

        df.Rows.Update(2, new { Name = "Zeynep", Age = 29, Salary = 110000m, IsActive = true });

        Assert.Equal("Ayse", df.GetRow(1)["Name"]);
        Assert.Equal("Zeynep", df.GetRow(2)["Name"]);
    }

    /// <summary>
    /// Verifies that negative update indexes are rejected.
    /// </summary>
    [Fact]
    public void RowsUpdate_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        // Verifies that row replacement fails fast for indexes below zero.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => df.Rows.Update(-1, ValidRow()));

        Assert.Equal("index", exception.ParamName);
    }

    /// <summary>
    /// Verifies that row count is not a valid update index.
    /// </summary>
    [Fact]
    public void RowsUpdate_WhenIndexEqualsRowCount_ThrowsArgumentOutOfRangeException()
    {
        // Verifies that row replacement fails fast when the index is outside the row range.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => df.Rows.Update(df.RowCount, ValidRow()));

        Assert.Equal("index", exception.ParamName);
    }

    /// <summary>
    /// Verifies that indexes greater than row count are rejected.
    /// </summary>
    [Fact]
    public void RowsUpdate_WhenIndexIsGreaterThanRowCount_ThrowsAndLeavesDataFrameUnchanged()
    {
        // Verifies that an out-of-range replacement does not change existing rows or shape.
        var df = CreatePeopleDataFrame();
        var originalRowCount = df.RowCount;
        var originalColumnCount = df.ColumnCount;
        var originalNames = RowNames(df);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => df.Rows.Update(df.RowCount + 1, ValidRow()));

        Assert.Equal("index", exception.ParamName);
        Assert.Equal(originalRowCount, df.RowCount);
        Assert.Equal(originalColumnCount, df.ColumnCount);
        Assert.Equal(originalNames, RowNames(df));
    }

    /// <summary>
    /// Verifies that null row arguments are rejected.
    /// </summary>
    [Fact]
    public void RowsUpdate_WithNullRow_ThrowsArgumentNullException()
    {
        // Verifies that row replacement fails fast when no row object is supplied.
        var df = CreatePeopleDataFrame();

        Assert.Throws<ArgumentNullException>(() => df.Rows.Update(1, null!));
    }

    /// <summary>
    /// Verifies that missing row properties are rejected.
    /// </summary>
    [Fact]
    public void RowsUpdate_WithMissingProperty_ThrowsArgumentException()
    {
        // Verifies that a replacement row must supply every existing DataFrame column.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentException>(() => df.Rows.Update(1, new { Name = "Zeynep", Age = 29 }));

        Assert.Contains("Salary", exception.Message);
    }

    /// <summary>
    /// Verifies that extra row properties are rejected.
    /// </summary>
    [Fact]
    public void RowsUpdate_WithExtraProperty_ThrowsArgumentException()
    {
        // Verifies that row replacement does not silently add or ignore unknown columns.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentException>(() => df.Rows.Update(1, new
        {
            Name = "Zeynep",
            Age = 29,
            Salary = 110000m,
            IsActive = true,
            UnknownColumn = "X"
        }));

        Assert.Contains("UnknownColumn", exception.Message);
    }

    /// <summary>
    /// Verifies that incompatible row value types are rejected.
    /// </summary>
    [Fact]
    public void RowsUpdate_WithIncompatibleValueType_ThrowsArgumentException()
    {
        // Verifies that row replacement does not convert incompatible values.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentException>(() => df.Rows.Update(1, new
        {
            Name = "Zeynep",
            Age = "29",
            Salary = 110000m,
            IsActive = true
        }));

        Assert.Contains("Age", exception.Message);
        Assert.Contains("System.String", exception.Message);
    }

    /// <summary>
    /// Verifies that nullable columns accept null replacement values.
    /// </summary>
    [Fact]
    public void RowsUpdate_WithNullForNullableColumn_ReplacesRow()
    {
        // Verifies that existing nullable column metadata allows null update values.
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali", "Ayse" }, Score = new int?[] { 10, 20 } });

        df.Rows.Update(1, new { Name = (string?)null, Score = (int?)null });

        Assert.Null(df.GetRow(1)["Name"]);
        Assert.Null(df.GetRow(1)["Score"]);
        Assert.True(df.Schema.GetColumn("Name").IsNullable);
        Assert.True(df.Schema.GetColumn("Score").IsNullable);
    }

    /// <summary>
    /// Verifies that non-nullable columns reject null replacement values.
    /// </summary>
    [Fact]
    public void RowsUpdate_WithNullForNonNullableColumn_ThrowsArgumentException()
    {
        // Verifies that row replacement preserves non-nullable column validation.
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali", "Ayse" }, Age = new[] { 30, 25 } });

        var exception = Assert.Throws<ArgumentException>(() => df.Rows.Update(1, new { Name = "Zeynep", Age = (int?)null }));

        Assert.Contains("Age", exception.Message);
        Assert.Contains("null", exception.Message);
    }

    /// <summary>
    /// Verifies that partial row replacements are rejected.
    /// </summary>
    [Fact]
    public void RowsUpdate_WithPartialRow_ThrowsArgumentException()
    {
        // Verifies that RowsUpdate requires full-row replacement rather than partial updates.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentException>(() => df.Rows.Update(1, new { Salary = 110000m }));

        Assert.Contains("Name", exception.Message);
    }

    private static global::Runiq.Data.DataFrame CreatePeopleDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse", "Mehmet" },
            Age = new[] { 30, 25, 41 },
            Salary = new[] { 120000m, 95000m, 150000m },
            IsActive = new[] { true, true, false }
        });
    }

    private static object ValidRow()
    {
        return new { Name = "Zeynep", Age = 29, Salary = 110000m, IsActive = true };
    }

    private static string[] ColumnNames(global::Runiq.Data.DataFrame df)
    {
        return df.Columns.Select(static column => column.Name).ToArray();
    }

    private static string[] SchemaNames(global::Runiq.Data.DataFrame df)
    {
        return df.Schema.Columns.Select(static column => column.Name).ToArray();
    }

    private static string[] RowNames(global::Runiq.Data.DataFrame df)
    {
        return Enumerable.Range(0, df.RowCount)
            .Select(index => (string)df.GetRow(index)["Name"]!)
            .ToArray();
    }
}
