using System.Reflection;
using Runiq.Data.Schema;
using Runiq.Data.Series;

namespace Runiq.Data.Tests.DataFrame;

/// <summary>
/// Verifies the behavior of the consumer-facing DataFrame object model.
/// </summary>
public sealed class DataFrameTests
{
    /// <summary>
    /// Verifies that a DataFrame can be created from two array columns.
    /// </summary>
    [Fact]
    public void Create_WithTwoArrayColumns_Succeeds()
    {
        // Verifies that anonymous object properties become DataFrame columns.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Age = new[] { 30, 25 }
        });

        Assert.NotNull(df);
    }

    /// <summary>
    /// Verifies that different supported column types can be created together.
    /// </summary>
    [Fact]
    public void Create_WithThreeDifferentTypedColumns_Succeeds()
    {
        // Verifies that supported CLR array types are inferred per column.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Age = new[] { 30, 25 },
            Salary = new[] { 120000m, 95000m }
        });

        Assert.Equal(typeof(string), df["Name"].DataType);
        Assert.Equal(typeof(int), df["Age"].DataType);
        Assert.Equal(typeof(decimal), df["Salary"].DataType);
    }

    /// <summary>
    /// Verifies that RowCount reports the shared column length.
    /// </summary>
    [Fact]
    public void RowCount_ReturnsNumberOfRows()
    {
        // Verifies that RowCount is derived from the equal-sized columns.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30, 25 } });

        Assert.Equal(2, df.RowCount);
    }

    /// <summary>
    /// Verifies that ColumnCount reports the number of columns.
    /// </summary>
    [Fact]
    public void ColumnCount_ReturnsNumberOfColumns()
    {
        // Verifies that ColumnCount reflects the supplied property count.
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" }, Age = new[] { 30 } });

        Assert.Equal(2, df.ColumnCount);
    }

    /// <summary>
    /// Verifies that schema metadata is inferred automatically.
    /// </summary>
    [Fact]
    public void Create_BuildsSchemaAutomatically()
    {
        // Verifies that DataFrame creation produces a schema from supplied columns.
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" }, Age = new[] { 30 } });

        Assert.NotNull(df.Schema);
        Assert.Equal(2, df.Schema.Count);
    }

    /// <summary>
    /// Verifies that schema column names match property names.
    /// </summary>
    [Fact]
    public void SchemaColumnNames_MatchObjectPropertyNames()
    {
        // Verifies that property names become schema column names.
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" }, Age = new[] { 30 } });

        Assert.Collection(
            df.Schema.Columns,
            column => Assert.Equal("Name", column.Name),
            column => Assert.Equal("Age", column.Name));
    }

    /// <summary>
    /// Verifies that schema column data types match element types.
    /// </summary>
    [Fact]
    public void SchemaColumnDataTypes_MatchPropertyElementTypes()
    {
        // Verifies that schema data types are inferred from enumerable element types.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Active = new[] { true },
            CreatedAt = new[] { DateTime.UnixEpoch }
        });

        Assert.Equal(typeof(bool), df.Schema.GetColumn("Active").DataType);
        Assert.Equal(typeof(DateTime), df.Schema.GetColumn("CreatedAt").DataType);
    }

    /// <summary>
    /// Verifies that column order follows reflection property order.
    /// </summary>
    [Fact]
    public void Create_PreservesColumnOrder()
    {
        // Verifies that DataFrame column order matches the supplied object's reflected property order.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            First = new[] { 1 },
            Second = new[] { 2 },
            Third = new[] { 3 }
        });

        Assert.Collection(
            df.Columns,
            column => Assert.Equal("First", column.Name),
            column => Assert.Equal("Second", column.Name),
            column => Assert.Equal("Third", column.Name));
    }

    /// <summary>
    /// Verifies that the public columns collection cannot be modified.
    /// </summary>
    [Fact]
    public void Columns_CannotBeModifiedFromPublicApi()
    {
        // Verifies that consumers receive a read-only column collection.
        var df = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 } });

        Assert.False(df.Columns is ICollection<ISeries> { IsReadOnly: false });
    }

    /// <summary>
    /// Verifies that array source mutations do not alter the DataFrame.
    /// </summary>
    [Fact]
    public void Create_SnapshotsSourceArrays()
    {
        // Verifies that DataFrame values are detached from source arrays.
        var ages = new[] { 30, 25 };
        var df = global::Runiq.Data.DataFrame.Create(new { Age = ages });

        ages[0] = 99;

        Assert.Equal(30, df["Age"].GetValue(0));
    }

    /// <summary>
    /// Verifies that list source mutations do not alter the DataFrame.
    /// </summary>
    [Fact]
    public void Create_SnapshotsSourceLists()
    {
        // Verifies that DataFrame values are detached from source lists.
        var names = new List<string> { "Ali", "Ayse" };
        var df = global::Runiq.Data.DataFrame.Create(new { Name = names });

        names[1] = "Fatma";
        names.Add("Can");

        Assert.Equal("Ayse", df["Name"].GetValue(1));
        Assert.Equal(2, df.RowCount);
    }

    /// <summary>
    /// Verifies that indexer lookup returns an existing column.
    /// </summary>
    [Fact]
    public void Indexer_WithExistingColumn_ReturnsColumn()
    {
        // Verifies that square-bracket column access returns the requested column.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 } });

        Assert.Equal("Age", df["Age"].Name);
    }

    /// <summary>
    /// Verifies that GetColumn lookup returns an existing column.
    /// </summary>
    [Fact]
    public void GetColumn_WithExistingColumn_ReturnsColumn()
    {
        // Verifies that method-based column access returns the requested column.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 } });

        Assert.Equal("Age", df.GetColumn("Age").Name);
    }

    /// <summary>
    /// Verifies that HasColumn returns true for existing columns.
    /// </summary>
    [Fact]
    public void HasColumn_WithExistingColumn_ReturnsTrue()
    {
        // Verifies that name existence checks succeed for present columns.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 } });

        Assert.True(df.HasColumn("Age"));
    }

    /// <summary>
    /// Verifies that HasColumn returns false for missing columns.
    /// </summary>
    [Fact]
    public void HasColumn_WithMissingColumn_ReturnsFalse()
    {
        // Verifies that name existence checks fail for absent columns.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 } });

        Assert.False(df.HasColumn("Missing"));
    }

    /// <summary>
    /// Verifies that column lookup is case-insensitive.
    /// </summary>
    [Fact]
    public void ColumnLookup_WithDifferentCasing_ReturnsColumn()
    {
        // Verifies that user-friendly column lookup ignores casing.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 } });

        Assert.Equal("Age", df["age"].Name);
        Assert.True(df.HasColumn("AGE"));
    }

    /// <summary>
    /// Verifies that missing indexer lookup fails clearly.
    /// </summary>
    [Fact]
    public void Indexer_WithMissingColumn_ThrowsKeyNotFoundException()
    {
        // Verifies that missing square-bracket lookup reports the missing column.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 } });

        var exception = Assert.Throws<KeyNotFoundException>(() => df["Missing"]);
        Assert.Contains("Missing", exception.Message);
    }

    /// <summary>
    /// Verifies that missing GetColumn lookup fails clearly.
    /// </summary>
    [Fact]
    public void GetColumn_WithMissingColumn_ThrowsKeyNotFoundException()
    {
        // Verifies that missing method lookup reports the missing column.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 } });

        var exception = Assert.Throws<KeyNotFoundException>(() => df.GetColumn("Missing"));
        Assert.Contains("Missing", exception.Message);
    }

    /// <summary>
    /// Verifies retrieved column metadata and values.
    /// </summary>
    [Fact]
    public void RetrievedColumn_ExposesMetadataAndValues()
    {
        // Verifies that public column access exposes expected metadata and row values.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30, 25 } });

        var age = df["Age"];

        Assert.Equal("Age", age.Name);
        Assert.Equal(typeof(int), age.DataType);
        Assert.Equal(2, age.Count);
        Assert.Equal(25, age.GetValue(1));
    }

    /// <summary>
    /// Verifies that null columns object is rejected.
    /// </summary>
    [Fact]
    public void Create_WithNullColumnsObject_ThrowsArgumentNullException()
    {
        // Verifies that a DataFrame requires an actual columns object.
        Assert.Throws<ArgumentNullException>(() => global::Runiq.Data.DataFrame.Create(null!));
    }

    /// <summary>
    /// Verifies that an object with no properties is rejected.
    /// </summary>
    [Fact]
    public void Create_WithNoPublicReadableProperties_ThrowsArgumentException()
    {
        // Verifies that DataFrame creation needs at least one public readable column property.
        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.Create(new object()));
    }

    /// <summary>
    /// Verifies that null property values are rejected.
    /// </summary>
    [Fact]
    public void Create_WithNullPropertyValue_ThrowsArgumentException()
    {
        // Verifies that each column property must contain a non-null value collection.
        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.Create(new { Name = (string[]?)null }));
    }

    /// <summary>
    /// Verifies that string property values are rejected as columns.
    /// </summary>
    [Fact]
    public void Create_WithStringPropertyValue_ThrowsArgumentException()
    {
        // Verifies that strings are not treated as enumerable column values.
        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.Create(new { Name = "Ali" }));
    }

    /// <summary>
    /// Verifies that non-enumerable property values are rejected.
    /// </summary>
    [Fact]
    public void Create_WithNonEnumerablePropertyValue_ThrowsArgumentException()
    {
        // Verifies that scalar values cannot be used as DataFrame columns.
        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.Create(new { Age = 30 }));
    }

    /// <summary>
    /// Verifies that unequal column row counts are rejected.
    /// </summary>
    [Fact]
    public void Create_WithDifferentColumnRowCounts_ThrowsArgumentException()
    {
        // Verifies that every DataFrame column must have the same number of rows.
        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Age = new[] { 30 }
        }));
    }

    /// <summary>
    /// Verifies that all-empty columns are allowed.
    /// </summary>
    [Fact]
    public void Create_WithAllEmptyColumns_Succeeds()
    {
        // Verifies that a zero-row DataFrame is valid when every column is empty.
        var df = global::Runiq.Data.DataFrame.Create(new { Name = Array.Empty<string>(), Age = Array.Empty<int>() });

        Assert.Equal(0, df.RowCount);
        Assert.Equal(2, df.ColumnCount);
    }

    /// <summary>
    /// Verifies that mixed empty and non-empty columns are rejected.
    /// </summary>
    [Fact]
    public void Create_WithMixedEmptyAndNonEmptyColumns_ThrowsArgumentException()
    {
        // Verifies that empty columns cannot be mixed with populated columns.
        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.Create(new
        {
            Name = Array.Empty<string>(),
            Age = new[] { 30 }
        }));
    }

    /// <summary>
    /// Verifies that duplicate property names are rejected case-insensitively.
    /// </summary>
    [Fact]
    public void Create_WithDuplicatePropertyNamesDifferentCasing_ThrowsArgumentException()
    {
        // Verifies that unusual reflected duplicate names cannot produce ambiguous columns.
        var input = new DuplicateColumnInput();

        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.Create(input));
    }

    /// <summary>
    /// Verifies that the public DataFrame API exposes no property setters.
    /// </summary>
    [Fact]
    public void PublicApi_DoesNotExposePropertyMutation()
    {
        // Verifies that consumers cannot replace DataFrame state after creation.
        var writableProperties = typeof(global::Runiq.Data.DataFrame)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.SetMethod is not null)
            .Select(static property => property.Name);

        Assert.Empty(writableProperties);
    }

    /// <summary>
    /// Verifies that schema-first DataFrame creation succeeds for matching input.
    /// </summary>
    [Fact]
    public void Create_WithMatchingSchema_Succeeds()
    {
        // Verifies that supplied schema can validate compatible columns.
        var schema = DataFrameSchema.Create(
            ColumnSchema.Create("Name", typeof(string), nullable: true, ordinal: 0),
            ColumnSchema.Create("Age", typeof(int), nullable: false, ordinal: 1));

        var df = global::Runiq.Data.DataFrame.Create(schema, new { Name = new[] { "Ali" }, Age = new[] { 30 } });

        Assert.Equal(schema, df.Schema);
    }

    /// <summary>
    /// Verifies that schema-first creation preserves schema order.
    /// </summary>
    [Fact]
    public void Create_WithSchema_PreservesSchemaOrder()
    {
        // Verifies that schema order wins over object property order.
        var schema = DataFrameSchema.Create(
            ColumnSchema.Create("Age", typeof(int), nullable: false, ordinal: 0),
            ColumnSchema.Create("Name", typeof(string), nullable: true, ordinal: 1));

        var df = global::Runiq.Data.DataFrame.Create(schema, new { Name = new[] { "Ali" }, Age = new[] { 30 } });

        Assert.Collection(
            df.Columns,
            column => Assert.Equal("Age", column.Name),
            column => Assert.Equal("Name", column.Name));
    }

    /// <summary>
    /// Verifies that missing schema columns are rejected.
    /// </summary>
    [Fact]
    public void Create_WithSchemaMissingObjectColumn_ThrowsArgumentException()
    {
        // Verifies that every expected schema column must be supplied by the object.
        var schema = DataFrameSchema.Create(
            ColumnSchema.Create("Name", typeof(string), nullable: true, ordinal: 0),
            ColumnSchema.Create("Age", typeof(int), nullable: false, ordinal: 1));

        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.Create(schema, new { Name = new[] { "Ali" } }));
    }

    /// <summary>
    /// Verifies that extra object columns are rejected.
    /// </summary>
    [Fact]
    public void Create_WithSchemaExtraObjectColumn_ThrowsArgumentException()
    {
        // Verifies that schema-first creation requires an exact column set.
        var schema = DataFrameSchema.Create(ColumnSchema.Create("Name", typeof(string), nullable: true, ordinal: 0));

        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.Create(schema, new
        {
            Name = new[] { "Ali" },
            Age = new[] { 30 }
        }));
    }

    /// <summary>
    /// Verifies that schema data type mismatches are rejected.
    /// </summary>
    [Fact]
    public void Create_WithSchemaDataTypeMismatch_ThrowsArgumentException()
    {
        // Verifies that inferred column element types must match schema types.
        var schema = DataFrameSchema.Create(ColumnSchema.Create("Age", typeof(long), nullable: false, ordinal: 0));

        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.Create(schema, new { Age = new[] { 30 } }));
    }

    /// <summary>
    /// Verifies that schema-first matching supports different casing.
    /// </summary>
    [Fact]
    public void Create_WithSchemaDifferentNameCasing_MatchesColumn()
    {
        // Verifies that schema-first creation matches names case-insensitively and keeps schema casing.
        var schema = DataFrameSchema.Create(ColumnSchema.Create("Age", typeof(int), nullable: false, ordinal: 0));

        var df = global::Runiq.Data.DataFrame.Create(schema, new { age = new[] { 30 } });

        Assert.Collection(df.Columns, column => Assert.Equal("Age", column.Name));
        Assert.Equal("Age", df["Age"].Name);
        Assert.Equal("Age", df["age"].Name);
        Assert.True(df.HasColumn("AGE"));
    }

    /// <summary>
    /// Verifies that schema-first creation still validates row counts.
    /// </summary>
    [Fact]
    public void Create_WithSchemaDifferentRowCounts_ThrowsArgumentException()
    {
        // Verifies that schema validation does not bypass DataFrame row-count invariants.
        var schema = DataFrameSchema.Create(
            ColumnSchema.Create("Name", typeof(string), nullable: true, ordinal: 0),
            ColumnSchema.Create("Age", typeof(int), nullable: false, ordinal: 1));

        Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.Create(schema, new
        {
            Name = new[] { "Ali", "Ayse" },
            Age = new[] { 30 }
        }));
    }

    private class DuplicateColumnBase
    {
        public int[] Id { get; } = [1];
    }

    private sealed class DuplicateColumnInput : DuplicateColumnBase
    {
        public int[] ID { get; } = [1];
    }
}
