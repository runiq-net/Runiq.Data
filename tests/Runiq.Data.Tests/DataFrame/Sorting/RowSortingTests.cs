namespace Runiq.Data.Tests.DataFrame.Sorting;

/// <summary>
/// Verifies mutable row sorting behavior through the Rows facade.
/// </summary>
public sealed class RowSortingTests
{
    /// <summary>
    /// Verifies that Rows.SortBy orders integer values from smallest to largest.
    /// </summary>
    [Fact]
    public void RowsSortBy_WithIntegerColumn_SortsAscending()
    {
        // Verifies that integer row ordering follows natural numeric order.
        var df = CreatePeopleDataFrame();

        df.Rows.SortBy("Age");

        Assert.Equal(new[] { 25, 30, 41 }, Ages(df));
        Assert.Equal(new[] { "Ayse", "Ali", "Mehmet" }, Names(df));
    }

    /// <summary>
    /// Verifies that Rows.SortByDescending orders integer values from largest to smallest.
    /// </summary>
    [Fact]
    public void RowsSortByDescending_WithIntegerColumn_SortsDescending()
    {
        // Verifies that descending integer ordering reverses natural numeric order.
        var df = CreatePeopleDataFrame();

        df.Rows.SortByDescending("Age");

        Assert.Equal(new[] { 41, 30, 25 }, Ages(df));
        Assert.Equal(new[] { "Mehmet", "Ali", "Ayse" }, Names(df));
    }

    /// <summary>
    /// Verifies that Rows.SortBy orders decimal values from smallest to largest.
    /// </summary>
    [Fact]
    public void RowsSortBy_WithDecimalColumn_SortsAscending()
    {
        // Verifies that decimal row ordering follows natural numeric order.
        var df = CreatePeopleDataFrame();

        df.Rows.SortBy("Salary");

        Assert.Equal(new[] { 95000m, 120000m, 150000m }, Salaries(df));
        Assert.Equal(new[] { "Ayse", "Ali", "Mehmet" }, Names(df));
    }

    /// <summary>
    /// Verifies that Rows.SortByDescending orders decimal values from largest to smallest.
    /// </summary>
    [Fact]
    public void RowsSortByDescending_WithDecimalColumn_SortsDescending()
    {
        // Verifies that descending decimal ordering reverses natural numeric order.
        var df = CreatePeopleDataFrame();

        df.Rows.SortByDescending("Salary");

        Assert.Equal(new[] { 150000m, 120000m, 95000m }, Salaries(df));
        Assert.Equal(new[] { "Mehmet", "Ali", "Ayse" }, Names(df));
    }

    /// <summary>
    /// Verifies that Rows.SortBy uses default .NET string comparison.
    /// </summary>
    [Fact]
    public void RowsSortBy_WithStringColumn_SortsAscendingUsingDefaultDotNetBehavior()
    {
        // Verifies that string sorting follows String.CompareTo behavior without custom culture options.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "bob", "Alice", "carol" },
            Age = new[] { 2, 1, 3 }
        });

        df.Rows.SortBy("Name");

        Assert.Equal(new[] { "Alice", "bob", "carol" }, Names(df));
        Assert.Equal(new[] { 1, 2, 3 }, Ages(df));
    }

    /// <summary>
    /// Verifies that Rows.SortBy preserves row count.
    /// </summary>
    [Fact]
    public void RowsSortBy_DoesNotChangeRowsCount()
    {
        // Verifies that sorting reorders rows without adding or removing them.
        var df = CreatePeopleDataFrame();
        var rowCount = df.Rows.Count();

        df.Rows.SortBy("Age");

        Assert.Equal(rowCount, df.Rows.Count());
    }

    /// <summary>
    /// Verifies that Rows.SortBy preserves column count.
    /// </summary>
    [Fact]
    public void RowsSortBy_DoesNotChangeColumnsCount()
    {
        // Verifies that sorting rows does not add or remove columns.
        var df = CreatePeopleDataFrame();
        var columnCount = df.Columns.Count();

        df.Rows.SortBy("Age");

        Assert.Equal(columnCount, df.Columns.Count());
    }

    /// <summary>
    /// Verifies that Rows.SortBy preserves schema and column order.
    /// </summary>
    [Fact]
    public void RowsSortBy_PreservesSchemaAndColumnOrder()
    {
        // Verifies that sorting rows leaves column metadata unchanged.
        var df = CreatePeopleDataFrame();
        var schema = df.Schema;

        df.Rows.SortBy("Age");

        Assert.Same(schema, df.Schema);
        Assert.Equal(new[] { "Name", "Age", "Salary", "IsActive" }, ColumnNames(df));
        Assert.Equal(new[] { "Name", "Age", "Salary", "IsActive" }, SchemaNames(df));
        Assert.Equal(typeof(string), df.Schema.GetColumn("Name").DataType);
        Assert.Equal(typeof(int), df.Schema.GetColumn("Age").DataType);
        Assert.Equal(typeof(decimal), df.Schema.GetColumn("Salary").DataType);
        Assert.Equal(typeof(bool), df.Schema.GetColumn("IsActive").DataType);
    }

    /// <summary>
    /// Verifies that Rows.SortBy keeps values from the same source row together.
    /// </summary>
    [Fact]
    public void RowsSortBy_KeepsRowValuesTogetherAcrossColumns()
    {
        // Verifies that every column is reordered by the same row indexes.
        var df = CreatePeopleDataFrame();

        df.Rows.SortBy("Age");

        Assert.Equal("Ayse", df.GetRow(0)["Name"]);
        Assert.Equal(25, df.GetRow(0)["Age"]);
        Assert.Equal(95000m, df.GetRow(0)["Salary"]);
        Assert.Equal(true, df.GetRow(0)["IsActive"]);
        Assert.Equal("Mehmet", df.GetRow(2)["Name"]);
        Assert.Equal(41, df.GetRow(2)["Age"]);
        Assert.Equal(150000m, df.GetRow(2)["Salary"]);
        Assert.Equal(false, df.GetRow(2)["IsActive"]);
    }

    /// <summary>
    /// Verifies that Rows.SortByDescending keeps values from the same source row together.
    /// </summary>
    [Fact]
    public void RowsSortByDescending_KeepsRowValuesTogetherAcrossColumns()
    {
        // Verifies that descending sort applies the same reordered indexes to every column.
        var df = CreatePeopleDataFrame();

        df.Rows.SortByDescending("Salary");

        Assert.Equal("Mehmet", df.GetRow(0)["Name"]);
        Assert.Equal(41, df.GetRow(0)["Age"]);
        Assert.Equal(150000m, df.GetRow(0)["Salary"]);
        Assert.Equal(false, df.GetRow(0)["IsActive"]);
        Assert.Equal("Ayse", df.GetRow(2)["Name"]);
        Assert.Equal(25, df.GetRow(2)["Age"]);
        Assert.Equal(95000m, df.GetRow(2)["Salary"]);
        Assert.Equal(true, df.GetRow(2)["IsActive"]);
    }

    /// <summary>
    /// Verifies that Rows.SortBy rejects missing columns.
    /// </summary>
    [Fact]
    public void RowsSortBy_WithMissingColumn_ThrowsKeyNotFoundException()
    {
        // Verifies that invalid sort columns fail fast instead of being ignored.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<KeyNotFoundException>(() => df.Rows.SortBy("MissingColumn"));

        Assert.Contains("MissingColumn", exception.Message);
    }

    /// <summary>
    /// Verifies that Rows.SortBy rejects invalid column names.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void RowsSortBy_WithNullEmptyOrWhitespaceColumnName_Throws(string? columnName)
    {
        // Verifies that the sort column name must be a meaningful value.
        var df = CreatePeopleDataFrame();

        if (columnName is null)
        {
            Assert.Throws<ArgumentNullException>(() => df.Rows.SortBy(columnName!));
        }
        else
        {
            Assert.Throws<ArgumentException>(() => df.Rows.SortBy(columnName));
        }
    }

    /// <summary>
    /// Verifies that Rows.SortByDescending rejects missing columns.
    /// </summary>
    [Fact]
    public void RowsSortByDescending_WithMissingColumn_ThrowsKeyNotFoundException()
    {
        // Verifies that descending sort keeps the same missing-column validation.
        var df = CreatePeopleDataFrame();

        var exception = Assert.Throws<KeyNotFoundException>(() => df.Rows.SortByDescending("MissingColumn"));

        Assert.Contains("MissingColumn", exception.Message);
    }

    /// <summary>
    /// Verifies that an invalid sort operation leaves the DataFrame unchanged.
    /// </summary>
    [Fact]
    public void RowsSortBy_WithInvalidColumn_DoesNotMutateDataFrame()
    {
        // Verifies that failed sorting does not change rows, columns, schema, or values.
        var df = CreatePeopleDataFrame();
        var schema = df.Schema;
        var rowCount = df.Rows.Count();
        var columnCount = df.Columns.Count();
        var names = Names(df);
        var ages = Ages(df);
        var salaries = Salaries(df);
        var columnNames = ColumnNames(df);
        var schemaNames = SchemaNames(df);

        Assert.Throws<KeyNotFoundException>(() => df.Rows.SortBy("MissingColumn"));

        Assert.Same(schema, df.Schema);
        Assert.Equal(rowCount, df.Rows.Count());
        Assert.Equal(columnCount, df.Columns.Count());
        Assert.Equal(names, Names(df));
        Assert.Equal(ages, Ages(df));
        Assert.Equal(salaries, Salaries(df));
        Assert.Equal(columnNames, ColumnNames(df));
        Assert.Equal(schemaNames, SchemaNames(df));
    }

    /// <summary>
    /// Verifies that Rows.SortBy rejects null values in the target column.
    /// </summary>
    [Fact]
    public void RowsSortBy_WithNullValuesInSortColumn_ThrowsAndDoesNotMutateDataFrame()
    {
        // Verifies that null sort values fail fast without changing existing row order.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse", "Mehmet" },
            NullableScore = new int?[] { 30, null, 41 },
            Department = new[] { "Sales", "Ops", "Finance" }
        });
        var schema = df.Schema;
        var rowCount = df.Rows.Count();
        var columnCount = df.Columns.Count();
        var names = Names(df);
        var nullableScores = NullableScores(df);
        var departments = Departments(df);
        var columnNames = ColumnNames(df);
        var schemaNames = SchemaNames(df);

        var exception = Assert.Throws<ArgumentException>(() => df.Rows.SortBy("NullableScore"));

        Assert.Contains("NullableScore", exception.Message);
        Assert.Contains("null", exception.Message);
        Assert.Same(schema, df.Schema);
        Assert.Equal(rowCount, df.Rows.Count());
        Assert.Equal(columnCount, df.Columns.Count());
        Assert.Equal(names, Names(df));
        Assert.Equal(nullableScores, NullableScores(df));
        Assert.Equal(departments, Departments(df));
        Assert.Equal(columnNames, ColumnNames(df));
        Assert.Equal(schemaNames, SchemaNames(df));
    }

    /// <summary>
    /// Verifies that Rows.SortBy rejects values that cannot be compared.
    /// </summary>
    [Fact]
    public void RowsSortBy_WithNonComparableValuesInSortColumn_ThrowsAndDoesNotMutateDataFrame()
    {
        // Verifies that unsupported sort values fail fast without converting them to strings.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse", "Mehmet" },
            UnsupportedColumn = new[] { new SortPayload(30), new SortPayload(25), new SortPayload(41) },
            Department = new[] { "Sales", "Ops", "Finance" }
        });
        var schema = df.Schema;
        var rowCount = df.Rows.Count();
        var columnCount = df.Columns.Count();
        var names = Names(df);
        var payloadValues = SortPayloadValues(df);
        var departments = Departments(df);
        var columnNames = ColumnNames(df);
        var schemaNames = SchemaNames(df);

        var exception = Assert.Throws<ArgumentException>(() => df.Rows.SortBy("UnsupportedColumn"));

        Assert.Contains("UnsupportedColumn", exception.Message);
        Assert.Contains("cannot be compared", exception.Message);
        Assert.Same(schema, df.Schema);
        Assert.Equal(rowCount, df.Rows.Count());
        Assert.Equal(columnCount, df.Columns.Count());
        Assert.Equal(names, Names(df));
        Assert.Equal(payloadValues, SortPayloadValues(df));
        Assert.Equal(departments, Departments(df));
        Assert.Equal(columnNames, ColumnNames(df));
        Assert.Equal(schemaNames, SchemaNames(df));
    }

    /// <summary>
    /// Verifies that sorting an empty DataFrame preserves schema.
    /// </summary>
    [Fact]
    public void RowsSortBy_WithEmptyDataFrameAndExistingColumn_PreservesSchemaAndDoesNotThrow()
    {
        // Verifies that a zero-row DataFrame can be sorted when the column exists.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = Array.Empty<string>(),
            Age = Array.Empty<int>()
        });
        var schema = df.Schema;

        df.Rows.SortBy("Age");

        Assert.Same(schema, df.Schema);
        Assert.Equal(0, df.Rows.Count());
        Assert.Equal(2, df.Columns.Count());
        Assert.Equal(new[] { "Name", "Age" }, ColumnNames(df));
    }

    /// <summary>
    /// Verifies that sorting a single-row DataFrame leaves values unchanged.
    /// </summary>
    [Fact]
    public void RowsSortBy_WithSingleRowDataFrame_LeavesRowUnchanged()
    {
        // Verifies that sorting one row is a no-op for row values.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali" },
            Age = new[] { 30 }
        });

        df.Rows.SortBy("Age");

        Assert.Equal(1, df.Rows.Count());
        Assert.Equal("Ali", df.GetRow(0)["Name"]);
        Assert.Equal(30, df.GetRow(0)["Age"]);
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
        return Enumerable.Range(0, df.Rows.Count())
            .Select(index => (string)df["Name"].GetValue(index)!)
            .ToArray();
    }

    private static int[] Ages(global::Runiq.Data.DataFrame df)
    {
        return Enumerable.Range(0, df.Rows.Count())
            .Select(index => (int)df["Age"].GetValue(index)!)
            .ToArray();
    }

    private static decimal[] Salaries(global::Runiq.Data.DataFrame df)
    {
        return Enumerable.Range(0, df.Rows.Count())
            .Select(index => (decimal)df["Salary"].GetValue(index)!)
            .ToArray();
    }

    private static int?[] NullableScores(global::Runiq.Data.DataFrame df)
    {
        return Enumerable.Range(0, df.Rows.Count())
            .Select(index => (int?)df["NullableScore"].GetValue(index))
            .ToArray();
    }

    private static string[] Departments(global::Runiq.Data.DataFrame df)
    {
        return Enumerable.Range(0, df.Rows.Count())
            .Select(index => (string)df["Department"].GetValue(index)!)
            .ToArray();
    }

    private static int[] SortPayloadValues(global::Runiq.Data.DataFrame df)
    {
        return Enumerable.Range(0, df.Rows.Count())
            .Select(index => ((SortPayload)df["UnsupportedColumn"].GetValue(index)!).Value)
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

    private sealed record SortPayload(int Value);
}
