namespace Runiq.Data.Tests.DataFrame;

/// <summary>
/// Verifies read-only row access behavior on DataFrame.
/// </summary>
public sealed class DataFrameRowTests
{
    /// <summary>
    /// Verifies that GetRow returns a row view for a valid index.
    /// </summary>
    [Fact]
    public void GetRow_WhenIndexIsValid_ReturnsRowView()
    {
        // This test verifies that a valid zero-based row index creates a readable row view.
        var df = CreatePeopleDataFrame();

        var row = df.GetRow(0);

        Assert.NotNull(row);
    }

    /// <summary>
    /// Verifies that GetRow reads values from the first row.
    /// </summary>
    [Fact]
    public void GetRow_WithFirstIndex_ReadsFirstRowValues()
    {
        // This test verifies that row access reads values from the requested first row.
        var df = CreatePeopleDataFrame();

        var row = df.GetRow(0);

        Assert.Equal("Ali", row.String("Name"));
        Assert.Equal(30, row.Int("Age"));
    }

    /// <summary>
    /// Verifies that GetRow reads values from the last row.
    /// </summary>
    [Fact]
    public void GetRow_WithLastIndex_ReadsLastRowValues()
    {
        // This test verifies that the final valid row index is accepted and read correctly.
        var df = CreatePeopleDataFrame();

        var row = df.GetRow(1);

        Assert.Equal("Ayse", row.String("Name"));
        Assert.Equal(25, row.Int("Age"));
    }

    /// <summary>
    /// Verifies that negative row indexes are rejected.
    /// </summary>
    [Fact]
    public void GetRow_WhenIndexIsNegative_ThrowsArgumentOutOfRangeException()
    {
        // This test verifies that row access fails fast for indexes below zero.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => df.GetRow(-1));

        Assert.Equal("index", exception.ParamName);
    }

    /// <summary>
    /// Verifies that row count is not a valid row index.
    /// </summary>
    [Fact]
    public void GetRow_WhenIndexEqualsRowCount_ThrowsArgumentOutOfRangeException()
    {
        // This test verifies that row access fails fast when the index is outside the row range.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => df.GetRow(df.RowCount));

        Assert.Contains("2", exception.Message);
    }

    /// <summary>
    /// Verifies that empty DataFrames do not expose any row.
    /// </summary>
    [Fact]
    public void GetRow_WhenDataFrameIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        // This test verifies that zero-row DataFrames reject row access for index zero.
        var df = global::Runiq.Data.DataFrame.Create(new { Name = Array.Empty<string>() });

        Assert.Throws<ArgumentOutOfRangeException>(() => df.GetRow(0));
    }

    /// <summary>
    /// Verifies that the row indexer returns raw values.
    /// </summary>
    [Fact]
    public void Indexer_WithExistingColumns_ReturnsRawValues()
    {
        // This test verifies that square-bracket row access returns object values for columns.
        var row = CreatePeopleDataFrame().GetRow(0);

        Assert.Equal("Ali", row["Name"]);
        Assert.Equal(30, row["Age"]);
    }

    /// <summary>
    /// Verifies that direct row access keeps returning raw cell values.
    /// </summary>
    [Fact]
    public void DataFrameRowIndexer_WhenReadingExistingColumn_ReturnsRawValueForBackwardCompatibility()
    {
        // This test verifies that direct row access keeps returning raw cell values instead of filtering-specific CellValue wrappers.
        var row = CreatePeopleDataFrame().GetRow(0);

        Assert.IsType<int>(row["Age"]);
        Assert.IsType<string>(row["Name"]);
        Assert.Equal(30, row["Age"]);
        Assert.Equal("Ali", row["Name"]);
    }

    /// <summary>
    /// Verifies that missing row indexer columns are rejected.
    /// </summary>
    [Fact]
    public void Indexer_WithMissingColumn_ThrowsKeyNotFoundException()
    {
        // This test verifies that missing row columns fail instead of returning null.
        var row = CreatePeopleDataFrame().GetRow(0);

        var exception = Assert.Throws<KeyNotFoundException>(() => row["MissingColumn"]);

        Assert.Contains("MissingColumn", exception.Message);
    }

    /// <summary>
    /// Verifies that invalid row indexer column names are rejected.
    /// </summary>
    /// <param name="columnName">The invalid column name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Indexer_WithInvalidColumnName_Throws(string? columnName)
    {
        // This test verifies that row indexer access keeps DataFrame column-name validation.
        var row = CreatePeopleDataFrame().GetRow(0);

        Assert.ThrowsAny<ArgumentException>(() => row[columnName!]);
    }

    /// <summary>
    /// Verifies that String returns a typed string value.
    /// </summary>
    [Fact]
    public void String_WithStringColumn_ReturnsValue()
    {
        // This test verifies that the string accessor returns the requested row value.
        var row = CreatePeopleDataFrame().GetRow(0);

        Assert.Equal("Ali", row.String("Name"));
    }

    /// <summary>
    /// Verifies that Int returns a typed int value.
    /// </summary>
    [Fact]
    public void Int_WithIntColumn_ReturnsValue()
    {
        // This test verifies that the int accessor returns the requested row value.
        var row = CreatePeopleDataFrame().GetRow(0);

        Assert.Equal(30, row.Int("Age"));
    }

    /// <summary>
    /// Verifies that Long returns a typed long value.
    /// </summary>
    [Fact]
    public void Long_WithLongColumn_ReturnsValue()
    {
        // This test verifies that the long accessor returns the requested row value.
        var row = CreateTypedDataFrame().GetRow(0);

        Assert.Equal(42L, row.Long("Count"));
    }

    /// <summary>
    /// Verifies that Decimal returns a typed decimal value.
    /// </summary>
    [Fact]
    public void Decimal_WithDecimalColumn_ReturnsValue()
    {
        // This test verifies that the decimal accessor returns the requested row value.
        var row = CreateTypedDataFrame().GetRow(0);

        Assert.Equal(120000m, row.Decimal("Salary"));
    }

    /// <summary>
    /// Verifies that Double returns a typed double value.
    /// </summary>
    [Fact]
    public void Double_WithDoubleColumn_ReturnsValue()
    {
        // This test verifies that the double accessor returns the requested row value.
        var row = CreateTypedDataFrame().GetRow(0);

        Assert.Equal(98.5d, row.Double("Score"));
    }

    /// <summary>
    /// Verifies that Bool returns a typed Boolean value.
    /// </summary>
    [Fact]
    public void Bool_WithBoolColumn_ReturnsValue()
    {
        // This test verifies that the bool accessor returns the requested row value.
        var row = CreateTypedDataFrame().GetRow(0);

        Assert.True(row.Bool("IsActive"));
    }

    /// <summary>
    /// Verifies that DateTime returns a typed DateTime value.
    /// </summary>
    [Fact]
    public void DateTime_WithDateTimeColumn_ReturnsValue()
    {
        // This test verifies that the DateTime accessor returns the requested row value.
        var row = CreateTypedDataFrame().GetRow(0);

        Assert.Equal(new DateTime(2026, 7, 9), row.DateTime("CreatedAt"));
    }

    /// <summary>
    /// Verifies that missing typed accessor columns are rejected.
    /// </summary>
    [Fact]
    public void TypedAccessor_WithMissingColumn_ThrowsKeyNotFoundException()
    {
        // This test verifies that typed accessors keep fail-fast behavior for missing columns.
        var row = CreatePeopleDataFrame().GetRow(0);

        var exception = Assert.Throws<KeyNotFoundException>(() => row.Int("MissingColumn"));

        Assert.Contains("MissingColumn", exception.Message);
    }

    /// <summary>
    /// Verifies that typed accessors reject values of the wrong type.
    /// </summary>
    [Fact]
    public void TypedAccessor_WithWrongType_ThrowsArgumentException()
    {
        // This test verifies that typed accessors do not coerce incompatible values.
        var row = CreatePeopleDataFrame().GetRow(0);

        var intException = Assert.Throws<ArgumentException>(() => row.Int("Name"));
        var stringException = Assert.Throws<ArgumentException>(() => row.String("Age"));

        Assert.Contains("Name", intException.Message);
        Assert.Contains("int", intException.Message);
        Assert.Contains("String", intException.Message);
        Assert.Contains("Age", stringException.Message);
        Assert.Contains("string", stringException.Message);
        Assert.Contains("Int32", stringException.Message);
    }

    /// <summary>
    /// Verifies that typed accessors reject null values.
    /// </summary>
    [Fact]
    public void TypedAccessor_WithNullValue_ThrowsArgumentException()
    {
        // This test verifies that non-nullable typed accessors fail on null row values.
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new string?[] { null } });
        var row = df.GetRow(0);

        var exception = Assert.Throws<ArgumentException>(() => row.String("Name"));

        Assert.Contains("Name", exception.Message);
        Assert.Contains("null", exception.Message);
        Assert.Contains("string", exception.Message);
    }

    /// <summary>
    /// Verifies that typed accessors reject invalid column names.
    /// </summary>
    /// <param name="columnName">The invalid column name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TypedAccessor_WithInvalidColumnName_Throws(string? columnName)
    {
        // This test verifies that typed row access keeps DataFrame column-name validation.
        var row = CreatePeopleDataFrame().GetRow(0);

        Assert.ThrowsAny<ArgumentException>(() => row.Int(columnName!));
    }

    /// <summary>
    /// Verifies that row column lookup is case-insensitive.
    /// </summary>
    [Fact]
    public void RowAccess_WithDifferentColumnCasing_ReturnsValue()
    {
        // This test verifies that row lookup follows DataFrame column-name casing behavior.
        var row = CreatePeopleDataFrame().GetRow(0);

        Assert.Equal("Ali", row.String("name"));
        Assert.Equal(30, row.Int("AGE"));
    }

    /// <summary>
    /// Verifies that HasColumn returns true for existing columns.
    /// </summary>
    [Fact]
    public void HasColumn_WithExistingColumn_ReturnsTrue()
    {
        // This test verifies that row column existence checks report present columns.
        var row = CreatePeopleDataFrame().GetRow(0);

        Assert.True(row.HasColumn("Age"));
    }

    /// <summary>
    /// Verifies that HasColumn returns false for missing columns.
    /// </summary>
    [Fact]
    public void HasColumn_WithMissingColumn_ReturnsFalse()
    {
        // This test verifies that row column existence checks report absent columns.
        var row = CreatePeopleDataFrame().GetRow(0);

        Assert.False(row.HasColumn("Missing"));
    }

    /// <summary>
    /// Verifies that HasColumn rejects invalid column names like DataFrame.
    /// </summary>
    /// <param name="columnName">The invalid column name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HasColumn_WithInvalidColumnName_Throws(string? columnName)
    {
        // This test verifies that row HasColumn mirrors DataFrame validation for invalid names.
        var row = CreatePeopleDataFrame().GetRow(0);

        Assert.ThrowsAny<ArgumentException>(() => row.HasColumn(columnName!));
    }

    /// <summary>
    /// Verifies that ColumnNames returns column names in order.
    /// </summary>
    [Fact]
    public void ColumnNames_ReturnsColumnNamesInOrder()
    {
        // This test verifies that row column names match the DataFrame column order.
        var row = CreateTypedDataFrame().GetRow(0);

        Assert.Equal(
            new[] { "Name", "Age", "Count", "Salary", "Score", "IsActive", "CreatedAt" },
            row.ColumnNames);
    }

    /// <summary>
    /// Verifies that ColumnNames cannot be mutated through the public API.
    /// </summary>
    [Fact]
    public void ColumnNames_CannotBeModifiedFromPublicApi()
    {
        // This test verifies that row column names are exposed as a read-only collection.
        var row = CreatePeopleDataFrame().GetRow(0);

        Assert.False(row.ColumnNames is ICollection<string> { IsReadOnly: false });
    }

    /// <summary>
    /// Verifies that ColumnNames follows the current DataFrame column state.
    /// </summary>
    [Fact]
    public void ColumnNames_AfterDataFrameColumnMutation_ReturnsCurrentColumnNames()
    {
        // This test verifies that row metadata stays aligned with live DataFrame column access.
        var df = CreatePeopleDataFrame();
        var row = df.GetRow(0);

        df.AddColumn("IsActive", new[] { true, true });

        Assert.Equal(new[] { "Name", "Age", "IsActive" }, row.ColumnNames);
        Assert.True(row.Bool("IsActive"));
    }

    private static global::Runiq.Data.DataFrame CreatePeopleDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Age = new[] { 30, 25 }
        });
    }

    private static global::Runiq.Data.DataFrame CreateTypedDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali" },
            Age = new[] { 30 },
            Count = new[] { 42L },
            Salary = new[] { 120000m },
            Score = new[] { 98.5d },
            IsActive = new[] { true },
            CreatedAt = new[] { new DateTime(2026, 7, 9) }
        });
    }
}
