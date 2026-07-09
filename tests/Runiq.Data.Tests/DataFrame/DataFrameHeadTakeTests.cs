namespace Runiq.Data.Tests.DataFrame;

/// <summary>
/// Verifies leading-row limiting behavior on DataFrame.
/// </summary>
public sealed class DataFrameHeadTakeTests
{
    /// <summary>
    /// Verifies that Head returns the requested leading rows.
    /// </summary>
    [Fact]
    public void Head_WhenCountIsLessThanRowCount_ReturnsFirstRows()
    {
        // This test verifies that Head returns a new DataFrame containing only the requested leading rows.
        var df = CreatePeopleDataFrame();

        var result = df.Head(2);

        Assert.NotSame(df, result);
        Assert.Equal(2, result.RowCount);
        Assert.Equal(new[] { "Ali", "Ayse" }, Names(result));
    }

    /// <summary>
    /// Verifies that Head can return an empty DataFrame with the same schema.
    /// </summary>
    [Fact]
    public void Head_WhenCountIsZero_ReturnsEmptyDataFrameWithSameSchema()
    {
        // This test verifies that Head(0) keeps column schema while removing all rows.
        var df = CreatePeopleDataFrame();

        var result = df.Head(0);

        Assert.Equal(0, result.RowCount);
        Assert.Equal(df.ColumnCount, result.ColumnCount);
        Assert.Equal(ColumnNames(df), ColumnNames(result));
        Assert.Equal(SchemaNames(df), SchemaNames(result));
    }

    /// <summary>
    /// Verifies that Head with the exact row count returns all rows.
    /// </summary>
    [Fact]
    public void Head_WhenCountEqualsRowCount_ReturnsAllRows()
    {
        // This test verifies that Head(rowCount) includes every source row.
        var df = CreatePeopleDataFrame();

        var result = df.Head(df.RowCount);

        Assert.Equal(df.RowCount, result.RowCount);
        Assert.Equal(Names(df), Names(result));
    }

    /// <summary>
    /// Verifies that Head with a large count returns all available rows.
    /// </summary>
    [Fact]
    public void Head_WhenCountExceedsRowCount_ReturnsAllRows()
    {
        // This test verifies that Head does not fail when the requested count is larger than the source row count.
        var df = CreatePeopleDataFrame();

        var result = df.Head(999);

        Assert.Equal(df.RowCount, result.RowCount);
        Assert.Equal(Names(df), Names(result));
    }

    /// <summary>
    /// Verifies that Head rejects negative counts.
    /// </summary>
    [Fact]
    public void Head_WhenCountIsNegative_ThrowsArgumentOutOfRangeException()
    {
        // This test verifies that negative Head counts fail fast instead of returning an empty DataFrame.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => df.Head(-1));

        Assert.Equal("count", exception.ParamName);
        Assert.Contains("zero or greater", exception.Message);
    }

    /// <summary>
    /// Verifies that Head does not mutate the original DataFrame.
    /// </summary>
    [Fact]
    public void Head_DoesNotModifyOriginalDataFrame()
    {
        // This test verifies that Head returns a separate DataFrame and keeps the source intact.
        var df = CreatePeopleDataFrame();

        var result = df.Head(1);

        Assert.NotSame(df, result);
        Assert.Equal(3, df.RowCount);
        Assert.Equal(new[] { "Ali", "Ayse", "Mehmet" }, Names(df));
        Assert.Equal(new[] { "Ali" }, Names(result));
    }

    /// <summary>
    /// Verifies that Head preserves column and row order.
    /// </summary>
    [Fact]
    public void Head_PreservesColumnOrderAndRowOrder()
    {
        // This test verifies that Head keeps source column order and the original leading row order.
        var df = CreatePeopleDataFrame();

        var result = df.Head(2);

        Assert.Equal(new[] { "Name", "Age", "Salary", "IsActive" }, ColumnNames(result));
        Assert.Equal("Ali", result["Name"].GetValue(0));
        Assert.Equal("Ayse", result["Name"].GetValue(1));
    }

    /// <summary>
    /// Verifies that Head preserves schema data types and nullability.
    /// </summary>
    [Fact]
    public void Head_PreservesSchemaTypeAndNullability()
    {
        // This test verifies that Head copies rows without changing schema metadata.
        var df = CreateNullableDataFrame();

        var result = df.Head(1);

        Assert.Equal(typeof(string), result.Schema.GetColumn("Name").DataType);
        Assert.True(result.Schema.GetColumn("Name").IsNullable);
        Assert.Equal(typeof(int?), result.Schema.GetColumn("Score").DataType);
        Assert.True(result.Schema.GetColumn("Score").IsNullable);
        Assert.Equal(typeof(bool), result.Schema.GetColumn("IsActive").DataType);
        Assert.False(result.Schema.GetColumn("IsActive").IsNullable);
    }

    /// <summary>
    /// Verifies that Take returns the requested leading rows.
    /// </summary>
    [Fact]
    public void Take_WhenCountIsLessThanRowCount_ReturnsFirstRows()
    {
        // This test verifies that Take returns a new DataFrame containing only the requested leading rows.
        var df = CreatePeopleDataFrame();

        var result = df.Take(2);

        Assert.NotSame(df, result);
        Assert.Equal(2, result.RowCount);
        Assert.Equal(new[] { "Ali", "Ayse" }, Names(result));
    }

    /// <summary>
    /// Verifies that Take can return an empty DataFrame with the same schema.
    /// </summary>
    [Fact]
    public void Take_WhenCountIsZero_ReturnsEmptyDataFrameWithSameSchema()
    {
        // This test verifies that Take(0) keeps column schema while removing all rows.
        var df = CreatePeopleDataFrame();

        var result = df.Take(0);

        Assert.Equal(0, result.RowCount);
        Assert.Equal(df.ColumnCount, result.ColumnCount);
        Assert.Equal(ColumnNames(df), ColumnNames(result));
        Assert.Equal(SchemaNames(df), SchemaNames(result));
    }

    /// <summary>
    /// Verifies that Take with the exact row count returns all rows.
    /// </summary>
    [Fact]
    public void Take_WhenCountEqualsRowCount_ReturnsAllRows()
    {
        // This test verifies that Take(rowCount) includes every source row.
        var df = CreatePeopleDataFrame();

        var result = df.Take(df.RowCount);

        Assert.Equal(df.RowCount, result.RowCount);
        Assert.Equal(Names(df), Names(result));
    }

    /// <summary>
    /// Verifies that Take with a large count returns all available rows.
    /// </summary>
    [Fact]
    public void Take_WhenCountExceedsRowCount_ReturnsAllRows()
    {
        // This test verifies that Take does not fail when the requested count is larger than the source row count.
        var df = CreatePeopleDataFrame();

        var result = df.Take(999);

        Assert.Equal(df.RowCount, result.RowCount);
        Assert.Equal(Names(df), Names(result));
    }

    /// <summary>
    /// Verifies that Take rejects negative counts.
    /// </summary>
    [Fact]
    public void Take_WhenCountIsNegative_ThrowsArgumentOutOfRangeException()
    {
        // This test verifies that negative Take counts fail fast instead of returning an empty DataFrame.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => df.Take(-1));

        Assert.Equal("count", exception.ParamName);
        Assert.Contains("zero or greater", exception.Message);
    }

    /// <summary>
    /// Verifies that Take does not mutate the original DataFrame.
    /// </summary>
    [Fact]
    public void Take_DoesNotModifyOriginalDataFrame()
    {
        // This test verifies that Take returns a separate DataFrame and keeps the source intact.
        var df = CreatePeopleDataFrame();

        var result = df.Take(1);

        Assert.NotSame(df, result);
        Assert.Equal(3, df.RowCount);
        Assert.Equal(new[] { "Ali", "Ayse", "Mehmet" }, Names(df));
        Assert.Equal(new[] { "Ali" }, Names(result));
    }

    /// <summary>
    /// Verifies that Take preserves column and row order.
    /// </summary>
    [Fact]
    public void Take_PreservesColumnOrderAndRowOrder()
    {
        // This test verifies that Take keeps source column order and the original leading row order.
        var df = CreatePeopleDataFrame();

        var result = df.Take(2);

        Assert.Equal(new[] { "Name", "Age", "Salary", "IsActive" }, ColumnNames(result));
        Assert.Equal("Ali", result["Name"].GetValue(0));
        Assert.Equal("Ayse", result["Name"].GetValue(1));
    }

    /// <summary>
    /// Verifies that Take preserves schema data types and nullability.
    /// </summary>
    [Fact]
    public void Take_PreservesSchemaTypeAndNullability()
    {
        // This test verifies that Take copies rows without changing schema metadata.
        var df = CreateNullableDataFrame();

        var result = df.Take(1);

        Assert.Equal(typeof(string), result.Schema.GetColumn("Name").DataType);
        Assert.True(result.Schema.GetColumn("Name").IsNullable);
        Assert.Equal(typeof(int?), result.Schema.GetColumn("Score").DataType);
        Assert.True(result.Schema.GetColumn("Score").IsNullable);
        Assert.Equal(typeof(bool), result.Schema.GetColumn("IsActive").DataType);
        Assert.False(result.Schema.GetColumn("IsActive").IsNullable);
    }

    /// <summary>
    /// Verifies that Head and Take return equivalent DataFrames.
    /// </summary>
    [Fact]
    public void HeadAndTake_WithSameCount_ReturnEquivalentDataFrames()
    {
        // This test verifies that Head and Take produce the same schema and row values for the same count.
        var df = CreatePeopleDataFrame();

        var byHead = df.Head(2);
        var byTake = df.Take(2);

        Assert.Equal(ColumnNames(byHead), ColumnNames(byTake));
        Assert.Equal(SchemaNames(byHead), SchemaNames(byTake));
        Assert.Equal(Names(byHead), Names(byTake));
        Assert.Equal(Ages(byHead), Ages(byTake));
    }

    /// <summary>
    /// Verifies that filtered DataFrames can be limited with Head.
    /// </summary>
    [Fact]
    public void FilterThenHead_ReturnsLeadingFilteredRows()
    {
        // This test verifies that Head composes after predicate filtering.
        var df = CreatePeopleDataFrame();

        var result = df.Filter(row => row["Age"] >= 30).Head(1);

        Assert.Equal(1, result.RowCount);
        Assert.Equal("Ali", result["Name"].GetValue(0));
    }

    /// <summary>
    /// Verifies that filtered DataFrames can be limited with Take.
    /// </summary>
    [Fact]
    public void FilterThenTake_ReturnsLeadingFilteredRows()
    {
        // This test verifies that Take composes after predicate filtering.
        var df = CreatePeopleDataFrame();

        var result = df.Filter(row => row["Age"] >= 30).Take(1);

        Assert.Equal(1, result.RowCount);
        Assert.Equal("Ali", result["Name"].GetValue(0));
    }

    /// <summary>
    /// Verifies that Head composes after string helper filtering.
    /// </summary>
    [Fact]
    public void StringHelperFilterThenHead_ReturnsLeadingFilteredRows()
    {
        // This test verifies that Head works after string helper predicates such as EndsWith.
        var df = CreateContactDataFrame();

        var result = df.Filter(row => row["Email"].EndsWith("@gmail.com")).Head(1);

        Assert.Equal(1, result.RowCount);
        Assert.Equal("Ali", result["Name"].GetValue(0));
    }

    /// <summary>
    /// Verifies that Take composes after string helper filtering.
    /// </summary>
    [Fact]
    public void StringHelperFilterThenTake_ReturnsLeadingFilteredRows()
    {
        // This test verifies that Take works after string helper predicates such as Contains.
        var df = CreateContactDataFrame();

        var result = df.Filter(row => row["Name"].Contains("Meh")).Take(1);

        Assert.Equal(1, result.RowCount);
        Assert.Equal("Mehmet", result["Name"].GetValue(0));
    }

    /// <summary>
    /// Verifies that DataFrameRow indexer behavior remains raw object access.
    /// </summary>
    [Fact]
    public void DataFrameRowIndexer_AfterHeadTakeFeature_ReturnsRawObjectValues()
    {
        // This test verifies that row access still returns raw object values and not filter helper cells.
        var row = CreatePeopleDataFrame().GetRow(0);

        object? age = row["Age"];

        Assert.IsType<int>(age);
        Assert.Equal(30, age);
    }

    /// <summary>
    /// Verifies that core numeric filtering still works.
    /// </summary>
    [Fact]
    public void Filter_WithNumericComparison_AfterHeadTakeFeature_StillWorks()
    {
        // This test verifies that the existing row filter pipeline keeps numeric comparison behavior.
        var df = CreatePeopleDataFrame();

        var result = df.Filter(row => row["Age"] >= 30);

        Assert.Equal(new[] { "Ali", "Mehmet" }, Names(result));
    }

    /// <summary>
    /// Verifies that string helper filtering still works.
    /// </summary>
    [Fact]
    public void Filter_WithStringHelper_AfterHeadTakeFeature_StillWorks()
    {
        // This test verifies that the existing row filter pipeline keeps string helper behavior.
        var df = CreateContactDataFrame();

        var result = df.Filter(row => row["Name"].Contains("Ali"));

        Assert.Equal(new[] { "Ali" }, Names(result));
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

    private static global::Runiq.Data.DataFrame CreateNullableDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Name = new string?[] { "Ali", null },
            Score = new int?[] { 10, null },
            IsActive = new[] { true, false }
        });
    }

    private static global::Runiq.Data.DataFrame CreateContactDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse", "Mehmet" },
            Email = new[] { "ali@gmail.com", "ayse@example.com", "mehmet@gmail.com" },
            Age = new[] { 30, 25, 41 },
            IsActive = new[] { true, true, false }
        });
    }

    private static string[] Names(global::Runiq.Data.DataFrame df)
    {
        return Enumerable.Range(0, df.RowCount)
            .Select(index => (string)df["Name"].GetValue(index)!)
            .ToArray();
    }

    private static int[] Ages(global::Runiq.Data.DataFrame df)
    {
        return Enumerable.Range(0, df.RowCount)
            .Select(index => (int)df["Age"].GetValue(index)!)
            .ToArray();
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
