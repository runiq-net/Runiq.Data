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

        Assert.Equal(2, adults.Rows.Count());
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

        Assert.Equal(1, young.Rows.Count());
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
        Assert.Equal(3, df.Rows.Count());
        Assert.Equal("Ayse", df["Name"].GetValue(1));
        Assert.Equal(2, adults.Rows.Count());
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

        Assert.Equal(0, filtered.Rows.Count());
        Assert.Equal(4, filtered.Columns.Count());
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

        Assert.Equal(df.Rows.Count(), filtered.Rows.Count());
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
    /// Verifies that string Contains helper returns matching rows.
    /// </summary>
    [Fact]
    public void Filter_WithStringContainsHelper_ReturnsMatchingRows()
    {
        // This test verifies that row["Name"].Contains("Ali") keeps rows whose string cell contains the substring.
        var df = CreateContactDataFrame();

        var filtered = df.Filter(row => row["Name"].Contains("Ali"));

        Assert.Equal(new[] { "Ali" }, Names(filtered));
    }

    /// <summary>
    /// Verifies that string Contains helper excludes non-matching rows.
    /// </summary>
    [Fact]
    public void Filter_WithStringContainsHelper_WhenSubstringDoesNotMatch_ReturnsEmptyDataFrame()
    {
        // This test verifies that string Contains helper excludes rows whose string cell does not contain the substring.
        var df = CreateContactDataFrame();

        var filtered = df.Filter(row => row["Name"].Contains("Zeynep"));

        Assert.Equal(0, filtered.Rows.Count());
    }

    /// <summary>
    /// Verifies that string Contains helper follows .NET behavior for empty substrings.
    /// </summary>
    [Fact]
    public void Filter_WithStringContainsHelper_WhenSubstringIsEmpty_ReturnsAllStringRows()
    {
        // This test verifies that empty substring matching follows the underlying .NET string behavior.
        var df = CreateContactDataFrame();

        var filtered = df.Filter(row => row["Name"].Contains(""));

        Assert.Equal(df.Rows.Count(), filtered.Rows.Count());
    }

    /// <summary>
    /// Verifies that string Contains helper rejects non-string cells.
    /// </summary>
    [Fact]
    public void Filter_WithStringContainsHelper_WhenCellIsNotString_ThrowsArgumentException()
    {
        // This test verifies that string Contains helper fails fast instead of converting non-string values.
        var df = CreateContactDataFrame();

        var exception = Assert.Throws<ArgumentException>(() => df.Filter(row => row["Age"].Contains("3")));

        Assert.Contains("Age", exception.Message);
        Assert.Contains("Int32", exception.Message);
        Assert.Contains("String", exception.Message);
    }

    /// <summary>
    /// Verifies that string Contains helper rejects null substrings.
    /// </summary>
    [Fact]
    public void Filter_WithStringContainsHelper_WhenSubstringIsNull_ThrowsArgumentNullException()
    {
        // This test verifies that a null substring is rejected before string matching runs.
        var df = CreateContactDataFrame();

        Assert.Throws<ArgumentNullException>(() => df.Filter(row => row["Name"].Contains(null!)));
    }

    /// <summary>
    /// Verifies that string Contains helper preserves missing column validation.
    /// </summary>
    [Fact]
    public void Filter_WithStringContainsHelper_WhenColumnIsMissing_ThrowsKeyNotFoundException()
    {
        // This test verifies that string Contains helper does not hide missing column failures.
        var df = CreateContactDataFrame();

        var exception = Assert.Throws<KeyNotFoundException>(() => df.Filter(row => row["MissingColumn"].Contains("Ali")));

        Assert.Contains("MissingColumn", exception.Message);
    }

    /// <summary>
    /// Verifies that string StartsWith helper returns matching rows.
    /// </summary>
    [Fact]
    public void Filter_WithStringStartsWithHelper_ReturnsMatchingRows()
    {
        // This test verifies that row["Name"].StartsWith("A") keeps rows whose string cell starts with the prefix.
        var df = CreateContactDataFrame();

        var filtered = df.Filter(row => row["Name"].StartsWith("A"));

        Assert.Equal(new[] { "Ali", "Ayse" }, Names(filtered));
    }

    /// <summary>
    /// Verifies that string StartsWith helper excludes non-matching rows.
    /// </summary>
    [Fact]
    public void Filter_WithStringStartsWithHelper_WhenPrefixDoesNotMatch_ReturnsEmptyDataFrame()
    {
        // This test verifies that string StartsWith helper excludes rows whose string cell does not start with the prefix.
        var df = CreateContactDataFrame();

        var filtered = df.Filter(row => row["Name"].StartsWith("Z"));

        Assert.Equal(0, filtered.Rows.Count());
    }

    /// <summary>
    /// Verifies that string StartsWith helper follows .NET behavior for empty prefixes.
    /// </summary>
    [Fact]
    public void Filter_WithStringStartsWithHelper_WhenPrefixIsEmpty_ReturnsAllStringRows()
    {
        // This test verifies that empty prefix matching follows the underlying .NET string behavior.
        var df = CreateContactDataFrame();

        var filtered = df.Filter(row => row["Name"].StartsWith(""));

        Assert.Equal(df.Rows.Count(), filtered.Rows.Count());
    }

    /// <summary>
    /// Verifies that string StartsWith helper rejects non-string cells.
    /// </summary>
    [Fact]
    public void Filter_WithStringStartsWithHelper_WhenCellIsNotString_ThrowsArgumentException()
    {
        // This test verifies that string StartsWith helper fails fast instead of converting non-string values.
        var df = CreateContactDataFrame();

        var exception = Assert.Throws<ArgumentException>(() => df.Filter(row => row["Age"].StartsWith("3")));

        Assert.Contains("Age", exception.Message);
        Assert.Contains("Int32", exception.Message);
        Assert.Contains("String", exception.Message);
    }

    /// <summary>
    /// Verifies that string StartsWith helper rejects null prefixes.
    /// </summary>
    [Fact]
    public void Filter_WithStringStartsWithHelper_WhenPrefixIsNull_ThrowsArgumentNullException()
    {
        // This test verifies that a null prefix is rejected before string matching runs.
        var df = CreateContactDataFrame();

        Assert.Throws<ArgumentNullException>(() => df.Filter(row => row["Name"].StartsWith(null!)));
    }

    /// <summary>
    /// Verifies that string StartsWith helper preserves missing column validation.
    /// </summary>
    [Fact]
    public void Filter_WithStringStartsWithHelper_WhenColumnIsMissing_ThrowsKeyNotFoundException()
    {
        // This test verifies that string StartsWith helper does not hide missing column failures.
        var df = CreateContactDataFrame();

        var exception = Assert.Throws<KeyNotFoundException>(() => df.Filter(row => row["MissingColumn"].StartsWith("A")));

        Assert.Contains("MissingColumn", exception.Message);
    }

    /// <summary>
    /// Verifies that string EndsWith helper returns matching rows.
    /// </summary>
    [Fact]
    public void Filter_WithStringEndsWithHelper_ReturnsMatchingRows()
    {
        // This test verifies that row["Email"].EndsWith("@gmail.com") keeps rows whose string cell ends with the suffix.
        var df = CreateContactDataFrame();

        var filtered = df.Filter(row => row["Email"].EndsWith("@gmail.com"));

        Assert.Equal(new[] { "Ali", "Mehmet" }, Names(filtered));
    }

    /// <summary>
    /// Verifies that string EndsWith helper excludes non-matching rows.
    /// </summary>
    [Fact]
    public void Filter_WithStringEndsWithHelper_WhenSuffixDoesNotMatch_ReturnsEmptyDataFrame()
    {
        // This test verifies that string EndsWith helper excludes rows whose string cell does not end with the suffix.
        var df = CreateContactDataFrame();

        var filtered = df.Filter(row => row["Email"].EndsWith("@example.org"));

        Assert.Equal(0, filtered.Rows.Count());
    }

    /// <summary>
    /// Verifies that string EndsWith helper follows .NET behavior for empty suffixes.
    /// </summary>
    [Fact]
    public void Filter_WithStringEndsWithHelper_WhenSuffixIsEmpty_ReturnsAllStringRows()
    {
        // This test verifies that empty suffix matching follows the underlying .NET string behavior.
        var df = CreateContactDataFrame();

        var filtered = df.Filter(row => row["Email"].EndsWith(""));

        Assert.Equal(df.Rows.Count(), filtered.Rows.Count());
    }

    /// <summary>
    /// Verifies that string EndsWith helper rejects non-string cells.
    /// </summary>
    [Fact]
    public void Filter_WithStringEndsWithHelper_WhenCellIsNotString_ThrowsArgumentException()
    {
        // This test verifies that string EndsWith helper fails fast instead of converting non-string values.
        var df = CreateContactDataFrame();

        var exception = Assert.Throws<ArgumentException>(() => df.Filter(row => row["IsActive"].EndsWith("true")));

        Assert.Contains("IsActive", exception.Message);
        Assert.Contains("Boolean", exception.Message);
        Assert.Contains("String", exception.Message);
    }

    /// <summary>
    /// Verifies that string EndsWith helper rejects null suffixes.
    /// </summary>
    [Fact]
    public void Filter_WithStringEndsWithHelper_WhenSuffixIsNull_ThrowsArgumentNullException()
    {
        // This test verifies that a null suffix is rejected before string matching runs.
        var df = CreateContactDataFrame();

        Assert.Throws<ArgumentNullException>(() => df.Filter(row => row["Email"].EndsWith(null!)));
    }

    /// <summary>
    /// Verifies that string EndsWith helper preserves missing column validation.
    /// </summary>
    [Fact]
    public void Filter_WithStringEndsWithHelper_WhenColumnIsMissing_ThrowsKeyNotFoundException()
    {
        // This test verifies that string EndsWith helper does not hide missing column failures.
        var df = CreateContactDataFrame();

        var exception = Assert.Throws<KeyNotFoundException>(() => df.Filter(row => row["MissingColumn"].EndsWith("@gmail.com")));

        Assert.Contains("MissingColumn", exception.Message);
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
    /// Verifies that string helpers compose with other predicates.
    /// </summary>
    [Fact]
    public void Filter_WithCompoundPredicateAndStringHelper_ReturnsMatchingRows()
    {
        // This test verifies that string helpers compose with existing Boolean comparison predicates.
        var df = CreateContactDataFrame();

        var filtered = df.Filter(row => row["IsActive"] == true && row["Name"].StartsWith("Al"));

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
        Assert.Equal(2, selected.Rows.Count());
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

    private static global::Runiq.Data.DataFrame CreateContactDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse", "Mehmet" },
            Email = new[] { "ali@gmail.com", "ayse@example.com", "mehmet@gmail.com" },
            Age = new[] { 30, 25, 41 },
            IsActive = new[] { true, true, false }
        });
    }

    private static string[] Names(global::Runiq.Data.DataFrame df)
    {
        return Enumerable.Range(0, df.Rows.Count())
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

