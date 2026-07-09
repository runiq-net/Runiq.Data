namespace Runiq.Data.Tests.DataFrame;

/// <summary>
/// Verifies row filtering behavior on DataFrame.
/// </summary>
public sealed class DataFrameFilterTests
{
    /// <summary>
    /// Verifies that Filter returns only rows whose predicate matches.
    /// </summary>
    [Fact]
    public void Filter_WhenPredicateMatchesRows_ReturnsOnlyMatchingRows()
    {
        // This test verifies that row filtering preserves only rows whose predicate evaluates to true.
        var df = CreatePeopleDataFrame();

        var adults = df.Filter(row => row["Age"] >= 30);

        Assert.Equal(2, adults.RowCount);
        Assert.Equal("Ali", adults["Name"].GetValue(0));
        Assert.Equal("Mehmet", adults["Name"].GetValue(1));
    }

    /// <summary>
    /// Verifies that Filter excludes rows whose predicate does not match.
    /// </summary>
    [Fact]
    public void Filter_WhenPredicateDoesNotMatchRows_ExcludesNonMatchingRows()
    {
        // This test verifies that rows returning false are not copied into the result.
        var df = CreatePeopleDataFrame();

        var young = df.Filter(row => row["Age"] < 30);

        Assert.Equal(1, young.RowCount);
        Assert.Equal("Ayse", young["Name"].GetValue(0));
    }

    /// <summary>
    /// Verifies that Filter does not mutate the original DataFrame.
    /// </summary>
    [Fact]
    public void Filter_DoesNotModifyOriginalDataFrame()
    {
        // This test verifies that filtering returns a separate DataFrame and keeps the source intact.
        var df = CreatePeopleDataFrame();

        var adults = df.Filter(row => row["Age"] >= 30);

        Assert.NotSame(df, adults);
        Assert.Equal(3, df.RowCount);
        Assert.Equal("Ayse", df["Name"].GetValue(1));
        Assert.Equal(2, adults.RowCount);
    }

    /// <summary>
    /// Verifies that Filter preserves column schema and column order.
    /// </summary>
    [Fact]
    public void Filter_PreservesColumnSchemaAndColumnOrder()
    {
        // This test verifies that filtering changes rows only and leaves column metadata in order.
        var df = CreatePeopleDataFrame();

        var filtered = df.Filter(row => row["Salary"] > 100000m);

        Assert.Equal(new[] { "Name", "Age", "Salary", "IsActive" }, ColumnNames(filtered));
        Assert.Equal(new[] { "Name", "Age", "Salary", "IsActive" }, SchemaNames(filtered));
        Assert.Equal(typeof(string), filtered.Schema.GetColumn("Name").DataType);
        Assert.Equal(typeof(int), filtered.Schema.GetColumn("Age").DataType);
        Assert.Equal(typeof(decimal), filtered.Schema.GetColumn("Salary").DataType);
        Assert.Equal(typeof(bool), filtered.Schema.GetColumn("IsActive").DataType);
    }

    /// <summary>
    /// Verifies that Filter preserves source row order.
    /// </summary>
    [Fact]
    public void Filter_PreservesRowOrder()
    {
        // This test verifies that matching rows keep their original relative order.
        var df = CreatePeopleDataFrame();

        var filtered = df.Filter(row => row["Age"] >= 25);

        Assert.Equal("Ali", filtered["Name"].GetValue(0));
        Assert.Equal("Ayse", filtered["Name"].GetValue(1));
        Assert.Equal("Mehmet", filtered["Name"].GetValue(2));
    }

    /// <summary>
    /// Verifies that Filter can return an empty DataFrame.
    /// </summary>
    [Fact]
    public void Filter_WhenNoRowsMatch_ReturnsEmptyDataFrame()
    {
        // This test verifies that filtering may produce zero rows while preserving columns.
        var df = CreatePeopleDataFrame();

        var filtered = df.Filter(row => row["Age"] > 100);

        Assert.Equal(0, filtered.RowCount);
        Assert.Equal(4, filtered.ColumnCount);
        Assert.Equal(new[] { "Name", "Age", "Salary", "IsActive" }, ColumnNames(filtered));
    }

    /// <summary>
    /// Verifies that Filter returns all rows when every row matches.
    /// </summary>
    [Fact]
    public void Filter_WhenAllRowsMatch_ReturnsAllRows()
    {
        // This test verifies that an always-true predicate copies every row.
        var df = CreatePeopleDataFrame();

        var filtered = df.Filter(row => row["Age"] >= 0);

        Assert.Equal(df.RowCount, filtered.RowCount);
        Assert.Equal("Mehmet", filtered["Name"].GetValue(2));
    }

    /// <summary>
    /// Verifies that Filter rejects null predicates.
    /// </summary>
    [Fact]
    public void Filter_WithNullPredicate_ThrowsArgumentNullException()
    {
        // This test verifies that a predicate must be supplied.
        var df = CreatePeopleDataFrame();
        Func<global::Runiq.Data.DataFrameFilterRow, bool> predicate = null!;

        Assert.Throws<ArgumentNullException>(() => df.Filter(predicate));
    }

    /// <summary>
    /// Verifies that Filter rejects missing columns used inside predicates.
    /// </summary>
    [Fact]
    public void Filter_WhenPredicateUsesMissingColumn_ThrowsKeyNotFoundException()
    {
        // This test verifies that missing predicate columns fail fast instead of returning empty results.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<KeyNotFoundException>(() => df.Filter(row => row["MissingColumn"] == 1));

        Assert.Contains("MissingColumn", exception.Message);
    }

    /// <summary>
    /// Verifies that Filter rejects type mismatches inside predicates.
    /// </summary>
    [Fact]
    public void Filter_WhenPredicateUsesTypeMismatch_ThrowsArgumentException()
    {
        // This test verifies that incompatible comparisons fail fast instead of evaluating false.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentException>(() => df.Filter(row => row["Name"] >= 30));

        Assert.Contains("Name", exception.Message);
        Assert.Contains("String", exception.Message);
        Assert.Contains("Int32", exception.Message);
    }

    /// <summary>
    /// Verifies that Filter does not swallow predicate exceptions.
    /// </summary>
    [Fact]
    public void Filter_WhenPredicateThrows_PropagatesException()
    {
        // This test verifies that custom predicate failures are visible to the caller.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<InvalidOperationException>(() => df.Filter(_ => throw new InvalidOperationException("boom")));

        Assert.Equal("boom", exception.Message);
    }

    /// <summary>
    /// Verifies that greater-than-or-equal integer comparisons work.
    /// </summary>
    [Fact]
    public void Filter_WithIntegerGreaterThanOrEqualComparison_ReturnsMatchingRows()
    {
        // This test verifies that row["Age"] >= 30 works for integer columns.
        var df = CreatePeopleDataFrame();

        var filtered = df.Filter(row => row["Age"] >= 30);

        Assert.Equal(new[] { "Ali", "Mehmet" }, Names(filtered));
    }

    /// <summary>
    /// Verifies that greater-than integer comparisons work.
    /// </summary>
    [Fact]
    public void Filter_WithIntegerGreaterThanComparison_ReturnsMatchingRows()
    {
        // This test verifies that row["Age"] > 30 works for integer columns.
        var df = CreatePeopleDataFrame();

        var filtered = df.Filter(row => row["Age"] > 30);

        Assert.Equal(new[] { "Mehmet" }, Names(filtered));
    }

    /// <summary>
    /// Verifies that less-than-or-equal integer comparisons work.
    /// </summary>
    [Fact]
    public void Filter_WithIntegerLessThanOrEqualComparison_ReturnsMatchingRows()
    {
        // This test verifies that row["Age"] <= 30 works for integer columns.
        var df = CreatePeopleDataFrame();

        var filtered = df.Filter(row => row["Age"] <= 30);

        Assert.Equal(new[] { "Ali", "Ayse" }, Names(filtered));
    }

    /// <summary>
    /// Verifies that less-than integer comparisons work.
    /// </summary>
    [Fact]
    public void Filter_WithIntegerLessThanComparison_ReturnsMatchingRows()
    {
        // This test verifies that row["Age"] < 30 works for integer columns.
        var df = CreatePeopleDataFrame();

        var filtered = df.Filter(row => row["Age"] < 30);

        Assert.Equal(new[] { "Ayse" }, Names(filtered));
    }

    /// <summary>
    /// Verifies that decimal comparisons work.
    /// </summary>
    [Fact]
    public void Filter_WithDecimalGreaterThanComparison_ReturnsMatchingRows()
    {
        // This test verifies that row["Salary"] > 100000m works for decimal columns.
        var df = CreatePeopleDataFrame();

        var filtered = df.Filter(row => row["Salary"] > 100000m);

        Assert.Equal(new[] { "Ali", "Mehmet" }, Names(filtered));
    }

    /// <summary>
    /// Verifies that numeric comparisons reject wrong literal types.
    /// </summary>
    [Fact]
    public void Filter_WithWrongNumericComparisonType_ThrowsArgumentException()
    {
        // This test verifies that numeric comparisons do not coerce strings to numbers.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentException>(() => df.Filter(row => row["Age"] == "30"));

        Assert.Contains("Age", exception.Message);
        Assert.Contains("Int32", exception.Message);
        Assert.Contains("String", exception.Message);
    }

    /// <summary>
    /// Verifies that string equality comparisons work.
    /// </summary>
    [Fact]
    public void Filter_WithStringEqualityComparison_ReturnsMatchingRows()
    {
        // This test verifies that row["Name"] == "Ali" works for string columns.
        var df = CreatePeopleDataFrame();

        var filtered = df.Filter(row => row["Name"] == "Ali");

        Assert.Equal(new[] { "Ali" }, Names(filtered));
    }

    /// <summary>
    /// Verifies that string inequality comparisons work.
    /// </summary>
    [Fact]
    public void Filter_WithStringInequalityComparison_ReturnsMatchingRows()
    {
        // This test verifies that row["Name"] != "Ali" works for string columns.
        var df = CreatePeopleDataFrame();

        var filtered = df.Filter(row => row["Name"] != "Ali");

        Assert.Equal(new[] { "Ayse", "Mehmet" }, Names(filtered));
    }

    /// <summary>
    /// Verifies that Boolean equality comparisons work.
    /// </summary>
    [Fact]
    public void Filter_WithBooleanEqualityComparison_ReturnsMatchingRows()
    {
        // This test verifies that row["IsActive"] == true works for Boolean columns.
        var df = CreatePeopleDataFrame();

        var filtered = df.Filter(row => row["IsActive"] == true);

        Assert.Equal(new[] { "Ali", "Ayse" }, Names(filtered));
    }

    /// <summary>
    /// Verifies that Boolean inequality comparisons work.
    /// </summary>
    [Fact]
    public void Filter_WithBooleanInequalityComparison_ReturnsMatchingRows()
    {
        // This test verifies that row["IsActive"] != false works for Boolean columns.
        var df = CreatePeopleDataFrame();

        var filtered = df.Filter(row => row["IsActive"] != false);

        Assert.Equal(new[] { "Ali", "Ayse" }, Names(filtered));
    }

    /// <summary>
    /// Verifies that equality comparisons reject incompatible types.
    /// </summary>
    [Fact]
    public void Filter_WithTypeMismatchedEqualityComparison_ThrowsArgumentException()
    {
        // This test verifies that equality comparisons fail fast for incompatible cell and literal types.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<ArgumentException>(() => df.Filter(row => row["IsActive"] == "true"));

        Assert.Contains("IsActive", exception.Message);
        Assert.Contains("Boolean", exception.Message);
        Assert.Contains("String", exception.Message);
    }

    /// <summary>
    /// Verifies that compound predicates work.
    /// </summary>
    [Fact]
    public void Filter_WithCompoundPredicate_ReturnsMatchingRows()
    {
        // This test verifies that multiple row comparisons compose with Boolean operators.
        var df = CreatePeopleDataFrame();

        var filtered = df.Filter(row => row["IsActive"] == true && row["Age"] >= 30);

        Assert.Equal(new[] { "Ali" }, Names(filtered));
    }

    /// <summary>
    /// Verifies that Select and Filter compose.
    /// </summary>
    [Fact]
    public void SelectThenFilter_ComposesWithExistingProjectionApi()
    {
        // This test verifies that filtered projections keep existing DataFrame operation behavior.
        var df = CreatePeopleDataFrame();

        var filtered = df.Select("Name", "Age").Filter(row => row["Age"] >= 30);

        Assert.Equal(new[] { "Name", "Age" }, ColumnNames(filtered));
        Assert.Equal(new[] { "Ali", "Mehmet" }, Names(filtered));
    }

    /// <summary>
    /// Verifies that Filter and Select compose.
    /// </summary>
    [Fact]
    public void FilterThenSelect_ComposesWithExistingProjectionApi()
    {
        // This test verifies that projection after filtering keeps the filtered row set.
        var df = CreatePeopleDataFrame();

        var selected = df.Filter(row => row["IsActive"] == true).Select("Name");

        Assert.Equal(new[] { "Name" }, ColumnNames(selected));
        Assert.Equal(2, selected.RowCount);
        Assert.Equal("Ali", selected["Name"].GetValue(0));
        Assert.Equal("Ayse", selected["Name"].GetValue(1));
    }

    /// <summary>
    /// Verifies that Drop and Filter compose.
    /// </summary>
    [Fact]
    public void DropThenFilter_ComposesWithExistingDropApi()
    {
        // This test verifies that filtering can run after dropping unrelated columns.
        var df = CreatePeopleDataFrame();

        var filtered = df.Drop("Salary").Filter(row => row["Age"] >= 30);

        Assert.Equal(new[] { "Name", "Age", "IsActive" }, ColumnNames(filtered));
        Assert.Equal(new[] { "Ali", "Mehmet" }, Names(filtered));
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

    private static string[] Names(global::Runiq.Data.DataFrame df)
    {
        return Enumerable.Range(0, df.RowCount)
            .Select(index => (string)df["Name"].GetValue(index)!)
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
