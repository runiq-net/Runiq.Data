namespace Runiq.Data.Tests.DataFrame;

/// <summary>
/// Verifies developer-friendly column type checks and required column validation helpers.
/// </summary>
public sealed class DataFrameColumnTypeTests
{
    /// <summary>
    /// Verifies that generic type checks succeed for matching column types.
    /// </summary>
    [Fact]
    public void HasColumnTypeGeneric_WithMatchingType_ReturnsTrue()
    {
        // Verifies that generic type checks report a present column with the expected type.
        var df = CreatePeopleDataFrame();

        var result = df.HasColumnType<int>("Age");

        Assert.True(result);
    }

    /// <summary>
    /// Verifies that generic type checks fail for mismatched column types.
    /// </summary>
    [Fact]
    public void HasColumnTypeGeneric_WithDifferentType_ReturnsFalse()
    {
        // Verifies that generic type checks are safe boolean checks for wrong types.
        var df = CreatePeopleDataFrame();

        var result = df.HasColumnType<string>("Age");

        Assert.False(result);
    }

    /// <summary>
    /// Verifies that generic type checks fail for missing columns.
    /// </summary>
    [Fact]
    public void HasColumnTypeGeneric_WithMissingColumn_ReturnsFalse()
    {
        // Verifies that missing columns return false instead of throwing.
        var df = CreatePeopleDataFrame();

        var result = df.HasColumnType<int>("Missing");

        Assert.False(result);
    }

    /// <summary>
    /// Verifies that generic type checks use case-insensitive column lookup.
    /// </summary>
    [Fact]
    public void HasColumnTypeGeneric_WithDifferentCasing_ReturnsTrue()
    {
        // Verifies that type checks match the DataFrame column lookup semantics.
        var df = CreatePeopleDataFrame();

        var result = df.HasColumnType<int>("age");

        Assert.True(result);
    }

    /// <summary>
    /// Verifies that generic type checks reject invalid column names.
    /// </summary>
    /// <param name="columnName">The invalid column name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HasColumnTypeGeneric_WithInvalidColumnName_Throws(string? columnName)
    {
        // Verifies that type checks still require a meaningful column name.
        var df = CreatePeopleDataFrame();

        Assert.ThrowsAny<ArgumentException>(() => df.HasColumnType<int>(columnName!));
    }

    /// <summary>
    /// Verifies that generic type checks work after immutable rename.
    /// </summary>
    [Fact]
    public void HasColumnTypeGeneric_AfterRenameColumn_ReturnsExpectedResults()
    {
        // Verifies that renamed DataFrames rebuild lookup data used by type checks.
        var df = CreatePeopleDataFrame();

        var renamed = df.RenameColumn("Age", "Years");

        Assert.True(renamed.HasColumnType<int>("years"));
        Assert.False(renamed.HasColumnType<int>("Age"));
    }

    /// <summary>
    /// Verifies that generic type checks work after mutable rename.
    /// </summary>
    [Fact]
    public void HasColumnTypeGeneric_AfterRenameColumnInPlace_ReturnsExpectedResults()
    {
        // Verifies that mutable rename keeps type checks synchronized with current columns.
        var df = CreatePeopleDataFrame();

        df.RenameColumnInPlace("Age", "Years");

        Assert.True(df.HasColumnType<int>("years"));
        Assert.False(df.HasColumnType<int>("Age"));
    }

    /// <summary>
    /// Verifies that generic type checks work after immutable add-column.
    /// </summary>
    [Fact]
    public void HasColumnTypeGeneric_AfterWithColumn_ReturnsExpectedResults()
    {
        // Verifies that type checks include columns added through immutable enrichment.
        var df = CreatePeopleDataFrame();

        var enriched = df.WithColumn("Active", new[] { true, false });

        Assert.True(enriched.HasColumnType<bool>("active"));
        Assert.False(df.HasColumnType<bool>("Active"));
    }

    /// <summary>
    /// Verifies that generic type checks work after mutable add-column.
    /// </summary>
    [Fact]
    public void HasColumnTypeGeneric_AfterAddColumn_ReturnsExpectedResults()
    {
        // Verifies that type checks include columns added through explicit mutation.
        var df = CreatePeopleDataFrame();

        df.AddColumn("Active", new[] { true, false });

        Assert.True(df.HasColumnType<bool>("active"));
    }

    /// <summary>
    /// Verifies that runtime type checks succeed for matching column types.
    /// </summary>
    [Fact]
    public void HasColumnType_WithMatchingType_ReturnsTrue()
    {
        // Verifies that runtime Type arguments are accepted for matching columns.
        var df = CreatePeopleDataFrame();

        var result = df.HasColumnType("Age", typeof(int));

        Assert.True(result);
    }

    /// <summary>
    /// Verifies that runtime type checks fail for mismatched column types.
    /// </summary>
    [Fact]
    public void HasColumnType_WithDifferentType_ReturnsFalse()
    {
        // Verifies that runtime type checks return false for existing columns with wrong types.
        var df = CreatePeopleDataFrame();

        var result = df.HasColumnType("Age", typeof(string));

        Assert.False(result);
    }

    /// <summary>
    /// Verifies that runtime type checks fail for missing columns.
    /// </summary>
    [Fact]
    public void HasColumnType_WithMissingColumn_ReturnsFalse()
    {
        // Verifies that runtime type checks are safe for missing columns.
        var df = CreatePeopleDataFrame();

        var result = df.HasColumnType("Missing", typeof(int));

        Assert.False(result);
    }

    /// <summary>
    /// Verifies that runtime type checks use case-insensitive column lookup.
    /// </summary>
    [Fact]
    public void HasColumnType_WithDifferentCasing_ReturnsTrue()
    {
        // Verifies that runtime type checks ignore column name casing.
        var df = CreatePeopleDataFrame();

        var result = df.HasColumnType("AGE", typeof(int));

        Assert.True(result);
    }

    /// <summary>
    /// Verifies that runtime type checks reject invalid column names.
    /// </summary>
    /// <param name="columnName">The invalid column name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HasColumnType_WithInvalidColumnName_Throws(string? columnName)
    {
        // Verifies that runtime type checks require a meaningful column name.
        var df = CreatePeopleDataFrame();

        Assert.ThrowsAny<ArgumentException>(() => df.HasColumnType(columnName!, typeof(int)));
    }

    /// <summary>
    /// Verifies that runtime type checks reject null type arguments.
    /// </summary>
    [Fact]
    public void HasColumnType_WithNullDataType_ThrowsArgumentNullException()
    {
        // Verifies that callers must supply an expected CLR type.
        var df = CreatePeopleDataFrame();

        Assert.Throws<ArgumentNullException>(() => df.HasColumnType("Age", null!));
    }

    /// <summary>
    /// Verifies that required column lookup returns an existing column.
    /// </summary>
    [Fact]
    public void RequireColumn_WithExistingColumn_ReturnsColumn()
    {
        // Verifies that required column lookup returns the matching series.
        var df = CreatePeopleDataFrame();

        var column = df.RequireColumn("Age");

        Assert.Equal("Age", column.Name);
        Assert.Equal(typeof(int), column.DataType);
    }

    /// <summary>
    /// Verifies that required column lookup is case-insensitive.
    /// </summary>
    [Fact]
    public void RequireColumn_WithDifferentCasing_ReturnsColumn()
    {
        // Verifies that required column lookup accepts different casing.
        var df = CreatePeopleDataFrame();

        var column = df.RequireColumn("age");

        Assert.Equal("Age", column.Name);
        Assert.Equal(typeof(int), column.DataType);
    }

    /// <summary>
    /// Verifies that required column lookup throws clearly for missing columns.
    /// </summary>
    [Fact]
    public void RequireColumn_WithMissingColumn_ThrowsClearException()
    {
        // Verifies that fail-fast lookup reports the missing column name.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<KeyNotFoundException>(() => df.RequireColumn("MissingColumn"));

        Assert.Contains("MissingColumn", exception.Message);
    }

    /// <summary>
    /// Verifies that required column lookup rejects invalid names.
    /// </summary>
    /// <param name="columnName">The invalid column name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RequireColumn_WithInvalidColumnName_Throws(string? columnName)
    {
        // Verifies that required lookup validates the requested column name.
        var df = CreatePeopleDataFrame();

        Assert.ThrowsAny<ArgumentException>(() => df.RequireColumn(columnName!));
    }

    /// <summary>
    /// Verifies that generic required lookup returns an existing column when the type matches.
    /// </summary>
    [Fact]
    public void RequireColumnGeneric_WithMatchingType_ReturnsColumn()
    {
        // Verifies that typed required lookup returns the matching series.
        var df = CreatePeopleDataFrame();

        var column = df.RequireColumn<int>("Age");

        Assert.Equal("Age", column.Name);
        Assert.Equal(typeof(int), column.DataType);
    }

    /// <summary>
    /// Verifies that generic required lookup is case-insensitive.
    /// </summary>
    [Fact]
    public void RequireColumnGeneric_WithDifferentCasing_ReturnsColumn()
    {
        // Verifies that typed required lookup accepts different casing.
        var df = CreatePeopleDataFrame();

        var column = df.RequireColumn<int>("AGE");

        Assert.Equal("Age", column.Name);
        Assert.Equal(typeof(int), column.DataType);
    }

    /// <summary>
    /// Verifies that generic required lookup throws clearly for missing columns.
    /// </summary>
    [Fact]
    public void RequireColumnGeneric_WithMissingColumn_ThrowsClearException()
    {
        // Verifies that typed required lookup reports missing column names.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<KeyNotFoundException>(() => df.RequireColumn<int>("MissingColumn"));

        Assert.Contains("MissingColumn", exception.Message);
    }

    /// <summary>
    /// Verifies that generic required lookup throws clearly for wrong column types.
    /// </summary>
    [Fact]
    public void RequireColumnGeneric_WithWrongType_ThrowsClearException()
    {
        // Verifies that typed required lookup reports the column, expected type, and actual type.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentException>(() => df.RequireColumn<string>("Age"));

        Assert.Contains("Age", exception.Message);
        Assert.Contains(typeof(string).ToString(), exception.Message);
        Assert.Contains(typeof(int).ToString(), exception.Message);
    }

    /// <summary>
    /// Verifies that generic required lookup rejects invalid names.
    /// </summary>
    /// <param name="columnName">The invalid column name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RequireColumnGeneric_WithInvalidColumnName_Throws(string? columnName)
    {
        // Verifies that typed required lookup validates the requested column name.
        var df = CreatePeopleDataFrame();

        Assert.ThrowsAny<ArgumentException>(() => df.RequireColumn<int>(columnName!));
    }

    /// <summary>
    /// Verifies that generic required lookup works after immutable add-column.
    /// </summary>
    [Fact]
    public void RequireColumnGeneric_AfterWithColumn_ReturnsColumn()
    {
        // Verifies that typed required lookup includes columns added immutably.
        var df = CreatePeopleDataFrame();

        var enriched = df.WithColumn("Active", new[] { true, false });
        var column = enriched.RequireColumn<bool>("active");

        Assert.Equal("Active", column.Name);
        Assert.Equal(typeof(bool), column.DataType);
    }

    /// <summary>
    /// Verifies that generic required lookup works after mutable add-column.
    /// </summary>
    [Fact]
    public void RequireColumnGeneric_AfterAddColumn_ReturnsColumn()
    {
        // Verifies that typed required lookup includes columns added mutably.
        var df = CreatePeopleDataFrame();

        df.AddColumn("Active", new[] { true, false });
        var column = df.RequireColumn<bool>("ACTIVE");

        Assert.Equal("Active", column.Name);
        Assert.Equal(typeof(bool), column.DataType);
    }

    private static global::Runiq.Data.DataFrame CreatePeopleDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Age = new[] { 30, 25 },
            Salary = new[] { 120000m, 95000m }
        });
    }
}
