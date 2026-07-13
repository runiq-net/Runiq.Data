using System.Reflection;
using Runiq.Data.Schema;

namespace Runiq.Data.Core.Tests.Schema;

/// <summary>
/// Verifies the behavior of single-column schema definitions.
/// </summary>
public sealed class ColumnSchemaTests
{
    /// <summary>
    /// Verifies that a valid column definition can be created.
    /// </summary>
    [Fact]
    public void Create_WithValidArguments_Succeeds()
    {
        // Verifies that the factory accepts a complete valid column definition.
        var column = ColumnSchema.Create("Id", typeof(int), nullable: false, ordinal: 0);

        Assert.NotNull(column);
    }

    /// <summary>
    /// Verifies that the column name is preserved.
    /// </summary>
    [Fact]
    public void Create_PreservesName()
    {
        // Verifies that the public schema exposes the same name supplied by the caller.
        var column = ColumnSchema.Create("Name", typeof(string), nullable: true, ordinal: 1);

        Assert.Equal("Name", column.Name);
    }

    /// <summary>
    /// Verifies that the data type is preserved.
    /// </summary>
    [Fact]
    public void Create_PreservesDataType()
    {
        // Verifies that the public schema exposes the caller's CLR data type.
        var column = ColumnSchema.Create("CreatedAt", typeof(DateTime), nullable: false, ordinal: 2);

        Assert.Equal(typeof(DateTime), column.DataType);
    }

    /// <summary>
    /// Verifies that nullability is preserved.
    /// </summary>
    [Fact]
    public void Create_PreservesIsNullable()
    {
        // Verifies that nullable columns remain marked as nullable.
        var column = ColumnSchema.Create("Description", typeof(string), nullable: true, ordinal: 3);

        Assert.True(column.IsNullable);
    }

    /// <summary>
    /// Verifies that the ordinal is preserved.
    /// </summary>
    [Fact]
    public void Create_PreservesOrdinal()
    {
        // Verifies that a standalone column keeps the ordinal supplied by the caller.
        var column = ColumnSchema.Create("Score", typeof(decimal), nullable: false, ordinal: 4);

        Assert.Equal(4, column.Ordinal);
    }

    /// <summary>
    /// Verifies that a null column name is rejected.
    /// </summary>
    [Fact]
    public void Create_WithNullName_ThrowsArgumentNullException()
    {
        // Verifies that schema columns require an actual name value.
        Assert.Throws<ArgumentNullException>(() => ColumnSchema.Create(null!, typeof(int), nullable: false, ordinal: 0));
    }

    /// <summary>
    /// Verifies that an empty column name is rejected.
    /// </summary>
    [Fact]
    public void Create_WithEmptyName_ThrowsArgumentException()
    {
        // Verifies that schema columns cannot be identified by an empty name.
        Assert.Throws<ArgumentException>(() => ColumnSchema.Create(string.Empty, typeof(int), nullable: false, ordinal: 0));
    }

    /// <summary>
    /// Verifies that a whitespace column name is rejected.
    /// </summary>
    [Fact]
    public void Create_WithWhitespaceName_ThrowsArgumentException()
    {
        // Verifies that whitespace-only names are not valid column identifiers.
        Assert.Throws<ArgumentException>(() => ColumnSchema.Create("   ", typeof(int), nullable: false, ordinal: 0));
    }

    /// <summary>
    /// Verifies that a null data type is rejected.
    /// </summary>
    [Fact]
    public void Create_WithNullDataType_ThrowsArgumentNullException()
    {
        // Verifies that schema columns must declare the CLR type they contain.
        Assert.Throws<ArgumentNullException>(() => ColumnSchema.Create("Id", null!, nullable: false, ordinal: 0));
    }

    /// <summary>
    /// Verifies that a negative ordinal is rejected.
    /// </summary>
    [Fact]
    public void Create_WithNegativeOrdinal_ThrowsArgumentOutOfRangeException()
    {
        // Verifies that column positions cannot be negative.
        Assert.Throws<ArgumentOutOfRangeException>(() => ColumnSchema.Create("Id", typeof(int), nullable: false, ordinal: -1));
    }

    /// <summary>
    /// Verifies that created column schemas are immutable through the public API.
    /// </summary>
    [Fact]
    public void PublicApi_DoesNotExposeMutation()
    {
        // Verifies that consumers cannot mutate a column schema after it is created.
        var writableProperties = typeof(ColumnSchema)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.SetMethod is not null)
            .Select(static property => property.Name);

        Assert.Empty(writableProperties);
    }
}
