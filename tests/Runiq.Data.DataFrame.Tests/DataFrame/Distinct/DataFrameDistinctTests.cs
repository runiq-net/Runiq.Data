namespace Runiq.Data.DataFrameTests.Distinct;

/// <summary>
/// Verifies duplicate-row projection behavior on DataFrame.
/// </summary>
public sealed class DataFrameDistinctTests
{
    /// <summary>
    /// Verifies that Distinct removes rows where every column value matches an earlier row.
    /// </summary>
    [Fact]
    public void Distinct_WithAllColumns_RemovesFullyDuplicateRows()
    {
        // This test verifies that the default duplicate key includes every source column.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 1, 2, 1, 3 },
            Country = new[] { "TR", "TR", "TR", "DE" },
            City = new[] { "Istanbul", "Ankara", "Istanbul", "Berlin" }
        });

        var result = df.Distinct();

        Assert.Equal(new[] { 1, 2, 3 }, Ids(result));
        Assert.Equal(new[] { "TR", "TR", "DE" }, Countries(result));
    }

    /// <summary>
    /// Verifies that Distinct keeps rows when any non-matching column makes the full row unique.
    /// </summary>
    [Fact]
    public void Distinct_WithAllColumns_KeepsRowsWhenAnyColumnDiffers()
    {
        // This test verifies that full-row distinct does not collapse rows that only share some values.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Country = new[] { "TR", "TR", "TR" },
            City = new[] { "Istanbul", "Istanbul", "Ankara" },
            Score = new[] { 10, 20, 10 }
        });

        var result = df.Distinct();

        Assert.Equal(3, result.Rows.Count());
        Assert.Equal(new[] { "Istanbul", "Istanbul", "Ankara" }, Cities(result));
    }

    /// <summary>
    /// Verifies that Distinct with one column uses only that column as the duplicate key.
    /// </summary>
    [Fact]
    public void Distinct_WithOneColumn_UsesSelectedColumnOnly()
    {
        // This test verifies that non-key column differences do not prevent selected-key duplicate removal.
        var df = CreateDuplicateRowsDataFrame();

        var result = df.Distinct("Country");

        Assert.Equal(new[] { 1, 3 }, Ids(result));
        Assert.Equal(new[] { "TR", "DE" }, Countries(result));
        Assert.Equal(new[] { "Istanbul", "Berlin" }, Cities(result));
    }

    /// <summary>
    /// Verifies that Distinct with multiple columns uses a composite duplicate key.
    /// </summary>
    [Fact]
    public void Distinct_WithMultipleColumns_UsesCompositeKey()
    {
        // This test verifies that rows are duplicates only when every selected key column matches.
        var df = CreateDuplicateRowsDataFrame();

        var result = df.Distinct("Country", "City");

        Assert.Equal(new[] { 1, 2, 3 }, Ids(result));
        Assert.Equal(new[] { "Istanbul", "Ankara", "Berlin" }, Cities(result));
    }

    /// <summary>
    /// Verifies that Distinct keeps the first row encountered for each duplicate key.
    /// </summary>
    [Fact]
    public void Distinct_KeepsFirstSeenRow()
    {
        // This test verifies that the first source payload wins when later rows share the same key.
        var df = CreateDuplicateRowsDataFrame();

        var result = df.Distinct("Country");

        Assert.Equal(1, result["Id"].GetValue(0));
        Assert.Equal("first-tr", result["Note"].GetValue(0));
    }

    /// <summary>
    /// Verifies that Distinct removes later rows with duplicate keys.
    /// </summary>
    [Fact]
    public void Distinct_RemovesLaterDuplicateRows()
    {
        // This test verifies that subsequent rows sharing a selected key are excluded from the result.
        var df = CreateDuplicateRowsDataFrame();

        var result = df.Distinct("Country");

        Assert.DoesNotContain(2, Ids(result));
        Assert.DoesNotContain(4, Ids(result));
    }

    /// <summary>
    /// Verifies that Distinct preserves stable source row order for remaining rows.
    /// </summary>
    [Fact]
    public void Distinct_PreservesStableRowOrder()
    {
        // This test verifies that kept rows remain in their original relative order.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 10, 20, 30, 40, 50 },
            Key = new[] { "B", "A", "C", "A", "B" }
        });

        var result = df.Distinct("Key");

        Assert.Equal(new[] { 10, 20, 30 }, Ids(result));
    }

    /// <summary>
    /// Verifies that Distinct returns every source column, not only key columns.
    /// </summary>
    [Fact]
    public void Distinct_ReturnsAllSourceColumns()
    {
        // This test verifies that selected key columns do not project away non-key columns.
        var df = CreateDuplicateRowsDataFrame();

        var result = df.Distinct("Country");

        Assert.Equal(new[] { "Id", "Country", "City", "Note" }, ColumnNames(result));
        Assert.Equal("first-tr", result["Note"].GetValue(0));
    }

    /// <summary>
    /// Verifies that Distinct preserves schema metadata.
    /// </summary>
    [Fact]
    public void Distinct_PreservesSchema()
    {
        // This test verifies that duplicate removal changes rows without changing column metadata.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new string?[] { "Ali", "Ali", null },
            Score = new int?[] { 10, 10, null },
            Active = new[] { true, true, false }
        });

        var result = df.Distinct();

        Assert.Equal(typeof(string), result.Schema.GetColumn("Name").DataType);
        Assert.True(result.Schema.GetColumn("Name").IsNullable);
        Assert.Equal(typeof(int?), result.Schema.GetColumn("Score").DataType);
        Assert.True(result.Schema.GetColumn("Score").IsNullable);
        Assert.Equal(typeof(bool), result.Schema.GetColumn("Active").DataType);
        Assert.False(result.Schema.GetColumn("Active").IsNullable);
    }

    /// <summary>
    /// Verifies that Distinct preserves source column order.
    /// </summary>
    [Fact]
    public void Distinct_PreservesColumnOrder()
    {
        // This test verifies that result columns stay in source order even when key columns are requested differently.
        var df = CreateDuplicateRowsDataFrame();

        var result = df.Distinct("City", "Country");

        Assert.Equal(new[] { "Id", "Country", "City", "Note" }, ColumnNames(result));
        Assert.Equal(new[] { "Id", "Country", "City", "Note" }, SchemaNames(result));
    }

    /// <summary>
    /// Verifies that Distinct does not mutate the source DataFrame.
    /// </summary>
    [Fact]
    public void Distinct_DoesNotModifySourceDataFrame()
    {
        // This test verifies that duplicate removal returns a separate projection and leaves source rows intact.
        var df = CreateDuplicateRowsDataFrame();

        var result = df.Distinct("Country");

        Assert.NotSame(df, result);
        Assert.Equal(5, df.Rows.Count());
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, Ids(df));
        Assert.Equal(new[] { 1, 3 }, Ids(result));
    }

    /// <summary>
    /// Verifies that Distinct always returns a different DataFrame instance.
    /// </summary>
    [Fact]
    public void Distinct_ReturnsNewDataFrameInstance()
    {
        // This test verifies that even already-distinct input produces a new DataFrame.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1, 2 }, Name = new[] { "Ali", "Ayse" } });

        var result = df.Distinct();

        Assert.NotSame(df, result);
        Assert.Equal(new[] { 1, 2 }, Ids(result));
    }

    /// <summary>
    /// Verifies that Distinct on an empty DataFrame returns an empty DataFrame with the same schema.
    /// </summary>
    [Fact]
    public void Distinct_WithEmptyDataFrame_ReturnsEmptyDataFrameWithSameSchema()
    {
        // This test verifies that zero-row inputs keep their column contract.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = Array.Empty<int>(), Country = Array.Empty<string>() });

        var result = df.Distinct();

        Assert.NotSame(df, result);
        Assert.Equal(0, result.Rows.Count());
        Assert.Equal(ColumnNames(df), ColumnNames(result));
        Assert.Equal(SchemaNames(df), SchemaNames(result));
    }

    /// <summary>
    /// Verifies that Distinct on a single-row DataFrame returns that row.
    /// </summary>
    [Fact]
    public void Distinct_WithSingleRowDataFrame_ReturnsSameRow()
    {
        // This test verifies that a single row is kept and copied to the result.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 }, Country = new[] { "TR" } });

        var result = df.Distinct("Country");

        Assert.NotSame(df, result);
        Assert.Equal(1, result.Rows.Count());
        Assert.Equal(1, result["Id"].GetValue(0));
    }

    /// <summary>
    /// Verifies that Distinct treats null key values as equal.
    /// </summary>
    [Fact]
    public void Distinct_WithNullKeyValues_TreatsNullsAsEqual()
    {
        // This test verifies that null key values remove later null-key duplicates.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 1, 2, 3, 4 },
            Country = new string?[] { null, null, "TR", "TR" }
        });

        var result = df.Distinct("Country");

        Assert.Equal(new[] { 1, 3 }, Ids(result));
        Assert.Null(result["Country"].GetValue(0));
    }

    /// <summary>
    /// Verifies that Distinct compares nulls correctly inside composite keys.
    /// </summary>
    [Fact]
    public void Distinct_WithNullCompositeKeyValues_UsesFullCompositeKey()
    {
        // This test verifies that null in one key column still composes with the other key columns.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 1, 2, 3, 4, 5 },
            Country = new string?[] { null, null, null, "TR", "TR" },
            City = new string?[] { "Istanbul", "Ankara", "Istanbul", null, null }
        });

        var result = df.Distinct("Country", "City");

        Assert.Equal(new[] { 1, 2, 4 }, Ids(result));
    }

    /// <summary>
    /// Verifies that Distinct rejects an explicitly null params array.
    /// </summary>
    [Fact]
    public void Distinct_WithNullColumnNamesArray_ThrowsArgumentNullException()
    {
        // This test verifies that the params array itself must be supplied.
        var df = CreateDuplicateRowsDataFrame();
        string[] columnNames = null!;

        Assert.Throws<ArgumentNullException>(() => df.Distinct(columnNames));
    }

    /// <summary>
    /// Verifies that Distinct rejects invalid column names.
    /// </summary>
    /// <param name="columnName">The invalid key column name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Distinct_WithInvalidColumnName_Throws(string? columnName)
    {
        // This test verifies that every requested key column name must be meaningful.
        var df = CreateDuplicateRowsDataFrame();

        Assert.ThrowsAny<ArgumentException>(() => df.Distinct(columnName!));
    }

    /// <summary>
    /// Verifies that Distinct rejects missing key columns.
    /// </summary>
    [Fact]
    public void Distinct_WithMissingColumnName_ThrowsKeyNotFoundException()
    {
        // This test verifies that missing key columns fail fast instead of being ignored.
        var df = CreateDuplicateRowsDataFrame();

        var exception = Assert.Throws<KeyNotFoundException>(() => df.Distinct("MissingColumn"));

        Assert.Contains("MissingColumn", exception.Message);
    }

    /// <summary>
    /// Verifies that Distinct rejects duplicate key column names.
    /// </summary>
    [Fact]
    public void Distinct_WithDuplicateColumnNames_ThrowsArgumentException()
    {
        // This test verifies that duplicate key column names are rejected before row processing.
        var df = CreateDuplicateRowsDataFrame();

        Assert.Throws<ArgumentException>(() => df.Distinct("Country", "Country"));
    }

    /// <summary>
    /// Verifies that Distinct rejects duplicate key column names case-insensitively.
    /// </summary>
    [Fact]
    public void Distinct_WithDuplicateColumnNamesDifferentCasing_ThrowsArgumentException()
    {
        // This test verifies that duplicate key detection follows DataFrame lookup casing behavior.
        var df = CreateDuplicateRowsDataFrame();

        Assert.Throws<ArgumentException>(() => df.Distinct("Country", "country"));
    }

    /// <summary>
    /// Verifies that validation failures do not mutate the source DataFrame.
    /// </summary>
    [Fact]
    public void Distinct_WhenValidationFails_DoesNotModifySourceDataFrame()
    {
        // This test verifies that fail-fast validation happens before any result construction changes source state.
        var df = CreateDuplicateRowsDataFrame();

        Assert.Throws<KeyNotFoundException>(() => df.Distinct("MissingColumn"));

        Assert.Equal(5, df.Rows.Count());
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, Ids(df));
        Assert.Equal(new[] { "Id", "Country", "City", "Note" }, ColumnNames(df));
    }

    /// <summary>
    /// Verifies that Distinct uses default equality for primitive and string values.
    /// </summary>
    [Fact]
    public void Distinct_UsesDefaultEqualityForStringAndPrimitiveValues()
    {
        // This test verifies that strings and primitive values compare by their normal .NET equality behavior.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 1, 2, 3, 4 },
            Name = new[] { "Ali", "Ali", "ali", "Ayse" },
            Score = new[] { 10, 10, 10, 20 }
        });

        var result = df.Distinct("Name", "Score");

        Assert.Equal(new[] { 1, 3, 4 }, Ids(result));
        Assert.Equal(new[] { "Ali", "ali", "Ayse" }, Names(result));
    }

    private static global::Runiq.Data.DataFrame CreateDuplicateRowsDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 1, 2, 3, 4, 5 },
            Country = new[] { "TR", "TR", "DE", "DE", "TR" },
            City = new[] { "Istanbul", "Ankara", "Berlin", "Berlin", "Istanbul" },
            Note = new[] { "first-tr", "second-tr", "first-de", "second-de", "duplicate-full-row" }
        });
    }

    private static int[] Ids(global::Runiq.Data.DataFrame df)
    {
        return Enumerable.Range(0, df.Rows.Count())
            .Select(index => (int)df["Id"].GetValue(index)!)
            .ToArray();
    }

    private static string[] Names(global::Runiq.Data.DataFrame df)
    {
        return Enumerable.Range(0, df.Rows.Count())
            .Select(index => (string)df["Name"].GetValue(index)!)
            .ToArray();
    }

    private static string[] Countries(global::Runiq.Data.DataFrame df)
    {
        return Enumerable.Range(0, df.Rows.Count())
            .Select(index => (string)df["Country"].GetValue(index)!)
            .ToArray();
    }

    private static string[] Cities(global::Runiq.Data.DataFrame df)
    {
        return Enumerable.Range(0, df.Rows.Count())
            .Select(index => (string)df["City"].GetValue(index)!)
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
