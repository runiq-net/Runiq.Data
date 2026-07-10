namespace Runiq.Data.Tests.DataFrame;

/// <summary>
/// Verifies DataFrame-level aggregation behavior.
/// </summary>
public sealed class DataFrameAggregationTests
{
    /// <summary>
    /// Verifies that Sum follows the declared numeric result contract.
    /// </summary>
    [Theory]
    [MemberData(nameof(SumCases))]
    public void Sum_WithNumericColumn_ReturnsExpectedValueAndType(Array values, object expected, Type expectedType)
    {
        // Verifies both the arithmetic result and the public object result type contract.
        var df = CreateSingleColumnDataFrame(values);

        var result = df.Sum("Value");

        Assert.IsType(expectedType, result);
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Verifies that Sum handles negative values.
    /// </summary>
    [Fact]
    public void Sum_WithNegativeValues_ReturnsSignedTotal()
    {
        // Verifies that signed numeric values are added without absolute-value behavior.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { -10, 5, -2 } });

        Assert.Equal(-7, df.Sum("Value"));
    }

    /// <summary>
    /// Verifies that Sum handles a single row.
    /// </summary>
    [Fact]
    public void Sum_WithSingleRow_ReturnsOnlyValue()
    {
        // Verifies that one-row aggregation does not require a second value.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { 42L } });

        Assert.Equal(42L, df.Sum("Value"));
    }

    /// <summary>
    /// Verifies that Sum uses checked integer arithmetic.
    /// </summary>
    [Fact]
    public void Sum_WithIntegerOverflow_ThrowsOverflowException()
    {
        // Verifies fail-fast overflow instead of silent integer wrap-around.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { int.MaxValue, 1 } });

        Assert.Throws<OverflowException>(() => df.Sum("Value"));
    }

    /// <summary>
    /// Verifies that Sum uses checked arithmetic for every exact numeric result type.
    /// </summary>
    [Theory]
    [MemberData(nameof(SumOverflowCases))]
    public void Sum_WithExactNumericOverflow_ThrowsOverflowException(Array values)
    {
        // Verifies unsigned, long, ulong, and decimal totals fail fast instead of wrapping or converting through double.
        var df = CreateSingleColumnDataFrame(values);

        Assert.Throws<OverflowException>(() => df.Sum("Value"));
    }

    /// <summary>
    /// Verifies that Sum rejects non-numeric columns.
    /// </summary>
    [Fact]
    public void Sum_WithNonNumericColumn_ThrowsArgumentException()
    {
        // Verifies that text columns are not implicitly converted for numeric aggregation.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { "10", "20" } });

        var exception = Assert.Throws<ArgumentException>(() => df.Sum("Value"));

        Assert.Contains("not numeric", exception.Message);
    }

    /// <summary>
    /// Verifies that Sum and Average reject every supported non-numeric category.
    /// </summary>
    [Theory]
    [MemberData(nameof(NumericAggregationActions))]
    public void NumericAggregation_WithNonNumericColumn_ThrowsArgumentException(Action<global::Runiq.Data.DataFrame, string> aggregate)
    {
        // Verifies numeric validation uses the declared schema type, not the first runtime value.
        foreach (var df in NonNumericFrames())
        {
            Assert.Throws<ArgumentException>(() => aggregate(df, "Value"));
        }
    }

    /// <summary>
    /// Verifies that Sum rejects null values.
    /// </summary>
    [Fact]
    public void Sum_WithNullValue_ThrowsArgumentException()
    {
        // Verifies fail-fast null handling instead of skip-null or zero-fill behavior.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new int?[] { 10, null, 20 } });

        var exception = Assert.Throws<ArgumentException>(() => df.Sum("Value"));

        Assert.Contains("null", exception.Message);
    }

    /// <summary>
    /// Verifies that Sum rejects empty columns.
    /// </summary>
    [Fact]
    public void Sum_WithEmptyDataFrame_ThrowsArgumentException()
    {
        // Verifies that an undefined empty sum does not return a default zero.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = Array.Empty<int>() });

        Assert.Throws<ArgumentException>(() => df.Sum("Value"));
    }

    /// <summary>
    /// Verifies that Sum does not mutate source state.
    /// </summary>
    [Fact]
    public void Sum_DoesNotMutateDataFrame()
    {
        // Verifies aggregation leaves rows, columns, schema, order, and values unchanged.
        var df = CreatePeopleDataFrame();
        var snapshot = Snapshot(df);

        Assert.Equal(75, df.Sum("Age"));

        AssertSnapshot(df, snapshot);
    }

    /// <summary>
    /// Verifies that Average returns double results for numeric columns.
    /// </summary>
    [Theory]
    [MemberData(nameof(AverageCases))]
    public void Average_WithNumericColumn_ReturnsExpectedDouble(Array values, double expected)
    {
        // Verifies the public double return value across integer, floating, and decimal columns.
        var df = CreateSingleColumnDataFrame(values);

        Assert.Equal(expected, df.Average("Value"), 6);
    }

    /// <summary>
    /// Verifies that Average handles mixed signed values.
    /// </summary>
    [Fact]
    public void Average_WithNegativeAndPositiveValues_ReturnsExpectedDouble()
    {
        // Verifies that signs are preserved during average calculation.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { -10, 20, 5 } });

        Assert.Equal(5d, df.Average("Value"), 6);
    }

    /// <summary>
    /// Verifies that Average handles a single row.
    /// </summary>
    [Fact]
    public void Average_WithSingleRow_ReturnsOnlyValueAsDouble()
    {
        // Verifies one-row average returns the value represented as double.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { 12.5m } });

        Assert.Equal(12.5d, df.Average("Value"), 6);
    }

    /// <summary>
    /// Verifies that Average rejects invalid inputs.
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidNumericAggregationFrames))]
    public void Average_WithInvalidNumericAggregationInput_ThrowsArgumentException(global::Runiq.Data.DataFrame df)
    {
        // Verifies non-numeric, null-containing, and empty columns fail fast for Average.
        Assert.Throws<ArgumentException>(() => df.Average("Value"));
    }

    /// <summary>
    /// Verifies that Average does not mutate source state.
    /// </summary>
    [Fact]
    public void Average_DoesNotMutateDataFrame()
    {
        // Verifies average calculation is read-only against DataFrame state.
        var df = CreatePeopleDataFrame();
        var snapshot = Snapshot(df);

        Assert.Equal(25d, df.Average("Age"), 6);

        AssertSnapshot(df, snapshot);
    }

    /// <summary>
    /// Verifies that Min returns the first minimum comparable value.
    /// </summary>
    [Theory]
    [MemberData(nameof(MinCases))]
    public void Min_WithComparableColumn_ReturnsExpectedValue(Array values, object expected)
    {
        // Verifies natural comparison for numeric, string, and DateTime columns.
        var df = CreateSingleColumnDataFrame(values);

        Assert.Equal(expected, df.Min("Value"));
    }

    /// <summary>
    /// Verifies that Min handles negative values.
    /// </summary>
    [Fact]
    public void Min_WithNegativeValues_ReturnsSmallestValue()
    {
        // Verifies signed comparison keeps the most negative value as minimum.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { -3, -10, 4 } });

        Assert.Equal(-10, df.Min("Value"));
    }

    /// <summary>
    /// Verifies that Min handles a single row.
    /// </summary>
    [Fact]
    public void Min_WithSingleRow_ReturnsOnlyValue()
    {
        // Verifies one-row minimum returns the single stored value.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { "only" } });

        Assert.Equal("only", df.Min("Value"));
    }

    /// <summary>
    /// Verifies that Min rejects null values.
    /// </summary>
    [Fact]
    public void Min_WithNullValue_ThrowsArgumentException()
    {
        // Verifies comparable aggregation does not skip null values.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new int?[] { 1, null, 2 } });

        Assert.Throws<ArgumentException>(() => df.Min("Value"));
    }

    /// <summary>
    /// Verifies that Min rejects empty columns.
    /// </summary>
    [Fact]
    public void Min_WithEmptyDataFrame_ThrowsArgumentException()
    {
        // Verifies empty minimum is not represented as null.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = Array.Empty<int>() });

        Assert.Throws<ArgumentException>(() => df.Min("Value"));
    }

    /// <summary>
    /// Verifies that Min rejects non-comparable values.
    /// </summary>
    [Fact]
    public void Min_WithNonComparableRuntimeValue_ThrowsArgumentException()
    {
        // Verifies unsupported runtime values fail instead of using string conversion.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { new Payload(1), new Payload(2) } });

        Assert.Throws<ArgumentException>(() => df.Min("Value"));
    }

    /// <summary>
    /// Verifies that Min does not mutate source state.
    /// </summary>
    [Fact]
    public void Min_DoesNotMutateDataFrame()
    {
        // Verifies minimum calculation is read-only against DataFrame state.
        var df = CreatePeopleDataFrame();
        var snapshot = Snapshot(df);

        Assert.Equal(20, df.Min("Age"));

        AssertSnapshot(df, snapshot);
    }

    /// <summary>
    /// Verifies that Max returns the first maximum comparable value.
    /// </summary>
    [Theory]
    [MemberData(nameof(MaxCases))]
    public void Max_WithComparableColumn_ReturnsExpectedValue(Array values, object expected)
    {
        // Verifies natural comparison for numeric, string, and DateTime columns.
        var df = CreateSingleColumnDataFrame(values);

        Assert.Equal(expected, df.Max("Value"));
    }

    /// <summary>
    /// Verifies that Max handles negative values.
    /// </summary>
    [Fact]
    public void Max_WithNegativeValues_ReturnsLargestValue()
    {
        // Verifies signed comparison selects the value closest to positive infinity.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { -3, -10, -1 } });

        Assert.Equal(-1, df.Max("Value"));
    }

    /// <summary>
    /// Verifies that Max handles a single row.
    /// </summary>
    [Fact]
    public void Max_WithSingleRow_ReturnsOnlyValue()
    {
        // Verifies one-row maximum returns the single stored value.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { new DateTime(2024, 1, 1) } });

        Assert.Equal(new DateTime(2024, 1, 1), df.Max("Value"));
    }

    /// <summary>
    /// Verifies that Max rejects null values.
    /// </summary>
    [Fact]
    public void Max_WithNullValue_ThrowsArgumentException()
    {
        // Verifies comparable aggregation does not skip null values.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new int?[] { 1, null, 2 } });

        Assert.Throws<ArgumentException>(() => df.Max("Value"));
    }

    /// <summary>
    /// Verifies that Max rejects empty columns.
    /// </summary>
    [Fact]
    public void Max_WithEmptyDataFrame_ThrowsArgumentException()
    {
        // Verifies empty maximum is not represented as null.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = Array.Empty<int>() });

        Assert.Throws<ArgumentException>(() => df.Max("Value"));
    }

    /// <summary>
    /// Verifies that Max rejects non-comparable values.
    /// </summary>
    [Fact]
    public void Max_WithNonComparableRuntimeValue_ThrowsArgumentException()
    {
        // Verifies unsupported runtime values fail instead of using string conversion.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { new Payload(1), new Payload(2) } });

        Assert.Throws<ArgumentException>(() => df.Max("Value"));
    }

    /// <summary>
    /// Verifies that Max does not mutate source state.
    /// </summary>
    [Fact]
    public void Max_DoesNotMutateDataFrame()
    {
        // Verifies maximum calculation is read-only against DataFrame state.
        var df = CreatePeopleDataFrame();
        var snapshot = Snapshot(df);

        Assert.Equal(30, df.Max("Age"));

        AssertSnapshot(df, snapshot);
    }

    /// <summary>
    /// Verifies common column name validation for every aggregation method.
    /// </summary>
    [Theory]
    [MemberData(nameof(AggregationActions))]
    public void Aggregation_WithInvalidColumnName_ThrowsExpectedException(Action<global::Runiq.Data.DataFrame, string> aggregate)
    {
        // Verifies null, empty, whitespace, and missing column validation is shared by the public APIs.
        var df = CreatePeopleDataFrame();

        Assert.Throws<ArgumentNullException>(() => aggregate(df, null!));
        Assert.Throws<ArgumentException>(() => aggregate(df, ""));
        Assert.Throws<ArgumentException>(() => aggregate(df, " "));
        Assert.Throws<KeyNotFoundException>(() => aggregate(df, "Missing"));
    }

    /// <summary>
    /// Verifies that failed validation leaves the source DataFrame unchanged.
    /// </summary>
    [Theory]
    [MemberData(nameof(AggregationActions))]
    public void Aggregation_WhenValidationFails_DoesNotMutateDataFrame(Action<global::Runiq.Data.DataFrame, string> aggregate)
    {
        // Verifies failure preserves row count, column count, schema, order, and values.
        var df = CreatePeopleDataFrame();
        var snapshot = Snapshot(df);

        Assert.Throws<KeyNotFoundException>(() => aggregate(df, "Missing"));

        AssertSnapshot(df, snapshot);
    }

    /// <summary>
    /// Verifies that every aggregation method rejects null values.
    /// </summary>
    [Theory]
    [MemberData(nameof(AggregationActions))]
    public void Aggregation_WithNullValue_ThrowsArgumentException(Action<global::Runiq.Data.DataFrame, string> aggregate)
    {
        // Verifies fail-fast null behavior is consistent across numeric and comparable aggregations.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new int?[] { 1, null, 2 } });

        Assert.Throws<ArgumentException>(() => aggregate(df, "Value"));
    }

    /// <summary>
    /// Verifies that every aggregation method rejects empty columns.
    /// </summary>
    [Theory]
    [MemberData(nameof(AggregationActions))]
    public void Aggregation_WithEmptyDataFrame_ThrowsArgumentException(Action<global::Runiq.Data.DataFrame, string> aggregate)
    {
        // Verifies empty aggregation never returns default zero or null results.
        var df = global::Runiq.Data.DataFrame.Create(new { Value = Array.Empty<int>() });

        Assert.Throws<ArgumentException>(() => aggregate(df, "Value"));
    }

    /// <summary>
    /// Verifies that no DataFrame Count API is introduced by aggregation work.
    /// </summary>
    [Fact]
    public void DataFrame_DoesNotExposeCountMethod()
    {
        // Verifies row count remains available through df.Rows.Count().
        var countMethod = typeof(global::Runiq.Data.DataFrame).GetMethod("Count", Type.EmptyTypes);
        var df = CreatePeopleDataFrame();

        Assert.Null(countMethod);
        Assert.Equal(3, df.Rows.Count());
    }

    public static TheoryData<Array, object, Type> SumCases()
    {
        return new TheoryData<Array, object, Type>
        {
            { new[] { 1, 2, 3 }, 6, typeof(int) },
            { new[] { 1L, 2L, 3L }, 6L, typeof(long) },
            { new[] { 1f, 2f, 3f }, 6f, typeof(float) },
            { new[] { 1d, 2d, 3d }, 6d, typeof(double) },
            { new[] { 1m, 2m, 3m }, 6m, typeof(decimal) },
            { new byte[] { 1, 2, 3 }, 6, typeof(int) },
            { new sbyte[] { 1, 2, 3 }, 6, typeof(int) },
            { new short[] { 1, 2, 3 }, 6, typeof(int) },
            { new ushort[] { 1, 2, 3 }, 6, typeof(int) },
            { new uint[] { 1, 2, 3 }, 6u, typeof(uint) },
            { new ulong[] { 1, 2, 3 }, 6UL, typeof(ulong) }
        };
    }

    public static TheoryData<Array> SumOverflowCases()
    {
        return new TheoryData<Array>
        {
            new uint[] { uint.MaxValue, 1u },
            new[] { long.MaxValue, 1L },
            new ulong[] { ulong.MaxValue, 1UL },
            new[] { decimal.MaxValue, 1m }
        };
    }

    public static TheoryData<Array, double> AverageCases()
    {
        return new TheoryData<Array, double>
        {
            { new[] { 1, 2 }, 1.5d },
            { new[] { 2L, 4L, 6L }, 4d },
            { new[] { 1f, 2f, 3f }, 2d },
            { new[] { 1d, 2d, 3d }, 2d },
            { new[] { 1m, 2m, 3m }, 2d },
            { new byte[] { 1, 2, 3 }, 2d },
            { new sbyte[] { 1, 2, 3 }, 2d },
            { new short[] { 1, 2, 3 }, 2d },
            { new ushort[] { 1, 2, 3 }, 2d },
            { new uint[] { 1, 2, 3 }, 2d },
            { new ulong[] { 1, 2, 3 }, 2d }
        };
    }

    public static TheoryData<Array, object> MinCases()
    {
        return new TheoryData<Array, object>
        {
            { new[] { 3, 1, 2 }, 1 },
            { new[] { 3m, 1m, 2m }, 1m },
            { new[] { "bob", "Alice", "carol" }, "Alice" },
            { new[] { new DateTime(2024, 5, 1), new DateTime(2023, 1, 1) }, new DateTime(2023, 1, 1) }
        };
    }

    public static TheoryData<Array, object> MaxCases()
    {
        return new TheoryData<Array, object>
        {
            { new[] { 3, 1, 2 }, 3 },
            { new[] { 3m, 1m, 2m }, 3m },
            { new[] { "bob", "Alice", "carol" }, "carol" },
            { new[] { new DateTime(2024, 5, 1), new DateTime(2023, 1, 1) }, new DateTime(2024, 5, 1) }
        };
    }

    public static TheoryData<global::Runiq.Data.DataFrame> InvalidNumericAggregationFrames()
    {
        return new TheoryData<global::Runiq.Data.DataFrame>
        {
            global::Runiq.Data.DataFrame.Create(new { Value = new[] { "10", "20" } }),
            global::Runiq.Data.DataFrame.Create(new { Value = new int?[] { 10, null, 20 } }),
            global::Runiq.Data.DataFrame.Create(new { Value = Array.Empty<int>() })
        };
    }

    public static TheoryData<Action<global::Runiq.Data.DataFrame, string>> AggregationActions()
    {
        return new TheoryData<Action<global::Runiq.Data.DataFrame, string>>
        {
            (df, columnName) => df.Sum(columnName),
            (df, columnName) => df.Average(columnName),
            (df, columnName) => df.Min(columnName),
            (df, columnName) => df.Max(columnName)
        };
    }

    public static TheoryData<Action<global::Runiq.Data.DataFrame, string>> NumericAggregationActions()
    {
        return new TheoryData<Action<global::Runiq.Data.DataFrame, string>>
        {
            (df, columnName) => df.Sum(columnName),
            (df, columnName) => df.Average(columnName)
        };
    }

    private static IEnumerable<global::Runiq.Data.DataFrame> NonNumericFrames()
    {
        yield return global::Runiq.Data.DataFrame.Create(new { Value = new[] { "10", "20" } });
        yield return global::Runiq.Data.DataFrame.Create(new { Value = new[] { new DateTime(2024, 1, 1), new DateTime(2024, 1, 2) } });
        yield return global::Runiq.Data.DataFrame.Create(new { Value = new[] { true, false } });
        yield return global::Runiq.Data.DataFrame.Create(new { Value = new[] { new Payload(1), new Payload(2) } });
    }

    private static global::Runiq.Data.DataFrame CreateSingleColumnDataFrame(Array values)
    {
        return global::Runiq.Data.DataFrame.Create(new { Value = values });
    }

    private static global::Runiq.Data.DataFrame CreatePeopleDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse", "Mehmet" },
            Age = new[] { 30, 20, 25 },
            Salary = new[] { 120000m, 95000m, 150000m }
        });
    }

    private static FrameSnapshot Snapshot(global::Runiq.Data.DataFrame df)
    {
        return new FrameSnapshot(
            df.Rows.Count(),
            df.Columns.Count(),
            df.Schema,
            df.Columns.Select(static column => column.Name).ToArray(),
            df.Schema.Columns.Select(static column => column.Name).ToArray(),
            df.Columns.Select(static column => column.DataType).ToArray(),
            df.Columns.Select(column => Enumerable.Range(0, column.Count).Select(column.GetValue).ToArray()).ToArray());
    }

    private static void AssertSnapshot(global::Runiq.Data.DataFrame df, FrameSnapshot snapshot)
    {
        Assert.Equal(snapshot.RowCount, df.Rows.Count());
        Assert.Equal(snapshot.ColumnCount, df.Columns.Count());
        Assert.Same(snapshot.Schema, df.Schema);
        Assert.Equal(snapshot.ColumnNames, df.Columns.Select(static column => column.Name));
        Assert.Equal(snapshot.SchemaNames, df.Schema.Columns.Select(static column => column.Name));
        Assert.Equal(snapshot.ColumnTypes, df.Columns.Select(static column => column.DataType));

        var currentValues = df.Columns
            .Select(column => Enumerable.Range(0, column.Count).Select(column.GetValue).ToArray())
            .ToArray();
        Assert.Equal(snapshot.Values, currentValues);
    }

    private sealed record FrameSnapshot(
        int RowCount,
        int ColumnCount,
        global::Runiq.Data.Schema.DataFrameSchema Schema,
        string[] ColumnNames,
        string[] SchemaNames,
        Type[] ColumnTypes,
        object?[][] Values);

    private sealed record Payload(int Value);
}
