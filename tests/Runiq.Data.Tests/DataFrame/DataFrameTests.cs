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

    /// <summary>
    /// Verifies that selecting one column returns a new one-column DataFrame.
    /// </summary>
    [Fact]
    public void Select_WithOneColumn_ReturnsOneColumnDataFrame()
    {
        // Verifies that projection keeps only the requested column.
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali", "Ayse" }, Age = new[] { 30, 25 } });

        var selected = df.Select("Name");

        Assert.NotSame(df, selected);
        Assert.Equal(2, selected.RowCount);
        Assert.Equal(1, selected.ColumnCount);
        Assert.Collection(selected.Columns, column => Assert.Equal("Name", column.Name));
        Assert.Collection(selected.Schema.Columns, column => Assert.Equal("Name", column.Name));
    }

    /// <summary>
    /// Verifies that selecting multiple columns follows the requested order.
    /// </summary>
    [Fact]
    public void Select_WithMultipleColumns_ReturnsColumnsInRequestedOrder()
    {
        // Verifies that projection order follows the caller instead of the source DataFrame.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Age = new[] { 30, 25 },
            Active = new[] { true, false }
        });

        var selected = df.Select("Active", "Name");

        Assert.Equal(2, selected.ColumnCount);
        Assert.Collection(
            selected.Columns,
            column => Assert.Equal("Active", column.Name),
            column => Assert.Equal("Name", column.Name));
        Assert.Collection(
            selected.Schema.Columns,
            column =>
            {
                Assert.Equal("Active", column.Name);
                Assert.Equal(0, column.Ordinal);
            },
            column =>
            {
                Assert.Equal("Name", column.Name);
                Assert.Equal(1, column.Ordinal);
            });
    }

    /// <summary>
    /// Verifies that selected column values and metadata are preserved.
    /// </summary>
    [Fact]
    public void Select_PreservesValuesDataTypesAndNullability()
    {
        // Verifies that projection copies the source column contract into the result.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Score = new int?[] { 10, null },
            Age = new[] { 30, 25 }
        });

        var selected = df.Select("Score", "Age");

        Assert.Equal(10, selected["Score"].GetValue(0));
        Assert.Null(selected["Score"].GetValue(1));
        Assert.Equal(25, selected["Age"].GetValue(1));
        Assert.Equal(typeof(int?), selected["Score"].DataType);
        Assert.True(selected["Score"].IsNullable);
        Assert.Equal(typeof(int), selected["Age"].DataType);
        Assert.False(selected["Age"].IsNullable);
        Assert.Equal(typeof(int?), selected.Schema.GetColumn("Score").DataType);
        Assert.True(selected.Schema.GetColumn("Score").IsNullable);
        Assert.Equal(typeof(int), selected.Schema.GetColumn("Age").DataType);
        Assert.False(selected.Schema.GetColumn("Age").IsNullable);
    }

    /// <summary>
    /// Verifies that selecting columns does not modify the original DataFrame.
    /// </summary>
    [Fact]
    public void Select_DoesNotModifyOriginalDataFrame()
    {
        // Verifies that projection returns a separate DataFrame and leaves the source shape intact.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali" },
            Age = new[] { 30 },
            Active = new[] { true }
        });

        var selected = df.Select("Age");

        Assert.NotSame(df, selected);
        Assert.Equal(3, df.ColumnCount);
        Assert.Equal(1, selected.ColumnCount);
        Assert.Collection(
            df.Columns,
            column => Assert.Equal("Name", column.Name),
            column => Assert.Equal("Age", column.Name),
            column => Assert.Equal("Active", column.Name));
    }

    /// <summary>
    /// Verifies that selecting with different casing succeeds and keeps source casing.
    /// </summary>
    [Fact]
    public void Select_WithDifferentCasing_ReturnsCanonicalColumnNames()
    {
        // Verifies that projection lookup is case-insensitive but result names remain canonical.
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" }, Age = new[] { 30 } });

        var selected = df.Select("name", "AGE");

        Assert.Collection(
            selected.Columns,
            column => Assert.Equal("Name", column.Name),
            column => Assert.Equal("Age", column.Name));
        Assert.Collection(
            selected.Schema.Columns,
            column => Assert.Equal("Name", column.Name),
            column => Assert.Equal("Age", column.Name));
        Assert.Equal("Age", selected["age"].Name);
    }

    /// <summary>
    /// Verifies that null column name arrays are rejected.
    /// </summary>
    [Fact]
    public void Select_WithNullColumnNamesArray_ThrowsArgumentNullException()
    {
        // Verifies that the params array itself must be supplied.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 } });
        string[] columnNames = null!;

        Assert.Throws<ArgumentNullException>(() => df.Select(columnNames));
    }

    /// <summary>
    /// Verifies that empty column name arrays are rejected.
    /// </summary>
    [Fact]
    public void Select_WithEmptyColumnNamesArray_ThrowsArgumentException()
    {
        // Verifies that projection cannot produce a zero-column DataFrame.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 } });

        Assert.Throws<ArgumentException>(() => df.Select());
    }

    /// <summary>
    /// Verifies that invalid column names are rejected.
    /// </summary>
    /// <param name="columnName">The invalid requested column name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Select_WithInvalidColumnName_Throws(string? columnName)
    {
        // Verifies that each requested column name must be meaningful.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 } });

        Assert.ThrowsAny<ArgumentException>(() => df.Select(columnName!));
    }

    /// <summary>
    /// Verifies that missing column names are rejected clearly.
    /// </summary>
    [Fact]
    public void Select_WithMissingColumn_ThrowsClearException()
    {
        // Verifies that projection reports the missing requested column.
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" }, Age = new[] { 30 } });

        var exception = Assert.Throws<KeyNotFoundException>(() => df.Select("Name", "MissingColumn"));

        Assert.Contains("MissingColumn", exception.Message);
        Assert.Contains("exists", exception.Message);
    }

    /// <summary>
    /// Verifies that duplicate selected column names are rejected.
    /// </summary>
    [Fact]
    public void Select_WithDuplicateColumnNames_ThrowsArgumentException()
    {
        // Verifies that projection does not allow duplicate result columns.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 } });

        Assert.Throws<ArgumentException>(() => df.Select("Age", "Age"));
    }

    /// <summary>
    /// Verifies that duplicate selected column names are rejected case-insensitively.
    /// </summary>
    [Fact]
    public void Select_WithDuplicateColumnNamesDifferentCasing_ThrowsArgumentException()
    {
        // Verifies that projection duplicate detection matches DataFrame lookup semantics.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 } });

        Assert.Throws<ArgumentException>(() => df.Select("Age", "age"));
    }

    /// <summary>
    /// Verifies that selected DataFrames remain immutable through the public API.
    /// </summary>
    [Fact]
    public void Select_ResultDoesNotExposePublicMutation()
    {
        // Verifies that projection results expose read-only state like created DataFrames.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 }, Name = new[] { "Ali" } });

        var selected = df.Select("Age");
        var writableProperties = selected.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.SetMethod is not null)
            .Select(static property => property.Name);

        Assert.False(selected.Columns is ICollection<ISeries> { IsReadOnly: false });
        Assert.Empty(writableProperties);
    }

    /// <summary>
    /// Verifies that dropping one column removes only that column.
    /// </summary>
    [Fact]
    public void Drop_WithOneColumn_ReturnsDataFrameWithoutColumn()
    {
        // Verifies that drop removes the requested column and keeps the remaining source order.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Age = new[] { 30, 25 },
            Active = new[] { true, false }
        });

        var dropped = df.Drop("Age");

        Assert.NotSame(df, dropped);
        Assert.Equal(2, dropped.RowCount);
        Assert.Equal(2, dropped.ColumnCount);
        Assert.False(dropped.HasColumn("Age"));
        Assert.Collection(
            dropped.Columns,
            column => Assert.Equal("Name", column.Name),
            column => Assert.Equal("Active", column.Name));
    }

    /// <summary>
    /// Verifies that dropping multiple columns removes all requested columns.
    /// </summary>
    [Fact]
    public void Drop_WithMultipleColumns_ReturnsDataFrameWithoutColumns()
    {
        // Verifies that multiple requested drops are applied in one projection.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 1, 2 },
            DebugFlag = new[] { true, false },
            Name = new[] { "Ali", "Ayse" },
            InternalNote = new string?[] { null, "review" }
        });

        var dropped = df.Drop("DebugFlag", "InternalNote");

        Assert.Equal(2, dropped.ColumnCount);
        Assert.False(dropped.HasColumn("DebugFlag"));
        Assert.False(dropped.HasColumn("InternalNote"));
        Assert.Collection(
            dropped.Columns,
            column => Assert.Equal("Id", column.Name),
            column => Assert.Equal("Name", column.Name));
    }

    /// <summary>
    /// Verifies that dropped DataFrames preserve remaining values and metadata.
    /// </summary>
    [Fact]
    public void Drop_PreservesRemainingValuesDataTypesAndNullability()
    {
        // Verifies that drop keeps the column contracts and values for columns that remain.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Score = new int?[] { 10, null },
            Age = new[] { 30, 25 }
        });

        var dropped = df.Drop("Name");

        Assert.Equal(10, dropped["Score"].GetValue(0));
        Assert.Null(dropped["Score"].GetValue(1));
        Assert.Equal(25, dropped["Age"].GetValue(1));
        Assert.Equal(typeof(int?), dropped["Score"].DataType);
        Assert.True(dropped["Score"].IsNullable);
        Assert.Equal(typeof(int), dropped["Age"].DataType);
        Assert.False(dropped["Age"].IsNullable);
        Assert.Equal(typeof(int?), dropped.Schema.GetColumn("Score").DataType);
        Assert.True(dropped.Schema.GetColumn("Score").IsNullable);
        Assert.Equal(typeof(int), dropped.Schema.GetColumn("Age").DataType);
        Assert.False(dropped.Schema.GetColumn("Age").IsNullable);
    }

    /// <summary>
    /// Verifies that drop projects schema and columns in source order.
    /// </summary>
    [Fact]
    public void Drop_PreservesRemainingSchemaAndColumnOrder()
    {
        // Verifies that the result follows the original order after removed columns are skipped.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            First = new[] { 1 },
            Second = new[] { 2 },
            Third = new[] { 3 },
            Fourth = new[] { 4 }
        });

        var dropped = df.Drop("Second");

        Assert.Collection(
            dropped.Columns,
            column => Assert.Equal("First", column.Name),
            column => Assert.Equal("Third", column.Name),
            column => Assert.Equal("Fourth", column.Name));
        Assert.Collection(
            dropped.Schema.Columns,
            column =>
            {
                Assert.Equal("First", column.Name);
                Assert.Equal(0, column.Ordinal);
            },
            column =>
            {
                Assert.Equal("Third", column.Name);
                Assert.Equal(1, column.Ordinal);
            },
            column =>
            {
                Assert.Equal("Fourth", column.Name);
                Assert.Equal(2, column.Ordinal);
            });
    }

    /// <summary>
    /// Verifies that dropping columns does not modify the original DataFrame.
    /// </summary>
    [Fact]
    public void Drop_DoesNotModifyOriginalDataFrame()
    {
        // Verifies that drop returns a separate DataFrame and leaves the source shape intact.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali" },
            Age = new[] { 30 },
            Active = new[] { true }
        });

        var dropped = df.Drop("Age");

        Assert.NotSame(df, dropped);
        Assert.Equal(3, df.ColumnCount);
        Assert.Equal(2, dropped.ColumnCount);
        Assert.True(df.HasColumn("Age"));
        Assert.Collection(
            df.Columns,
            column => Assert.Equal("Name", column.Name),
            column => Assert.Equal("Age", column.Name),
            column => Assert.Equal("Active", column.Name));
    }

    /// <summary>
    /// Verifies that dropping with different casing succeeds and keeps source names.
    /// </summary>
    [Fact]
    public void Drop_WithDifferentCasing_ReturnsCanonicalRemainingColumnNames()
    {
        // Verifies that drop lookup is case-insensitive but remaining result names stay canonical.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 1 },
            DebugFlag = new[] { true },
            Name = new[] { "Ali" },
            InternalNote = new[] { "private" }
        });

        var dropped = df.Drop("debugflag", "INTERNALNOTE");

        Assert.Collection(
            dropped.Columns,
            column => Assert.Equal("Id", column.Name),
            column => Assert.Equal("Name", column.Name));
        Assert.Collection(
            dropped.Schema.Columns,
            column => Assert.Equal("Id", column.Name),
            column => Assert.Equal("Name", column.Name));
        Assert.Equal("Name", dropped["name"].Name);
    }

    /// <summary>
    /// Verifies that missing columns are rejected by default.
    /// </summary>
    [Fact]
    public void Drop_WithMissingColumn_ThrowsClearException()
    {
        // Verifies that Pandas-like default behavior reports missing drop columns instead of ignoring them.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 } });

        var exception = Assert.Throws<KeyNotFoundException>(() => df.Drop("MissingColumn"));

        Assert.Contains("MissingColumn", exception.Message);
        Assert.Contains("does not exist", exception.Message);
    }

    /// <summary>
    /// Verifies that null column name arrays are rejected.
    /// </summary>
    [Fact]
    public void Drop_WithNullColumnNamesArray_ThrowsArgumentNullException()
    {
        // Verifies that the params array itself must be supplied.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 } });
        string[] columnNames = null!;

        Assert.Throws<ArgumentNullException>(() => df.Drop(columnNames));
    }

    /// <summary>
    /// Verifies that empty column name arrays are rejected.
    /// </summary>
    [Fact]
    public void Drop_WithEmptyColumnNamesArray_ThrowsArgumentException()
    {
        // Verifies that a drop operation must name at least one column.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 } });

        Assert.Throws<ArgumentException>(() => df.Drop());
    }

    /// <summary>
    /// Verifies that invalid drop column names are rejected.
    /// </summary>
    /// <param name="columnName">The invalid drop column name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Drop_WithInvalidColumnName_Throws(string? columnName)
    {
        // Verifies that each drop column name must be meaningful.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 } });

        Assert.ThrowsAny<ArgumentException>(() => df.Drop(columnName!));
    }

    /// <summary>
    /// Verifies that duplicate drop column names are rejected.
    /// </summary>
    [Fact]
    public void Drop_WithDuplicateColumnNames_ThrowsArgumentException()
    {
        // Verifies that drop does not accept the same column more than once.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 }, Name = new[] { "Ali" } });

        Assert.Throws<ArgumentException>(() => df.Drop("Age", "Age"));
    }

    /// <summary>
    /// Verifies that duplicate drop column names are rejected case-insensitively.
    /// </summary>
    [Fact]
    public void Drop_WithDuplicateColumnNamesDifferentCasing_ThrowsArgumentException()
    {
        // Verifies that drop duplicate detection matches DataFrame lookup semantics.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 }, Name = new[] { "Ali" } });

        Assert.Throws<ArgumentException>(() => df.Drop("Age", "age"));
    }

    /// <summary>
    /// Verifies that dropping all columns is rejected clearly.
    /// </summary>
    [Fact]
    public void Drop_WithAllColumns_ThrowsArgumentException()
    {
        // Verifies that drop does not create a zero-column DataFrame while schemas require columns.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 }, Name = new[] { "Ali" } });

        var exception = Assert.Throws<ArgumentException>(() => df.Drop("Age", "Name"));

        Assert.Contains("Dropping all columns", exception.Message);
    }

    /// <summary>
    /// Verifies that dropped DataFrames remain immutable through the public API.
    /// </summary>
    [Fact]
    public void Drop_ResultDoesNotExposePublicMutation()
    {
        // Verifies that drop results expose read-only state like created DataFrames.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30 }, Name = new[] { "Ali" } });

        var dropped = df.Drop("Name");
        var writableProperties = dropped.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.SetMethod is not null)
            .Select(static property => property.Name);

        Assert.False(dropped.Columns is ICollection<ISeries> { IsReadOnly: false });
        Assert.Empty(writableProperties);
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
