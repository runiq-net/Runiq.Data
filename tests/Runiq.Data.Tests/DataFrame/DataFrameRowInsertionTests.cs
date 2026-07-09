namespace Runiq.Data.Tests.DataFrame;

/// <summary>
/// Verifies immutable and mutable row insertion behavior on DataFrame.
/// </summary>
public sealed class DataFrameRowInsertionTests
{
    /// <summary>
    /// Verifies that WithRow appends a row and returns a separate DataFrame.
    /// </summary>
    [Fact]
    public void WithRow_AppendsRowAndReturnsNewDataFrame()
    {
        // Verifies immutable row append behavior and the resulting shape.
        var df = CreatePeopleDataFrame();

        var updated = df.WithRow(new { Name = "Zeynep", Age = 29, Salary = 110000m, IsActive = true });

        Assert.NotSame(df, updated);
        Assert.Equal(3, updated.RowCount);
        Assert.Equal(2, df.RowCount);
        Assert.Equal("Zeynep", updated.GetRow(2)["Name"]);
        Assert.Equal(29, updated.GetRow(2)["Age"]);
        Assert.Equal(110000m, updated.GetRow(2)["Salary"]);
        Assert.Equal(true, updated.GetRow(2)["IsActive"]);
    }

    /// <summary>
    /// Verifies that WithRow leaves the source DataFrame unchanged.
    /// </summary>
    [Fact]
    public void WithRow_DoesNotMutateOriginalDataFrame()
    {
        // Verifies that immutable row append preserves the original instance.
        var df = CreatePeopleDataFrame();

        var updated = df.WithRow(new { Name = "Zeynep", Age = 29, Salary = 110000m, IsActive = true });

        Assert.Equal(2, df.RowCount);
        Assert.Equal("Ali", df.GetRow(0)["Name"]);
        Assert.Equal("Ayse", df.GetRow(1)["Name"]);
        Assert.Equal(3, updated.RowCount);
    }

    /// <summary>
    /// Verifies that AddRow appends a row by mutating the current DataFrame.
    /// </summary>
    [Fact]
    public void AddRow_AppendsRowAndMutatesCurrentDataFrame()
    {
        // Verifies explicit mutable row append behavior.
        var df = CreatePeopleDataFrame();

        df.AddRow(new { Name = "Zeynep", Age = 29, Salary = 110000m, IsActive = true });

        Assert.Equal(3, df.RowCount);
        Assert.Equal("Zeynep", df.GetRow(2)["Name"]);
        Assert.Equal(29, df.GetRow(2)["Age"]);
        Assert.Equal(110000m, df.GetRow(2)["Salary"]);
        Assert.Equal(true, df.GetRow(2)["IsActive"]);
    }

    /// <summary>
    /// Verifies that appended rows preserve column order and schema metadata.
    /// </summary>
    [Fact]
    public void WithRow_PreservesColumnOrderAndSchema()
    {
        // Verifies that row insertion does not reorder or reshape columns.
        var df = CreatePeopleDataFrame();

        var updated = df.WithRow(new { Name = "Zeynep", Age = 29, Salary = 110000m, IsActive = true });

        Assert.Equal(new[] { "Name", "Age", "Salary", "IsActive" }, ColumnNames(updated));
        Assert.Equal(new[] { "Name", "Age", "Salary", "IsActive" }, SchemaNames(updated));
        Assert.Equal(typeof(string), updated.Schema.GetColumn("Name").DataType);
        Assert.Equal(typeof(int), updated.Schema.GetColumn("Age").DataType);
        Assert.Equal(typeof(decimal), updated.Schema.GetColumn("Salary").DataType);
        Assert.Equal(typeof(bool), updated.Schema.GetColumn("IsActive").DataType);
        Assert.True(updated.Schema.GetColumn("Name").IsNullable);
        Assert.False(updated.Schema.GetColumn("Age").IsNullable);
    }

    /// <summary>
    /// Verifies that missing row properties are rejected.
    /// </summary>
    [Fact]
    public void WithRow_WithMissingProperty_Throws()
    {
        // Verifies that rows must supply every existing DataFrame column.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentException>(() => df.WithRow(new { Name = "Zeynep", Age = 29 }));

        Assert.Contains("Salary", exception.Message);
    }

    /// <summary>
    /// Verifies that extra row properties are rejected.
    /// </summary>
    [Fact]
    public void WithRow_WithExtraProperty_Throws()
    {
        // Verifies that row insertion does not silently add or ignore unknown columns.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentException>(() => df.WithRow(new
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
    public void WithRow_WithIncompatibleValueType_Throws()
    {
        // Verifies that row insertion does not convert incompatible values.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentException>(() => df.WithRow(new
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
    /// Verifies that null row arguments are rejected.
    /// </summary>
    [Fact]
    public void WithRow_WithNullRow_Throws()
    {
        // Verifies that row insertion fails fast when no row object is supplied.
        var df = CreatePeopleDataFrame();

        Assert.Throws<ArgumentNullException>(() => df.WithRow(null!));
        Assert.Throws<ArgumentNullException>(() => df.AddRow(null!));
    }

    /// <summary>
    /// Verifies that rows can be added to an empty DataFrame with an existing schema.
    /// </summary>
    [Fact]
    public void AddRow_ToEmptyDataFrameWithExistingSchema_AppendsRow()
    {
        // Verifies that a zero-row DataFrame with schema accepts a matching row.
        var df = CreatePeopleDataFrame().Head(0);

        df.AddRow(new { Name = "Zeynep", Age = 29, Salary = 110000m, IsActive = true });

        Assert.Equal(1, df.RowCount);
        Assert.Equal("Zeynep", df.GetRow(0)["Name"]);
        Assert.Equal(new[] { "Name", "Age", "Salary", "IsActive" }, ColumnNames(df));
    }

    /// <summary>
    /// Verifies that WithRow keeps existing rows unchanged in the returned DataFrame.
    /// </summary>
    [Fact]
    public void WithRow_PreservesExistingRows()
    {
        // Verifies that rows before the appended row keep their original values.
        var df = CreatePeopleDataFrame();

        var updated = df.WithRow(new { Name = "Zeynep", Age = 29, Salary = 110000m, IsActive = true });

        Assert.Equal("Ali", updated.GetRow(0)["Name"]);
        Assert.Equal(30, updated.GetRow(0)["Age"]);
        Assert.Equal("Ayse", updated.GetRow(1)["Name"]);
        Assert.Equal(25, updated.GetRow(1)["Age"]);
    }

    /// <summary>
    /// Verifies that AddRow keeps existing rows unchanged before the appended row.
    /// </summary>
    [Fact]
    public void AddRow_PreservesExistingRowsBeforeAppendedRow()
    {
        // Verifies that mutable row append only adds the final row.
        var df = CreatePeopleDataFrame();

        df.AddRow(new { Name = "Zeynep", Age = 29, Salary = 110000m, IsActive = true });

        Assert.Equal("Ali", df.GetRow(0)["Name"]);
        Assert.Equal(30, df.GetRow(0)["Age"]);
        Assert.Equal("Ayse", df.GetRow(1)["Name"]);
        Assert.Equal(25, df.GetRow(1)["Age"]);
    }

    /// <summary>
    /// Verifies that nullable columns accept null row values.
    /// </summary>
    [Fact]
    public void WithRow_WithNullForNullableColumn_AppendsRow()
    {
        // Verifies that existing nullability metadata controls null row values.
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" }, Score = new int?[] { 10 } });

        var updated = df.WithRow(new { Name = (string?)null, Score = (int?)null });

        Assert.Equal(2, updated.RowCount);
        Assert.Null(updated.GetRow(1)["Name"]);
        Assert.Null(updated.GetRow(1)["Score"]);
    }

    /// <summary>
    /// Verifies that non-nullable columns reject null row values.
    /// </summary>
    [Fact]
    public void WithRow_WithNullForNonNullableColumn_Throws()
    {
        // Verifies that null values cannot be inserted into non-nullable column types.
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" }, Age = new[] { 30 } });

        var exception = Assert.Throws<ArgumentException>(() => df.WithRow(new { Name = "Zeynep", Age = (int?)null }));

        Assert.Contains("Age", exception.Message);
        Assert.Contains("null", exception.Message);
    }

    private static global::Runiq.Data.DataFrame CreatePeopleDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Age = new[] { 30, 25 },
            Salary = new[] { 120000m, 95000m },
            IsActive = new[] { true, true }
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
