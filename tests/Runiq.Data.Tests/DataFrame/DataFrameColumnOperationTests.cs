namespace Runiq.Data.Tests.DataFrame;

/// <summary>
/// Verifies add-column and explicit mutable column operations on DataFrame.
/// </summary>
public sealed class DataFrameColumnOperationTests
{
    /// <summary>
    /// Verifies that WithColumn returns a new DataFrame with the column appended.
    /// </summary>
    [Fact]
    public void WithColumn_AddsColumnAndReturnsNewDataFrame()
    {
        // Verifies immutable add-column behavior and the resulting shape.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Age = new[] { 30, 25 }
        });

        var enriched = df.WithColumn("IsAdult", new[] { true, true });

        Assert.NotSame(df, enriched);
        Assert.Equal(2, enriched.RowCount);
        Assert.Equal(3, enriched.ColumnCount);
        Assert.Equal(new[] { "Name", "Age", "IsAdult" }, ColumnNames(enriched));
        Assert.Equal(new[] { "Name", "Age", "IsAdult" }, SchemaNames(enriched));
        Assert.Equal(true, enriched["IsAdult"].GetValue(0));
        Assert.Equal(true, enriched["isadult"].GetValue(1));
    }

    /// <summary>
    /// Verifies that WithColumn leaves the source DataFrame unchanged.
    /// </summary>
    [Fact]
    public void WithColumn_DoesNotModifySourceDataFrame()
    {
        // Verifies that immutable add-column does not mutate the original instance.
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" }, Age = new[] { 30 } });

        var enriched = df.WithColumn("IsAdult", new[] { true });

        Assert.Equal(2, df.ColumnCount);
        Assert.False(df.HasColumn("IsAdult"));
        Assert.Equal(new[] { "Name", "Age" }, ColumnNames(df));
        Assert.Equal(3, enriched.ColumnCount);
    }

    /// <summary>
    /// Verifies that WithColumn preserves existing values and metadata.
    /// </summary>
    [Fact]
    public void WithColumn_PreservesExistingValuesDataTypesAndNullableMetadata()
    {
        // Verifies that existing column contracts survive the immutable add-column operation.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Score = new int?[] { 10, null }
        });

        var enriched = df.WithColumn("Active", new[] { true, false });

        Assert.Equal("Ali", enriched["Name"].GetValue(0));
        Assert.Null(enriched["Score"].GetValue(1));
        Assert.Equal(typeof(string), enriched["Name"].DataType);
        Assert.True(enriched["Name"].IsNullable);
        Assert.Equal(typeof(int?), enriched["Score"].DataType);
        Assert.True(enriched["Score"].IsNullable);
        Assert.Equal(typeof(string), enriched.Schema.GetColumn("Name").DataType);
        Assert.True(enriched.Schema.GetColumn("Score").IsNullable);
    }

    /// <summary>
    /// Verifies that WithColumn creates schema metadata for the new column.
    /// </summary>
    [Fact]
    public void WithColumn_CreatesSchemaEntryAndInfersNewColumnMetadata()
    {
        // Verifies that the appended column gets inferred type and nullability in the schema.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1, 2 } });

        var enriched = df.WithColumn("Nickname", new string?[] { "A", null });

        var column = enriched.Schema.GetColumn("nickname");
        Assert.Equal("Nickname", column.Name);
        Assert.Equal(typeof(string), column.DataType);
        Assert.True(column.IsNullable);
        Assert.Equal(1, column.Ordinal);
        Assert.Equal("A", enriched["Nickname"].GetValue(0));
        Assert.Null(enriched["Nickname"].GetValue(1));
    }

    /// <summary>
    /// Verifies that WithColumn snapshots source values.
    /// </summary>
    [Fact]
    public void WithColumn_SnapshotsSourceValues()
    {
        // Verifies that later mutations to the source collection do not affect the DataFrame.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1, 2 } });
        var values = new List<bool> { true, false };

        var enriched = df.WithColumn("Active", values);
        values[0] = false;
        values.Add(true);

        Assert.Equal(true, enriched["Active"].GetValue(0));
        Assert.Equal(2, enriched.RowCount);
    }

    /// <summary>
    /// Verifies that WithColumn supports adding an empty column to a zero-row DataFrame.
    /// </summary>
    [Fact]
    public void WithColumn_WithEmptyValuesOnEmptyRowDataFrame_Succeeds()
    {
        // Verifies that zero-row DataFrames can be enriched with another zero-row column.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = Array.Empty<int>() });

        var enriched = df.WithColumn("Name", Array.Empty<string>());

        Assert.Equal(0, enriched.RowCount);
        Assert.Equal(2, enriched.ColumnCount);
        Assert.Equal("Name", enriched.Columns[1].Name);
    }

    /// <summary>
    /// Verifies that WithColumn rejects non-empty values for a zero-row DataFrame.
    /// </summary>
    [Fact]
    public void WithColumn_WithNonEmptyValuesOnEmptyRowDataFrame_Throws()
    {
        // Verifies that added column values must match a zero-row DataFrame's row count.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = Array.Empty<int>() });

        Assert.Throws<ArgumentException>(() => df.WithColumn("Name", new[] { "Ali" }));
    }

    /// <summary>
    /// Verifies that WithColumn rejects invalid names.
    /// </summary>
    /// <param name="name">The invalid column name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithColumn_WithInvalidName_Throws(string? name)
    {
        // Verifies that immutable add-column requires a meaningful column name.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 } });

        Assert.ThrowsAny<ArgumentException>(() => df.WithColumn(name!, new[] { true }));
    }

    /// <summary>
    /// Verifies that WithColumn rejects invalid value collections and conflicts.
    /// </summary>
    [Fact]
    public void WithColumn_WithInvalidValuesOrDuplicateName_Throws()
    {
        // Verifies null values, string values, duplicate names, and count mismatches are rejected.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1, 2 } });

        Assert.Throws<ArgumentNullException>(() => df.WithColumn<int>("Score", null!));
        Assert.Throws<ArgumentException>(() => df.WithColumn("Code", "ab"));
        Assert.Throws<ArgumentException>(() => df.WithColumn("Id", new[] { 10, 20 }));
        Assert.Throws<ArgumentException>(() => df.WithColumn("id", new[] { 10, 20 }));
        Assert.Throws<ArgumentException>(() => df.WithColumn("Score", new[] { 10 }));
        Assert.Throws<ArgumentException>(() => df.WithColumn("Score", Array.Empty<int>()));
    }

    /// <summary>
    /// Verifies that AddColumn mutates the current DataFrame and appends the column.
    /// </summary>
    [Fact]
    public void AddColumn_MutatesCurrentDataFrameAndAppendsColumn()
    {
        // Verifies explicit mutable add-column behavior and resulting column order.
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali", "Ayse" }, Age = new[] { 30, 25 } });

        df.AddColumn("IsAdult", new[] { true, true });

        Assert.Equal(2, df.RowCount);
        Assert.Equal(3, df.ColumnCount);
        Assert.Equal(new[] { "Name", "Age", "IsAdult" }, ColumnNames(df));
        Assert.Equal(new[] { "Name", "Age", "IsAdult" }, SchemaNames(df));
        Assert.Equal(true, df["isadult"].GetValue(0));
    }

    /// <summary>
    /// Verifies that AddColumn preserves existing data and updates schema.
    /// </summary>
    [Fact]
    public void AddColumn_PreservesExistingDataAndUpdatesSchema()
    {
        // Verifies mutable add-column preserves existing values, types, and nullability metadata.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Score = new int?[] { 10, null }
        });

        df.AddColumn("Active", new[] { true, false });

        Assert.Equal("Ayse", df["Name"].GetValue(1));
        Assert.Null(df["Score"].GetValue(1));
        Assert.Equal(typeof(int?), df["Score"].DataType);
        Assert.True(df["Score"].IsNullable);
        Assert.Equal(typeof(bool), df.Schema.GetColumn("Active").DataType);
        Assert.False(df.Schema.GetColumn("Active").IsNullable);
    }

    /// <summary>
    /// Verifies that AddColumn snapshots source values.
    /// </summary>
    [Fact]
    public void AddColumn_SnapshotsSourceValues()
    {
        // Verifies that mutable add-column stores a snapshot instead of the source list.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1, 2 } });
        var values = new List<string> { "A", "B" };

        df.AddColumn("Code", values);
        values[0] = "Changed";
        values.Add("C");

        Assert.Equal("A", df["Code"].GetValue(0));
        Assert.Equal(2, df.RowCount);
    }

    /// <summary>
    /// Verifies that AddColumn supports adding an empty column to a zero-row DataFrame.
    /// </summary>
    [Fact]
    public void AddColumn_WithEmptyValuesOnEmptyRowDataFrame_Succeeds()
    {
        // Verifies that mutable add-column accepts a matching zero-length value collection.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = Array.Empty<int>() });

        df.AddColumn("Name", Array.Empty<string>());

        Assert.Equal(0, df.RowCount);
        Assert.Equal(2, df.ColumnCount);
        Assert.Equal("Name", df.Columns[1].Name);
    }

    /// <summary>
    /// Verifies that AddColumn rejects non-empty values for a zero-row DataFrame.
    /// </summary>
    [Fact]
    public void AddColumn_WithNonEmptyValuesOnEmptyRowDataFrame_Throws()
    {
        // Verifies that mutable add-column values must match a zero-row DataFrame's row count.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = Array.Empty<int>() });

        Assert.Throws<ArgumentException>(() => df.AddColumn("Name", new[] { "Ali" }));
    }

    /// <summary>
    /// Verifies that AddColumn rejects invalid inputs.
    /// </summary>
    /// <param name="name">The invalid column name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddColumn_WithInvalidName_Throws(string? name)
    {
        // Verifies that mutable add-column requires a meaningful column name.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 } });

        Assert.ThrowsAny<ArgumentException>(() => df.AddColumn(name!, new[] { true }));
    }

    /// <summary>
    /// Verifies that AddColumn rejects invalid value collections and conflicts.
    /// </summary>
    [Fact]
    public void AddColumn_WithInvalidValuesOrDuplicateName_Throws()
    {
        // Verifies null values, string values, duplicate names, and count mismatches are rejected.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1, 2 } });

        Assert.Throws<ArgumentNullException>(() => df.AddColumn<int>("Score", null!));
        Assert.Throws<ArgumentException>(() => df.AddColumn("Code", "ab"));
        Assert.Throws<ArgumentException>(() => df.AddColumn("Id", new[] { 10, 20 }));
        Assert.Throws<ArgumentException>(() => df.AddColumn("id", new[] { 10, 20 }));
        Assert.Throws<ArgumentException>(() => df.AddColumn("Score", new[] { 10 }));
        Assert.Throws<ArgumentException>(() => df.AddColumn("Score", Array.Empty<int>()));
    }

    /// <summary>
    /// Verifies that RemoveColumn mutates the current DataFrame.
    /// </summary>
    [Fact]
    public void RemoveColumn_MutatesCurrentDataFrame()
    {
        // Verifies explicit mutable remove-column behavior.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Age = new[] { 30, 25 },
            Active = new[] { true, false }
        });

        df.RemoveColumn("Age");

        Assert.Equal(2, df.RowCount);
        Assert.Equal(2, df.ColumnCount);
        Assert.False(df.HasColumn("Age"));
        Assert.Equal(new[] { "Name", "Active" }, ColumnNames(df));
        Assert.Equal(new[] { "Name", "Active" }, SchemaNames(df));
    }

    /// <summary>
    /// Verifies that RemoveColumn preserves remaining values and metadata.
    /// </summary>
    [Fact]
    public void RemoveColumn_PreservesRemainingValuesDataTypesAndNullableMetadata()
    {
        // Verifies that removing one column keeps the contracts of the remaining columns.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 1, 2 },
            DebugFlag = new[] { true, false },
            Score = new int?[] { 10, null }
        });

        df.RemoveColumn("debugflag");

        Assert.Equal(new[] { "Id", "Score" }, ColumnNames(df));
        Assert.Equal(1, df.Schema.GetColumn("Score").Ordinal);
        Assert.Equal(1, df["Id"].GetValue(0));
        Assert.Null(df["Score"].GetValue(1));
        Assert.Equal(typeof(int?), df["Score"].DataType);
        Assert.True(df["Score"].IsNullable);
        Assert.True(df.Schema.GetColumn("score").IsNullable);
    }

    /// <summary>
    /// Verifies that RemoveColumn rejects invalid or missing columns.
    /// </summary>
    /// <param name="name">The invalid column name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RemoveColumn_WithInvalidName_Throws(string? name)
    {
        // Verifies that mutable remove-column requires a meaningful column name.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 }, Name = new[] { "Ali" } });

        Assert.ThrowsAny<ArgumentException>(() => df.RemoveColumn(name!));
    }

    /// <summary>
    /// Verifies that RemoveColumn rejects missing columns and the last remaining column.
    /// </summary>
    [Fact]
    public void RemoveColumn_WithMissingColumnOrLastColumn_Throws()
    {
        // Verifies clear failures for missing columns and zero-column DataFrame attempts.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 }, Name = new[] { "Ali" } });
        var singleColumn = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 } });

        var missingException = Assert.Throws<KeyNotFoundException>(() => df.RemoveColumn("Missing"));
        var lastColumnException = Assert.Throws<ArgumentException>(() => singleColumn.RemoveColumn("Id"));

        Assert.Contains("Missing", missingException.Message);
        Assert.Contains("last remaining column", lastColumnException.Message);
    }

    /// <summary>
    /// Verifies that RenameColumnInPlace mutates the current DataFrame.
    /// </summary>
    [Fact]
    public void RenameColumnInPlace_MutatesCurrentDataFrame()
    {
        // Verifies explicit mutable rename behavior and canonical name update.
        var df = global::Runiq.Data.DataFrame.Create(new { cust_id = new[] { 1, 2 }, Name = new[] { "Ali", "Ayse" } });

        df.RenameColumnInPlace("cust_id", "CustomerId");

        Assert.Equal(2, df.RowCount);
        Assert.Equal(2, df.ColumnCount);
        Assert.Equal(new[] { "CustomerId", "Name" }, ColumnNames(df));
        Assert.Equal(new[] { "CustomerId", "Name" }, SchemaNames(df));
        Assert.Equal(2, df["customerid"].GetValue(1));
        Assert.False(df.HasColumn("cust_id"));
    }

    /// <summary>
    /// Verifies that RenameColumnInPlace preserves order, values, and metadata.
    /// </summary>
    [Fact]
    public void RenameColumnInPlace_PreservesOrderValuesDataTypesAndNullableMetadata()
    {
        // Verifies mutable rename keeps the renamed column in place with its original contract.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            score = new int?[] { 10, null },
            Active = new[] { true, false }
        });

        df.RenameColumnInPlace("SCORE", "Score");

        Assert.Equal(new[] { "Name", "Score", "Active" }, ColumnNames(df));
        Assert.Equal(10, df["score"].GetValue(0));
        Assert.Null(df["Score"].GetValue(1));
        Assert.Equal(typeof(int?), df["Score"].DataType);
        Assert.True(df["Score"].IsNullable);
        Assert.Equal(typeof(int?), df.Schema.GetColumn("Score").DataType);
        Assert.True(df.Schema.GetColumn("Score").IsNullable);
    }

    /// <summary>
    /// Verifies that RenameColumnInPlace allows changing only casing.
    /// </summary>
    [Fact]
    public void RenameColumnInPlace_WithSameColumnDifferentCasing_Succeeds()
    {
        // Verifies that casing-only renames are not treated as duplicate-name conflicts.
        var df = global::Runiq.Data.DataFrame.Create(new { age = new[] { 30 }, Name = new[] { "Ali" } });

        df.RenameColumnInPlace("age", "Age");

        Assert.True(df.HasColumn("age"));
        Assert.Equal("Age", df.GetColumn("age").Name);
        Assert.Equal("Age", df.Schema.GetColumn("AGE").Name);
    }

    /// <summary>
    /// Verifies that RenameColumnInPlace rejects invalid current names.
    /// </summary>
    /// <param name="currentName">The invalid current name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RenameColumnInPlace_WithInvalidCurrentName_Throws(string? currentName)
    {
        // Verifies that mutable rename requires a meaningful source name.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 }, Name = new[] { "Ali" } });

        Assert.ThrowsAny<ArgumentException>(() => df.RenameColumnInPlace(currentName!, "Years"));
    }

    /// <summary>
    /// Verifies that RenameColumnInPlace rejects invalid new names.
    /// </summary>
    /// <param name="newName">The invalid new name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RenameColumnInPlace_WithInvalidNewName_Throws(string? newName)
    {
        // Verifies that mutable rename requires a meaningful target name.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 }, Name = new[] { "Ali" } });

        Assert.ThrowsAny<ArgumentException>(() => df.RenameColumnInPlace("Age", newName!));
    }

    /// <summary>
    /// Verifies that RenameColumnInPlace rejects missing and conflicting names.
    /// </summary>
    [Fact]
    public void RenameColumnInPlace_WithMissingOrConflictingName_Throws()
    {
        // Verifies clear failures for missing sources and case-insensitive target conflicts.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 }, Name = new[] { "Ali" } });

        var missingException = Assert.Throws<KeyNotFoundException>(() => df.RenameColumnInPlace("Missing", "Years"));
        var conflictException = Assert.Throws<ArgumentException>(() => df.RenameColumnInPlace("Age", "Name"));
        var casingConflictException = Assert.Throws<ArgumentException>(() => df.RenameColumnInPlace("Age", "name"));

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
