namespace Runiq.Data.Tests.DataFrame;

/// <summary>
/// Verifies mutable row removal behavior through the Rows facade.
/// </summary>
public sealed class DataFrameRowRemovalTests
{
    /// <summary>
    /// Verifies that Rows.Remove removes the requested row and mutates the current DataFrame.
    /// </summary>
    [Fact]
    public void RowsRemove_RemovesRequestedRowAndMutatesCurrentDataFrame()
    {
        // Verifies that row removal changes the existing DataFrame instance.
        var df = CreatePeopleDataFrame();
        var sameInstance = df;

        df.Rows.Remove(1);

        Assert.Same(df, sameInstance);
        Assert.Equal(2, df.RowCount);
        Assert.Equal("Ali", df.GetRow(0)["Name"]);
        Assert.Equal("Mehmet", df.GetRow(1)["Name"]);
    }

    /// <summary>
    /// Verifies that Rows.Remove preserves column count, order, and schema.
    /// </summary>
    [Fact]
    public void RowsRemove_PreservesColumnCountOrderAndSchema()
    {
        // Verifies that removing a row does not reshape columns or schema metadata.
        var df = CreatePeopleDataFrame();
        var schema = df.Schema;

        df.Rows.Remove(1);

        Assert.Equal(4, df.ColumnCount);
        Assert.Equal(new[] { "Name", "Age", "Salary", "IsActive" }, ColumnNames(df));
        Assert.Equal(new[] { "Name", "Age", "Salary", "IsActive" }, SchemaNames(df));
        Assert.Same(schema, df.Schema);
        Assert.Equal(typeof(string), df.Schema.GetColumn("Name").DataType);
        Assert.Equal(typeof(int), df.Schema.GetColumn("Age").DataType);
        Assert.Equal(typeof(decimal), df.Schema.GetColumn("Salary").DataType);
        Assert.Equal(typeof(bool), df.Schema.GetColumn("IsActive").DataType);
    }

    /// <summary>
    /// Verifies that Rows.Remove preserves the relative order of remaining rows.
    /// </summary>
    [Fact]
    public void RowsRemove_PreservesRemainingRowOrder()
    {
        // Verifies that only the selected row is removed.
        var df = CreatePeopleDataFrame();

        df.Rows.Remove(1);

        Assert.Equal(new[] { "Ali", "Mehmet" }, RowNames(df));
    }

    /// <summary>
    /// Verifies that Rows.Remove can remove the first row.
    /// </summary>
    [Fact]
    public void RowsRemove_WithFirstIndex_RemovesFirstRow()
    {
        // Verifies that index zero is a valid removal target.
        var df = CreatePeopleDataFrame();

        df.Rows.Remove(0);

        Assert.Equal(2, df.RowCount);
        Assert.Equal(new[] { "Ayse", "Mehmet" }, RowNames(df));
    }

    /// <summary>
    /// Verifies that Rows.Remove can remove the last row.
    /// </summary>
    [Fact]
    public void RowsRemove_WithLastIndex_RemovesLastRow()
    {
        // Verifies that the final valid row index is a valid removal target.
        var df = CreatePeopleDataFrame();

        df.Rows.Remove(2);

        Assert.Equal(2, df.RowCount);
        Assert.Equal(new[] { "Ali", "Ayse" }, RowNames(df));
    }

    /// <summary>
    /// Verifies that Rows.Remove can remove the only row while preserving schema.
    /// </summary>
    [Fact]
    public void RowsRemove_WithOnlyRow_LeavesEmptyDataFrameWithSameSchema()
    {
        // Verifies that removing the last remaining row keeps the column schema.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali" },
            Age = new[] { 30 }
        });

        df.Rows.Remove(0);

        Assert.Equal(0, df.RowCount);
        Assert.Equal(2, df.ColumnCount);
        Assert.Equal(new[] { "Name", "Age" }, ColumnNames(df));
        Assert.Equal(new[] { "Name", "Age" }, SchemaNames(df));
        Assert.Equal(typeof(string), df.Schema.GetColumn("Name").DataType);
        Assert.Equal(typeof(int), df.Schema.GetColumn("Age").DataType);
    }

    /// <summary>
    /// Verifies that Rows.Remove rejects negative indexes.
    /// </summary>
    [Fact]
    public void RowsRemove_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        // Verifies that row removal fails fast for indexes below zero.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => df.Rows.Remove(-1));

        Assert.Equal("index", exception.ParamName);
    }

    /// <summary>
    /// Verifies that Rows.Remove rejects RowCount as an index.
    /// </summary>
    [Fact]
    public void RowsRemove_WhenIndexEqualsRowCount_ThrowsArgumentOutOfRangeException()
    {
        // Verifies that row removal fails fast when the index is outside the row range.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => df.Rows.Remove(df.RowCount));

        Assert.Equal("index", exception.ParamName);
    }

    /// <summary>
    /// Verifies that indexes greater than row count are rejected.
    /// </summary>
    [Fact]
    public void RowsRemove_WhenIndexIsGreaterThanRowCount_ThrowsAndLeavesDataFrameUnchanged()
    {
        // Verifies that an out-of-range removal does not change existing rows or shape.
        var df = CreatePeopleDataFrame();
        var originalRowCount = df.RowCount;
        var originalColumnCount = df.ColumnCount;
        var originalNames = RowNames(df);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => df.Rows.Remove(df.RowCount + 1));

        Assert.Equal("index", exception.ParamName);
        Assert.Equal(originalRowCount, df.RowCount);
        Assert.Equal(originalColumnCount, df.ColumnCount);
        Assert.Equal(originalNames, RowNames(df));
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
