namespace Runiq.Data.Tests.DataFrame;

/// <summary>
/// Verifies direct column mutation operations on DataFrame.
/// </summary>
public sealed class ColumnOperationTests
{
    /// <summary>
    /// Verifies that ColumnsAdd mutates the current DataFrame and appends the column.
    /// </summary>
    [Fact]
    public void ColumnsAdd_MutatesCurrentDataFrameAndAppendsColumn()
    {
        // Verifies direct mutable add-column behavior and resulting column order.
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali", "Ayse" }, Age = new[] { 30, 25 } });

        df.Columns.Add("IsAdult", new[] { true, true });

        Assert.Equal(2, df.Rows.Count());
        Assert.Equal(3, df.Columns.Count());
        Assert.Equal(new[] { "Name", "Age", "IsAdult" }, ColumnNames(df));
        Assert.Equal(new[] { "Name", "Age", "IsAdult" }, SchemaNames(df));
        Assert.Equal(true, df["isadult"].GetValue(0));
    }

    /// <summary>
    /// Verifies that ColumnsAdd preserves existing data and updates schema.
    /// </summary>
    [Fact]
    public void ColumnsAdd_PreservesExistingDataAndUpdatesSchema()
    {
        // Verifies mutable add-column preserves existing values, types, and nullability metadata.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Score = new int?[] { 10, null }
        });

        df.Columns.Add("Active", new[] { true, false });

        Assert.Equal("Ayse", df["Name"].GetValue(1));
        Assert.Null(df["Score"].GetValue(1));
        Assert.Equal(typeof(int?), df["Score"].DataType);
        Assert.True(df["Score"].IsNullable);
        Assert.Equal(typeof(bool), df.Schema.GetColumn("Active").DataType);
        Assert.False(df.Schema.GetColumn("Active").IsNullable);
    }

    /// <summary>
    /// Verifies that ColumnsAdd snapshots source values.
    /// </summary>
    [Fact]
    public void ColumnsAdd_SnapshotsSourceValues()
    {
        // Verifies that mutable add-column stores a snapshot instead of the source list.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1, 2 } });
        var values = new List<string> { "A", "B" };

        df.Columns.Add("Code", values);
        values[0] = "Changed";
        values.Add("C");

        Assert.Equal("A", df["Code"].GetValue(0));
        Assert.Equal(2, df.Rows.Count());
    }

    /// <summary>
    /// Verifies that ColumnsAdd supports adding an empty column to a zero-row DataFrame.
    /// </summary>
    [Fact]
    public void ColumnsAdd_WithEmptyValuesOnEmptyRowDataFrame_Succeeds()
    {
        // Verifies that mutable add-column accepts a matching zero-length value collection.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = Array.Empty<int>() });

        df.Columns.Add("Name", Array.Empty<string>());

        Assert.Equal(0, df.Rows.Count());
        Assert.Equal(2, df.Columns.Count());
        Assert.Equal("Name", df.Columns[1].Name);
    }

    /// <summary>
    /// Verifies that ColumnsAdd rejects non-empty values for a zero-row DataFrame.
    /// </summary>
    [Fact]
    public void ColumnsAdd_WithNonEmptyValuesOnEmptyRowDataFrame_Throws()
    {
        // Verifies that mutable add-column values must match a zero-row DataFrame's row count.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = Array.Empty<int>() });

        Assert.Throws<ArgumentException>(() => df.Columns.Add("Name", new[] { "Ali" }));
    }

    /// <summary>
    /// Verifies that ColumnsAdd rejects invalid inputs.
    /// </summary>
    /// <param name="name">The invalid column name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ColumnsAdd_WithInvalidName_Throws(string? name)
    {
        // Verifies that mutable add-column requires a meaningful column name.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 } });

        Assert.ThrowsAny<ArgumentException>(() => df.Columns.Add(name!, new[] { true }));
    }

    /// <summary>
    /// Verifies that ColumnsAdd rejects invalid value collections and conflicts.
    /// </summary>
    [Fact]
    public void ColumnsAdd_WithInvalidValuesOrDuplicateName_Throws()
    {
        // Verifies null values, string values, duplicate names, and count mismatches are rejected.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1, 2 } });

        Assert.Throws<ArgumentNullException>(() => df.Columns.Add<int>("Score", null!));
        Assert.Throws<ArgumentException>(() => df.Columns.Add("Code", "ab"));
        Assert.Throws<ArgumentException>(() => df.Columns.Add("Id", new[] { 10, 20 }));
        Assert.Throws<ArgumentException>(() => df.Columns.Add("id", new[] { 10, 20 }));
        Assert.Throws<ArgumentException>(() => df.Columns.Add("Score", new[] { 10 }));
        Assert.Throws<ArgumentException>(() => df.Columns.Add("Score", Array.Empty<int>()));
    }

    /// <summary>
    /// Verifies that ColumnsRemove mutates the current DataFrame.
    /// </summary>
    [Fact]
    public void ColumnsRemove_MutatesCurrentDataFrame()
    {
        // Verifies direct mutable remove-column behavior.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Age = new[] { 30, 25 },
            Active = new[] { true, false }
        });

        df.Columns.Remove("Age");

        Assert.Equal(2, df.Rows.Count());
        Assert.Equal(2, df.Columns.Count());
        Assert.False(df.HasColumn("Age"));
        Assert.Equal(new[] { "Name", "Active" }, ColumnNames(df));
        Assert.Equal(new[] { "Name", "Active" }, SchemaNames(df));
    }

    /// <summary>
    /// Verifies that ColumnsRemove preserves remaining values and metadata.
    /// </summary>
    [Fact]
    public void ColumnsRemove_PreservesRemainingValuesDataTypesAndNullableMetadata()
    {
        // Verifies that removing one column keeps the contracts of the remaining columns.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 1, 2 },
            DebugFlag = new[] { true, false },
            Score = new int?[] { 10, null }
        });

        df.Columns.Remove("debugflag");

        Assert.Equal(new[] { "Id", "Score" }, ColumnNames(df));
        Assert.Equal(1, df.Schema.GetColumn("Score").Ordinal);
        Assert.Equal(1, df["Id"].GetValue(0));
        Assert.Null(df["Score"].GetValue(1));
        Assert.Equal(typeof(int?), df["Score"].DataType);
        Assert.True(df["Score"].IsNullable);
        Assert.True(df.Schema.GetColumn("score").IsNullable);
    }

    /// <summary>
    /// Verifies that ColumnsRemove rejects invalid or missing columns.
    /// </summary>
    /// <param name="name">The invalid column name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ColumnsRemove_WithInvalidName_Throws(string? name)
    {
        // Verifies that mutable remove-column requires a meaningful column name.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 }, Name = new[] { "Ali" } });

        Assert.ThrowsAny<ArgumentException>(() => df.Columns.Remove(name!));
    }

    /// <summary>
    /// Verifies that ColumnsRemove rejects missing columns and the last remaining column.
    /// </summary>
    [Fact]
    public void ColumnsRemove_WithMissingColumnOrLastColumn_Throws()
    {
        // Verifies clear failures for missing columns and zero-column DataFrame attempts.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 }, Name = new[] { "Ali" } });
        var singleColumn = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 } });

        var missingException = Assert.Throws<KeyNotFoundException>(() => df.Columns.Remove("Missing"));
        var lastColumnException = Assert.Throws<ArgumentException>(() => singleColumn.Columns.Remove("Id"));

        Assert.Contains("Missing", missingException.Message);
        Assert.Contains("last remaining column", lastColumnException.Message);
    }

    /// <summary>
    /// Verifies that ColumnsRename mutates the current DataFrame.
    /// </summary>
    [Fact]
    public void ColumnsRename_MutatesCurrentDataFrame()
    {
        // Verifies direct mutable rename behavior and canonical name update.
        var df = global::Runiq.Data.DataFrame.Create(new { cust_id = new[] { 1, 2 }, Name = new[] { "Ali", "Ayse" } });

        df.Columns.Rename("cust_id", "CustomerId");

        Assert.Equal(2, df.Rows.Count());
        Assert.Equal(2, df.Columns.Count());
        Assert.Equal(new[] { "CustomerId", "Name" }, ColumnNames(df));
        Assert.Equal(new[] { "CustomerId", "Name" }, SchemaNames(df));
        Assert.Equal(2, df["customerid"].GetValue(1));
        Assert.False(df.HasColumn("cust_id"));
    }

    /// <summary>
    /// Verifies that ColumnsRename preserves order, values, and metadata.
    /// </summary>
    [Fact]
    public void ColumnsRename_PreservesOrderValuesDataTypesAndNullableMetadata()
    {
        // Verifies mutable rename keeps the renamed column in place with its original contract.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            score = new int?[] { 10, null },
            Active = new[] { true, false }
        });

        df.Columns.Rename("SCORE", "Score");

        Assert.Equal(new[] { "Name", "Score", "Active" }, ColumnNames(df));
        Assert.Equal(10, df["score"].GetValue(0));
        Assert.Null(df["Score"].GetValue(1));
        Assert.Equal(typeof(int?), df["Score"].DataType);
        Assert.True(df["Score"].IsNullable);
        Assert.Equal(typeof(int?), df.Schema.GetColumn("Score").DataType);
        Assert.True(df.Schema.GetColumn("Score").IsNullable);
    }

    /// <summary>
    /// Verifies that ColumnsRename allows changing only casing.
    /// </summary>
    [Fact]
    public void ColumnsRename_WithSameColumnDifferentCasing_Succeeds()
    {
        // Verifies that casing-only renames are not treated as duplicate-name conflicts.
        var df = global::Runiq.Data.DataFrame.Create(new { age = new[] { 30 }, Name = new[] { "Ali" } });

        df.Columns.Rename("age", "Age");

        Assert.True(df.HasColumn("age"));
        Assert.Equal("Age", df.GetColumn("age").Name);
        Assert.Equal("Age", df.Schema.GetColumn("AGE").Name);
    }

    /// <summary>
    /// Verifies that ColumnsRename rejects invalid current names.
    /// </summary>
    /// <param name="currentName">The invalid current name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ColumnsRename_WithInvalidCurrentName_Throws(string? currentName)
    {
        // Verifies that mutable rename requires a meaningful source name.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 }, Name = new[] { "Ali" } });

        Assert.ThrowsAny<ArgumentException>(() => df.Columns.Rename(currentName!, "Years"));
    }

    /// <summary>
    /// Verifies that ColumnsRename rejects invalid new names.
    /// </summary>
    /// <param name="newName">The invalid new name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ColumnsRename_WithInvalidNewName_Throws(string? newName)
    {
        // Verifies that mutable rename requires a meaningful target name.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 }, Name = new[] { "Ali" } });

        Assert.ThrowsAny<ArgumentException>(() => df.Columns.Rename("Age", newName!));
    }

    /// <summary>
    /// Verifies that ColumnsRename rejects missing and conflicting names.
    /// </summary>
    [Fact]
    public void ColumnsRename_WithMissingOrConflictingName_Throws()
    {
        // Verifies clear failures for missing sources and case-insensitive target conflicts.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 }, Name = new[] { "Ali" } });

        var missingException = Assert.Throws<KeyNotFoundException>(() => df.Columns.Rename("Missing", "Years"));
        var conflictException = Assert.Throws<ArgumentException>(() => df.Columns.Rename("Age", "Name"));
        var casingConflictException = Assert.Throws<ArgumentException>(() => df.Columns.Rename("Age", "name"));

        Assert.Contains("Missing", missingException.Message);
        Assert.Contains("Name", conflictException.Message);
        Assert.Contains("name", casingConflictException.Message);
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

