namespace Runiq.Data.Tests.DataFrame;

/// <summary>
/// Verifies row and column count facade behavior.
/// </summary>
public sealed class DataFrameCountFacadeTests
{
    [Fact]
    public void RowsCount_ReturnsCurrentRowTotalAndReflectsRowMutations()
    {
        // Verifies that row counts track add and remove operations while update keeps the same total.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1, 2 }, Name = new[] { "Ali", "Ayse" } });

        Assert.Equal(2, df.Rows.Count());

        df.Rows.Add(new { Id = 3, Name = "Can" });
        Assert.Equal(3, df.Rows.Count());

        df.Rows.Update(1, new { Id = 20, Name = "Updated" });
        Assert.Equal(3, df.Rows.Count());

        df.Rows.Remove(0);
        Assert.Equal(2, df.Rows.Count());
    }

    [Fact]
    public void ColumnsCount_ReturnsCurrentColumnTotalAndReflectsColumnMutations()
    {
        // Verifies that column counts track add and remove operations while rename keeps the same total.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1, 2 }, Name = new[] { "Ali", "Ayse" } });

        Assert.Equal(2, df.Columns.Count());

        df.Columns.Add("Active", new[] { true, false });
        Assert.Equal(3, df.Columns.Count());

        df.Columns.Rename("Name", "CustomerName");
        Assert.Equal(3, df.Columns.Count());

        df.Columns.Remove("Active");
        Assert.Equal(2, df.Columns.Count());
    }

    [Fact]
    public void CountMethods_DoNotMutateDataFrame()
    {
        // Verifies that reading row and column counts leaves shape, order, and values unchanged.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1, 2 }, Name = new[] { "Ali", "Ayse" } });

        var rowTotal = df.Rows.Count();
        var columnTotal = df.Columns.Count();

        Assert.Equal(2, rowTotal);
        Assert.Equal(2, columnTotal);
        Assert.Equal(new[] { "Id", "Name" }, df.Columns.Select(static column => column.Name));
        Assert.Equal(1, df["Id"].GetValue(0));
        Assert.Equal("Ayse", df["Name"].GetValue(1));
    }
}
