namespace Runiq.Data.Tests.DataFrame;

/// <summary>
/// Verifies direct row append behavior on DataFrame.
/// </summary>
public sealed class DataFrameRowInsertionTests
{
    /// <summary>
    /// Verifies that RowsAdd appends a row by mutating the current DataFrame.
    /// </summary>
    [Fact]
    public void RowsAdd_AppendsRowAndMutatesCurrentDataFrame()
    {
        // Verifies direct mutable row append behavior.
        var df = CreatePeopleDataFrame();

        df.Rows.Add(new { Name = "Zeynep", Age = 29, Salary = 110000m, IsActive = true });

        Assert.Equal(3, df.Rows.Count());
        Assert.Equal("Zeynep", df.GetRow(2)["Name"]);
        Assert.Equal(29, df.GetRow(2)["Age"]);
        Assert.Equal(110000m, df.GetRow(2)["Salary"]);
        Assert.Equal(true, df.GetRow(2)["IsActive"]);
    }

    /// <summary>
    /// Verifies that appended rows preserve column order and schema metadata.
    /// </summary>
    [Fact]
    public void RowsAdd_PreservesColumnOrderAndSchema()
    {
        // Verifies that row append does not reorder or reshape columns.
        var df = CreatePeopleDataFrame();

        df.Rows.Add(new { Name = "Zeynep", Age = 29, Salary = 110000m, IsActive = true });

        Assert.Equal(new[] { "Name", "Age", "Salary", "IsActive" }, ColumnNames(df));
        Assert.Equal(new[] { "Name", "Age", "Salary", "IsActive" }, SchemaNames(df));
        Assert.Equal(typeof(string), df.Schema.GetColumn("Name").DataType);
        Assert.Equal(typeof(int), df.Schema.GetColumn("Age").DataType);
        Assert.Equal(typeof(decimal), df.Schema.GetColumn("Salary").DataType);
        Assert.Equal(typeof(bool), df.Schema.GetColumn("IsActive").DataType);
        Assert.True(df.Schema.GetColumn("Name").IsNullable);
        Assert.False(df.Schema.GetColumn("Age").IsNullable);
    }

    /// <summary>
    /// Verifies that missing row properties are rejected.
    /// </summary>
    [Fact]
    public void RowsAdd_WithMissingProperty_Throws()
    {
        // Verifies that rows must supply every existing DataFrame column.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentException>(() => df.Rows.Add(new { Name = "Zeynep", Age = 29 }));

        Assert.Contains("Salary", exception.Message);
    }

    /// <summary>
    /// Verifies that extra row properties are rejected.
    /// </summary>
    [Fact]
    public void RowsAdd_WithExtraProperty_Throws()
    {
        // Verifies that row append does not silently add or ignore unknown columns.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentException>(() => df.Rows.Add(new
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
    public void RowsAdd_WithIncompatibleValueType_Throws()
    {
        // Verifies that row append does not convert incompatible values.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentException>(() => df.Rows.Add(new
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
    public void RowsAdd_WithNullRow_Throws()
    {
        // Verifies that row append fails fast when no row object is supplied.
        var df = CreatePeopleDataFrame();

        Assert.Throws<ArgumentNullException>(() => df.Rows.Add(null!));
    }

    /// <summary>
    /// Verifies that rows can be added to an empty DataFrame with an existing schema.
    /// </summary>
    [Fact]
    public void RowsAdd_ToEmptyDataFrameWithExistingSchema_AppendsRow()
    {
        // Verifies that a zero-row DataFrame with schema accepts a matching row.
        var df = CreatePeopleDataFrame().Head(0);

        df.Rows.Add(new { Name = "Zeynep", Age = 29, Salary = 110000m, IsActive = true });

        Assert.Equal(1, df.Rows.Count());
        Assert.Equal("Zeynep", df.GetRow(0)["Name"]);
        Assert.Equal(new[] { "Name", "Age", "Salary", "IsActive" }, ColumnNames(df));
    }

    /// <summary>
    /// Verifies that RowsAdd keeps existing rows unchanged before the appended row.
    /// </summary>
    [Fact]
    public void RowsAdd_PreservesExistingRowsBeforeAppendedRow()
    {
        // Verifies that mutable row append only adds the final row.
        var df = CreatePeopleDataFrame();

        df.Rows.Add(new { Name = "Zeynep", Age = 29, Salary = 110000m, IsActive = true });

        Assert.Equal("Ali", df.GetRow(0)["Name"]);
        Assert.Equal(30, df.GetRow(0)["Age"]);
        Assert.Equal("Ayse", df.GetRow(1)["Name"]);
        Assert.Equal(25, df.GetRow(1)["Age"]);
    }

    /// <summary>
    /// Verifies that nullable columns accept null row values.
    /// </summary>
    [Fact]
    public void RowsAdd_WithNullForNullableColumn_AppendsRow()
    {
        // Verifies that existing nullability metadata controls null row values.
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" }, Score = new int?[] { 10 } });

        df.Rows.Add(new { Name = (string?)null, Score = (int?)null });

        Assert.Equal(2, df.Rows.Count());
        Assert.Null(df.GetRow(1)["Name"]);
        Assert.Null(df.GetRow(1)["Score"]);
    }

    /// <summary>
    /// Verifies that non-nullable columns reject null row values.
    /// </summary>
    [Fact]
    public void RowsAdd_WithNullForNonNullableColumn_Throws()
    {
        // Verifies that null values cannot be inserted into non-nullable column types.
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" }, Age = new[] { 30 } });

        var exception = Assert.Throws<ArgumentException>(() => df.Rows.Add(new { Name = "Zeynep", Age = (int?)null }));

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

