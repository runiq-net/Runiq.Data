using Runiq.Data.Schema;

namespace Runiq.Data.Core.Tests.Schema;

/// <summary>
/// Verifies the behavior of ordered DataFrame schema definitions.
/// </summary>
public sealed class DataFrameSchemaTests
{
    /// <summary>
    /// Verifies that a valid schema can be created.
    /// </summary>
    [Fact]
    public void Create_WithValidColumns_Succeeds()
    {
        // Verifies that a schema can be built from one or more valid columns.
        var schema = DataFrameSchema.Create(
            ColumnSchema.Create("Id", typeof(int), nullable: false, ordinal: 0),
            ColumnSchema.Create("Name", typeof(string), nullable: true, ordinal: 1));

        Assert.NotNull(schema);
    }

    /// <summary>
    /// Verifies that column order is preserved.
    /// </summary>
    [Fact]
    public void Create_PreservesColumnOrder()
    {
        // Verifies that schema order follows the order of the supplied columns.
        var schema = DataFrameSchema.Create(
            ColumnSchema.Create("Id", typeof(int), nullable: false, ordinal: 0),
            ColumnSchema.Create("Name", typeof(string), nullable: true, ordinal: 1),
            ColumnSchema.Create("CreatedAt", typeof(DateTime), nullable: false, ordinal: 2));

        Assert.Collection(
            schema.Columns,
            column => Assert.Equal("Id", column.Name),
            column => Assert.Equal("Name", column.Name),
            column => Assert.Equal("CreatedAt", column.Name));
    }

    /// <summary>
    /// Verifies that Count reports the number of columns.
    /// </summary>
    [Fact]
    public void Count_ReturnsNumberOfColumns()
    {
        // Verifies that Count reflects the final immutable schema shape.
        var schema = DataFrameSchema.Create(
            ColumnSchema.Create("Id", typeof(int), nullable: false, ordinal: 0),
            ColumnSchema.Create("Name", typeof(string), nullable: true, ordinal: 1));

        Assert.Equal(2, schema.Count);
    }

    /// <summary>
    /// Verifies that the public columns collection cannot be modified.
    /// </summary>
    [Fact]
    public void Columns_CannotBeModifiedFromPublicApi()
    {
        // Verifies that consumers receive a read-only view of the schema columns.
        var schema = DataFrameSchema.Create(ColumnSchema.Create("Id", typeof(int), nullable: false, ordinal: 0));

        Assert.False(schema.Columns is ICollection<ColumnSchema> { IsReadOnly: false });
    }

    /// <summary>
    /// Verifies that lookup by existing column name succeeds.
    /// </summary>
    [Fact]
    public void GetColumn_WithExistingName_ReturnsColumn()
    {
        // Verifies that a column can be retrieved by its schema name.
        var schema = DataFrameSchema.Create(
            ColumnSchema.Create("Id", typeof(int), nullable: false, ordinal: 0),
            ColumnSchema.Create("Name", typeof(string), nullable: true, ordinal: 1));

        var column = schema.GetColumn("Name");

        Assert.Equal("Name", column.Name);
    }

    /// <summary>
    /// Verifies that lookup uses case-insensitive column names.
    /// </summary>
    [Fact]
    public void GetColumn_WithDifferentNameCasing_ReturnsColumn()
    {
        // Verifies that schema lookup matches the duplicate-name comparison behavior.
        var schema = DataFrameSchema.Create(ColumnSchema.Create("Name", typeof(string), nullable: true, ordinal: 0));

        var column = schema.GetColumn("name");

        Assert.Equal("Name", column.Name);
    }

    /// <summary>
    /// Verifies that lookup by missing column name fails clearly.
    /// </summary>
    [Fact]
    public void GetColumn_WithMissingName_ThrowsKeyNotFoundException()
    {
        // Verifies that missing column lookup reports a clear failure.
        var schema = DataFrameSchema.Create(ColumnSchema.Create("Id", typeof(int), nullable: false, ordinal: 0));

        Assert.Throws<KeyNotFoundException>(() => schema.GetColumn("Missing"));
    }

    /// <summary>
    /// Verifies that ContainsColumn returns true for an existing column.
    /// </summary>
    [Fact]
    public void ContainsColumn_WithExistingName_ReturnsTrue()
    {
        // Verifies that name existence checks succeed for present columns.
        var schema = DataFrameSchema.Create(ColumnSchema.Create("Id", typeof(int), nullable: false, ordinal: 0));

        Assert.True(schema.ContainsColumn("Id"));
    }

    /// <summary>
    /// Verifies that ContainsColumn uses case-insensitive names.
    /// </summary>
    [Fact]
    public void ContainsColumn_WithDifferentNameCasing_ReturnsTrue()
    {
        // Verifies that existence checks use the schema's case-insensitive name comparison.
        var schema = DataFrameSchema.Create(ColumnSchema.Create("Id", typeof(int), nullable: false, ordinal: 0));

        Assert.True(schema.ContainsColumn("id"));
    }

    /// <summary>
    /// Verifies that ContainsColumn returns false for a missing column.
    /// </summary>
    [Fact]
    public void ContainsColumn_WithMissingName_ReturnsFalse()
    {
        // Verifies that name existence checks return false when no column matches.
        var schema = DataFrameSchema.Create(ColumnSchema.Create("Id", typeof(int), nullable: false, ordinal: 0));

        Assert.False(schema.ContainsColumn("Missing"));
    }

    /// <summary>
    /// Verifies that duplicate column names are rejected.
    /// </summary>
    [Fact]
    public void Create_WithDuplicateColumnNames_ThrowsArgumentException()
    {
        // Verifies that a schema cannot contain two columns with the same name.
        Assert.Throws<ArgumentException>(() => DataFrameSchema.Create(
            ColumnSchema.Create("Id", typeof(int), nullable: false, ordinal: 0),
            ColumnSchema.Create("Id", typeof(long), nullable: false, ordinal: 1)));
    }

    /// <summary>
    /// Verifies that duplicate column names are rejected case-insensitively.
    /// </summary>
    [Fact]
    public void Create_WithDuplicateColumnNamesDifferentCasing_ThrowsArgumentException()
    {
        // Verifies that duplicate detection treats column names case-insensitively.
        Assert.Throws<ArgumentException>(() => DataFrameSchema.Create(
            ColumnSchema.Create("Id", typeof(int), nullable: false, ordinal: 0),
            ColumnSchema.Create("id", typeof(long), nullable: false, ordinal: 1)));
    }

    /// <summary>
    /// Verifies that a null column collection is rejected.
    /// </summary>
    [Fact]
    public void Create_WithNullColumnCollection_ThrowsArgumentNullException()
    {
        // Verifies that callers must supply an actual column collection.
        Assert.Throws<ArgumentNullException>(() => DataFrameSchema.Create(null!));
    }

    /// <summary>
    /// Verifies that an empty column collection is rejected.
    /// </summary>
    [Fact]
    public void Create_WithEmptyColumnCollection_ThrowsArgumentException()
    {
        // Verifies that a schema must define at least one column.
        Assert.Throws<ArgumentException>(() => DataFrameSchema.Create());
    }

    /// <summary>
    /// Verifies that a null column item is rejected.
    /// </summary>
    [Fact]
    public void Create_WithNullColumnItem_ThrowsArgumentException()
    {
        // Verifies that every schema column item must be a valid ColumnSchema instance.
        Assert.Throws<ArgumentException>(() => DataFrameSchema.Create(
            ColumnSchema.Create("Id", typeof(int), nullable: false, ordinal: 0),
            null!));
    }

    /// <summary>
    /// Verifies that ordinals match final schema order.
    /// </summary>
    [Fact]
    public void Create_AssignsOrdinalsThatMatchFinalOrder()
    {
        // Verifies that each schema column ordinal matches its final position.
        var schema = DataFrameSchema.Create(
            ColumnSchema.Create("Name", typeof(string), nullable: true, ordinal: 0),
            ColumnSchema.Create("Id", typeof(int), nullable: false, ordinal: 1));

        Assert.Collection(
            schema.Columns,
            column => Assert.Equal(0, column.Ordinal),
            column => Assert.Equal(1, column.Ordinal));
    }

    /// <summary>
    /// Verifies that non-sequential input ordinals are normalized.
    /// </summary>
    [Fact]
    public void Create_WithNonSequentialOrdinals_NormalizesOrdinalsToColumnOrder()
    {
        // Verifies that DataFrameSchema owns the final column positions.
        var schema = DataFrameSchema.Create(
            ColumnSchema.Create("Name", typeof(string), nullable: true, ordinal: 10),
            ColumnSchema.Create("Id", typeof(int), nullable: false, ordinal: 20));

        Assert.Collection(
            schema.Columns,
            column => Assert.Equal(0, column.Ordinal),
            column => Assert.Equal(1, column.Ordinal));
    }
}
