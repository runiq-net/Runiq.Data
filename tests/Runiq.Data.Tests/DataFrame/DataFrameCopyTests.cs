namespace Runiq.Data.Tests.DataFrame;

/// <summary>
/// Verifies explicit DataFrame copy branching behavior.
/// </summary>
public sealed class DataFrameCopyTests
{
    /// <summary>
    /// Verifies that Copy returns a distinct DataFrame with the same schema and values.
    /// </summary>
    [Fact]
    public void Copy_ReturnsDistinctDataFrameAndPreservesSchemaOrderTypesRowsAndValues()
    {
        // Verifies that copy creates a separate branch with identical observable data.
        var df = CreatePeopleDataFrame();

        var copy = df.Copy();

        Assert.NotSame(df, copy);
        Assert.Equal(df.Rows.Count(), copy.Rows.Count());
        Assert.Equal(df.Columns.Count(), copy.Columns.Count());
        Assert.Equal(new[] { "Name", "Age", "Score" }, ColumnNames(copy));
        Assert.Equal(new[] { "Name", "Age", "Score" }, SchemaNames(copy));
        Assert.Equal(typeof(string), copy.Schema.GetColumn("Name").DataType);
        Assert.True(copy.Schema.GetColumn("Name").IsNullable);
        Assert.Equal(typeof(int), copy.Schema.GetColumn("Age").DataType);
        Assert.False(copy.Schema.GetColumn("Age").IsNullable);
        Assert.Equal(typeof(int?), copy.Schema.GetColumn("Score").DataType);
        Assert.True(copy.Schema.GetColumn("Score").IsNullable);
        Assert.Equal("Ali", copy.GetRow(0)["Name"]);
        Assert.Equal(25, copy.GetRow(1)["Age"]);
        Assert.Null(copy.GetRow(1)["Score"]);
    }

    /// <summary>
    /// Verifies that mutating a copy with ColumnsAdd does not mutate the original.
    /// </summary>
    [Fact]
    public void Copy_ColumnsAddOnCopy_DoesNotMutateOriginal()
    {
        // Verifies immutable-style column branching through Copy plus direct mutation.
        var df = CreatePeopleDataFrame();
        var copy = df.Copy();

        copy.Columns.Add("Active", new[] { true, false });

        Assert.False(df.HasColumn("Active"));
        Assert.True(copy.HasColumn("Active"));
        Assert.Equal(3, df.Columns.Count());
        Assert.Equal(4, copy.Columns.Count());
    }

    /// <summary>
    /// Verifies that mutating a copy with ColumnsRemove does not mutate the original.
    /// </summary>
    [Fact]
    public void Copy_ColumnsRemoveOnCopy_DoesNotMutateOriginal()
    {
        // Verifies that column removal on a copied branch is isolated.
        var df = CreatePeopleDataFrame();
        var copy = df.Copy();

        copy.Columns.Remove("Score");

        Assert.True(df.HasColumn("Score"));
        Assert.False(copy.HasColumn("Score"));
        Assert.Equal(new[] { "Name", "Age", "Score" }, ColumnNames(df));
        Assert.Equal(new[] { "Name", "Age" }, ColumnNames(copy));
    }

    /// <summary>
    /// Verifies that mutating a copy with ColumnsRename does not mutate the original.
    /// </summary>
    [Fact]
    public void Copy_ColumnsRenameOnCopy_DoesNotMutateOriginal()
    {
        // Verifies that rename on a copied branch preserves the source branch.
        var df = CreatePeopleDataFrame();
        var copy = df.Copy();

        copy.Columns.Rename("Score", "Points");

        Assert.True(df.HasColumn("Score"));
        Assert.False(df.HasColumn("Points"));
        Assert.False(copy.HasColumn("Score"));
        Assert.True(copy.HasColumn("Points"));
        Assert.Null(copy.GetRow(1)["Points"]);
    }

    /// <summary>
    /// Verifies that mutating a copy with RowsAdd does not mutate the original.
    /// </summary>
    [Fact]
    public void Copy_RowsAddOnCopy_DoesNotMutateOriginal()
    {
        // Verifies that row append on a copied branch is isolated.
        var df = CreatePeopleDataFrame();
        var copy = df.Copy();

        copy.Rows.Add(new { Name = "Zeynep", Age = 29, Score = (int?)40 });

        Assert.Equal(2, df.Rows.Count());
        Assert.Equal(3, copy.Rows.Count());
        Assert.Equal("Zeynep", copy.GetRow(2)["Name"]);
    }

    /// <summary>
    /// Verifies that mutating the original after Copy does not mutate the copy.
    /// </summary>
    [Fact]
    public void Copy_MutatingOriginalAfterCopy_DoesNotMutateCopy()
    {
        // Verifies that the copied branch remains stable when the source branch changes later.
        var df = CreatePeopleDataFrame();
        var copy = df.Copy();

        df.Columns.Add("Active", new[] { true, false });
        df.Columns.Rename("Age", "Years");
        df.Rows.Add(new { Name = "Zeynep", Years = 29, Score = (int?)40, Active = true });

        Assert.Equal(2, copy.Rows.Count());
        Assert.Equal(3, copy.Columns.Count());
        Assert.True(copy.HasColumn("Age"));
        Assert.False(copy.HasColumn("Years"));
        Assert.False(copy.HasColumn("Active"));
        Assert.Equal("Ayse", copy.GetRow(1)["Name"]);
    }

    private static global::Runiq.Data.DataFrame CreatePeopleDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Age = new[] { 30, 25 },
            Score = new int?[] { 10, null }
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
}

