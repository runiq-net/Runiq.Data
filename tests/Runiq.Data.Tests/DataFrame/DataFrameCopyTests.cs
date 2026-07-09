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
        Assert.Equal(df.RowCount, copy.RowCount);
        Assert.Equal(df.ColumnCount, copy.ColumnCount);
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
    /// Verifies that mutating a copy with AddColumn does not mutate the original.
    /// </summary>
    [Fact]
    public void Copy_AddColumnOnCopy_DoesNotMutateOriginal()
    {
        // Verifies immutable-style column branching through Copy plus direct mutation.
        var df = CreatePeopleDataFrame();
        var copy = df.Copy();

        copy.AddColumn("Active", new[] { true, false });

        Assert.False(df.HasColumn("Active"));
        Assert.True(copy.HasColumn("Active"));
        Assert.Equal(3, df.ColumnCount);
        Assert.Equal(4, copy.ColumnCount);
    }

    /// <summary>
    /// Verifies that mutating a copy with RemoveColumn does not mutate the original.
    /// </summary>
    [Fact]
    public void Copy_RemoveColumnOnCopy_DoesNotMutateOriginal()
    {
        // Verifies that column removal on a copied branch is isolated.
        var df = CreatePeopleDataFrame();
        var copy = df.Copy();

        copy.RemoveColumn("Score");

        Assert.True(df.HasColumn("Score"));
        Assert.False(copy.HasColumn("Score"));
        Assert.Equal(new[] { "Name", "Age", "Score" }, ColumnNames(df));
        Assert.Equal(new[] { "Name", "Age" }, ColumnNames(copy));
    }

    /// <summary>
    /// Verifies that mutating a copy with RenameColumn does not mutate the original.
    /// </summary>
    [Fact]
    public void Copy_RenameColumnOnCopy_DoesNotMutateOriginal()
    {
        // Verifies that rename on a copied branch preserves the source branch.
        var df = CreatePeopleDataFrame();
        var copy = df.Copy();

        copy.RenameColumn("Score", "Points");

        Assert.True(df.HasColumn("Score"));
        Assert.False(df.HasColumn("Points"));
        Assert.False(copy.HasColumn("Score"));
        Assert.True(copy.HasColumn("Points"));
        Assert.Null(copy.GetRow(1)["Points"]);
    }

    /// <summary>
    /// Verifies that mutating a copy with AddRow does not mutate the original.
    /// </summary>
    [Fact]
    public void Copy_AddRowOnCopy_DoesNotMutateOriginal()
    {
        // Verifies that row append on a copied branch is isolated.
        var df = CreatePeopleDataFrame();
        var copy = df.Copy();

        copy.AddRow(new { Name = "Zeynep", Age = 29, Score = (int?)40 });

        Assert.Equal(2, df.RowCount);
        Assert.Equal(3, copy.RowCount);
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

        df.AddColumn("Active", new[] { true, false });
        df.RenameColumn("Age", "Years");
        df.AddRow(new { Name = "Zeynep", Years = 29, Score = (int?)40, Active = true });

        Assert.Equal(2, copy.RowCount);
        Assert.Equal(3, copy.ColumnCount);
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
