namespace Runiq.Data.Tests.DataFrame.Grouping;

/// <summary>
/// Verifies DataFrame-level GroupBy behavior and grouped aggregation results.
/// </summary>
public sealed class DataFrameGroupByTests
{
    /// <summary>
    /// Verifies that GroupBy validates key arguments before creating a grouped snapshot.
    /// </summary>
    [Fact]
    public void GroupBy_WithInvalidKeys_ThrowsExpectedExceptions()
    {
        // Verifies public key validation for null arrays, empty keys, missing keys, and duplicates.
        var df = CreateEmployeeDataFrame();

        Assert.Throws<ArgumentNullException>(() => df.GroupBy((string[])null!));
        Assert.Throws<ArgumentException>(() => df.GroupBy());
        Assert.Throws<ArgumentNullException>(() => df.GroupBy((string)null!));
        Assert.Throws<ArgumentException>(() => df.GroupBy(""));
        Assert.Throws<ArgumentException>(() => df.GroupBy(" "));
        Assert.Throws<KeyNotFoundException>(() => df.GroupBy("Missing"));
        Assert.Throws<ArgumentException>(() => df.GroupBy("Department", "Department"));
    }

    /// <summary>
    /// Verifies that failed GroupBy validation does not mutate the source DataFrame.
    /// </summary>
    [Fact]
    public void GroupBy_WhenValidationFails_DoesNotMutateDataFrame()
    {
        // Verifies invalid grouping leaves shape, schema, order, and values unchanged.
        var df = CreateEmployeeDataFrame();
        var snapshot = Snapshot(df);

        Assert.Throws<KeyNotFoundException>(() => df.GroupBy("Missing"));

        AssertSnapshot(df, snapshot);
    }

    /// <summary>
    /// Verifies single-key grouped Sum values, ordering, schema, and source immutability.
    /// </summary>
    [Fact]
    public void GroupBySum_WithSingleKey_ReturnsStableGroupedTotals()
    {
        // Verifies first-seen group order, result column naming, type contract, and projected shape.
        var df = CreateEmployeeDataFrame();
        var snapshot = Snapshot(df);

        var result = df.GroupBy("Department").Sum("Salary");

        Assert.Equal(new[] { "Department", "Salary_Sum" }, ColumnNames(result));
        Assert.Equal(new[] { typeof(string), typeof(int) }, ColumnTypes(result));
        Assert.Equal(new[] { "IT", "HR" }, Values<string>(result, "Department"));
        Assert.Equal(new[] { 250, 200 }, Values<int>(result, "Salary_Sum"));
        Assert.Equal(2, result.Columns.Count());
        AssertSnapshot(df, snapshot);
    }

    /// <summary>
    /// Verifies grouped Sum handles negative values and single-row groups.
    /// </summary>
    [Fact]
    public void GroupBySum_WithNegativeValuesAndSingleRowGroups_ReturnsExpectedTotals()
    {
        // Verifies each group is aggregated independently, including one-row groups.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "IT", "HR", "IT", "Ops" },
            Adjustment = new[] { -10, 5, 3, -7 }
        });

        var result = df.GroupBy("Department").Sum("Adjustment");

        Assert.Equal(new[] { "IT", "HR", "Ops" }, Values<string>(result, "Department"));
        Assert.Equal(new[] { -7, 5, -7 }, Values<int>(result, "Adjustment_Sum"));
    }

    /// <summary>
    /// Verifies grouped Sum preserves the FE-019 numeric result type contract.
    /// </summary>
    [Theory]
    [MemberData(nameof(GroupedSumTypeCases))]
    public void GroupBySum_WithNumericTypes_ReturnsExpectedSchemaType(Array values, Type expectedType, object expectedFirstTotal)
    {
        // Verifies grouped Sum does not convert exact numeric types through double.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "IT", "IT" },
            Value = values
        });

        var result = df.GroupBy("Department").Sum("Value");

        Assert.Equal(expectedType, result.Schema.GetColumn("Value_Sum").DataType);
        Assert.IsType(expectedType, result["Value_Sum"].GetValue(0)!);
        Assert.Equal(expectedFirstTotal, result["Value_Sum"].GetValue(0));
    }

    /// <summary>
    /// Verifies composite-key grouped Sum uses value-based equality and stable first-seen ordering.
    /// </summary>
    [Fact]
    public void GroupBySum_WithCompositeKey_ReturnsStableCompositeGroups()
    {
        // Verifies same composite values share a group while partial key differences create separate groups.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Country = new[] { "TR", "DE", "TR", "TR", "DE" },
            Department = new[] { "IT", "HR", "IT", "HR", "HR" },
            Salary = new[] { 100, 80, 150, 120, 20 }
        });

        var result = df.GroupBy("Country", "Department").Sum("Salary");

        Assert.Equal(new[] { "Country", "Department", "Salary_Sum" }, ColumnNames(result));
        Assert.Equal(new[] { "TR", "DE", "TR" }, Values<string>(result, "Country"));
        Assert.Equal(new[] { "IT", "HR", "HR" }, Values<string>(result, "Department"));
        Assert.Equal(new[] { 250, 100, 120 }, Values<int>(result, "Salary_Sum"));
    }

    /// <summary>
    /// Verifies grouped Average returns double values and avoids integer division.
    /// </summary>
    [Fact]
    public void GroupByAverage_WithIntegerValues_ReturnsDoubleAverages()
    {
        // Verifies uneven group sizes and non-integer averages are represented as double.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "IT", "HR", "IT", "HR", "HR" },
            Salary = new[] { 100, 80, 151, 120, 101 }
        });
        var snapshot = Snapshot(df);

        var result = df.GroupBy("Department").Average("Salary");

        Assert.Equal(new[] { "Department", "Salary_Average" }, ColumnNames(result));
        Assert.Equal(typeof(double), result.Schema.GetColumn("Salary_Average").DataType);
        Assert.Equal(125.5d, (double)result["Salary_Average"].GetValue(0)!, 6);
        Assert.Equal(100.333333d, (double)result["Salary_Average"].GetValue(1)!, 6);
        AssertSnapshot(df, snapshot);
    }

    /// <summary>
    /// Verifies grouped Average supports decimal inputs.
    /// </summary>
    [Fact]
    public void GroupByAverage_WithDecimalValues_ReturnsDoubleAverages()
    {
        // Verifies decimal source values produce double average result columns.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "IT", "IT", "HR" },
            Salary = new[] { 100.5m, 101.5m, 80m }
        });

        var result = df.GroupBy("Department").Average("Salary");

        Assert.Equal(typeof(double), result.Schema.GetColumn("Salary_Average").DataType);
        Assert.Equal(new[] { 101d, 80d }, Values<double>(result, "Salary_Average"));
    }

    /// <summary>
    /// Verifies grouped Min supports numeric, string, DateTime, and negative values.
    /// </summary>
    [Fact]
    public void GroupByMin_WithComparableValues_ReturnsMinimumValues()
    {
        // Verifies default .NET comparison is used without custom comparer behavior.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "IT", "HR", "IT", "HR" },
            Age = new[] { -2, 35, 30, 25 },
            Name = new[] { "bob", "Zeynep", "Alice", "Ayse" },
            CreatedAt = new[] { new DateTime(2024, 2, 1), new DateTime(2024, 3, 1), new DateTime(2024, 1, 1), new DateTime(2024, 4, 1) }
        });
        var snapshot = Snapshot(df);

        var ages = df.GroupBy("Department").Min("Age");
        var names = df.GroupBy("Department").Min("Name");
        var dates = df.GroupBy("Department").Min("CreatedAt");

        Assert.Equal(typeof(int), ages.Schema.GetColumn("Age_Min").DataType);
        Assert.Equal(new[] { -2, 25 }, Values<int>(ages, "Age_Min"));
        Assert.Equal(new[] { "Alice", "Ayse" }, Values<string>(names, "Name_Min"));
        Assert.Equal(new[] { new DateTime(2024, 1, 1), new DateTime(2024, 3, 1) }, Values<DateTime>(dates, "CreatedAt_Min"));
        AssertSnapshot(df, snapshot);
    }

    /// <summary>
    /// Verifies grouped Max supports numeric, string, DateTime, and negative values.
    /// </summary>
    [Fact]
    public void GroupByMax_WithComparableValues_ReturnsMaximumValues()
    {
        // Verifies maximum aggregation preserves result types and default comparison semantics.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "IT", "HR", "IT", "HR" },
            Score = new[] { -3, -10, -1, -7 },
            Name = new[] { "bob", "Zeynep", "Alice", "Ayse" },
            CreatedAt = new[] { new DateTime(2024, 2, 1), new DateTime(2024, 3, 1), new DateTime(2024, 1, 1), new DateTime(2024, 4, 1) }
        });
        var snapshot = Snapshot(df);

        var scores = df.GroupBy("Department").Max("Score");
        var names = df.GroupBy("Department").Max("Name");
        var dates = df.GroupBy("Department").Max("CreatedAt");

        Assert.Equal(typeof(int), scores.Schema.GetColumn("Score_Max").DataType);
        Assert.Equal(new[] { -1, -7 }, Values<int>(scores, "Score_Max"));
        Assert.Equal(new[] { "bob", "Zeynep" }, Values<string>(names, "Name_Max"));
        Assert.Equal(new[] { new DateTime(2024, 2, 1), new DateTime(2024, 4, 1) }, Values<DateTime>(dates, "CreatedAt_Max"));
        AssertSnapshot(df, snapshot);
    }

    /// <summary>
    /// Verifies grouping by the same column being aggregated is supported.
    /// </summary>
    [Fact]
    public void GroupByMin_WhenKeyAndAggregateColumnMatch_ReturnsKeyAndAggregateColumns()
    {
        // Verifies using the same source column as key and aggregate is not rejected.
        var df = global::Runiq.Data.DataFrame.Create(new { Age = new[] { 30, 25, 30 } });

        var result = df.GroupBy("Age").Min("Age");

        Assert.Equal(new[] { "Age", "Age_Min" }, ColumnNames(result));
        Assert.Equal(new[] { 30, 25 }, Values<int>(result, "Age"));
        Assert.Equal(new[] { 30, 25 }, Values<int>(result, "Age_Min"));
    }

    /// <summary>
    /// Verifies generated result column names cannot collide with group key columns.
    /// </summary>
    [Fact]
    public void GroupByAggregation_WithGeneratedResultColumnCollision_ThrowsArgumentException()
    {
        // Verifies collisions fail fast instead of silently renaming aggregate output.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Age_Min = new[] { "A", "B" },
            Age = new[] { 30, 25 }
        });

        Assert.Throws<ArgumentException>(() => df.GroupBy("Age_Min").Min("Age"));
    }

    /// <summary>
    /// Verifies generated result column names cannot collide with any composite group key column.
    /// </summary>
    [Fact]
    public void GroupByAggregation_WithCompositeKeyGeneratedResultColumnCollision_ThrowsArgumentException()
    {
        // Verifies collision validation checks every group key, not just the first one.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Country = new[] { "TR", "DE" },
            Salary_Sum = new[] { "IT", "HR" },
            Salary = new[] { 100, 80 }
        });

        Assert.Throws<ArgumentException>(() => df.GroupBy("Country", "Salary_Sum").Sum("Salary"));
    }

    /// <summary>
    /// Verifies grouped aggregations reject null group keys and null aggregate values.
    /// </summary>
    [Theory]
    [MemberData(nameof(GroupedAggregationActions))]
    public void GroupByAggregation_WithNullValues_ThrowsAndDoesNotMutateSource(Action<GroupedDataFrame, string> aggregate)
    {
        // Verifies fail-fast null behavior for keys and aggregate values across all grouped aggregations.
        var nullKey = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new string?[] { "IT", null },
            Value = new int?[] { 1, 2 }
        });
        var nullValue = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "IT", "HR" },
            Value = new int?[] { 1, null }
        });
        var keySnapshot = Snapshot(nullKey);
        var valueSnapshot = Snapshot(nullValue);

        Assert.Throws<ArgumentException>(() => aggregate(nullKey.GroupBy("Department"), "Value"));
        Assert.Throws<ArgumentException>(() => aggregate(nullValue.GroupBy("Department"), "Value"));
        AssertSnapshot(nullKey, keySnapshot);
        AssertSnapshot(nullValue, valueSnapshot);
    }

    /// <summary>
    /// Verifies grouped aggregations reject empty source snapshots.
    /// </summary>
    [Theory]
    [MemberData(nameof(GroupedAggregationActions))]
    public void GroupByAggregation_WithEmptyDataFrame_ThrowsArgumentException(Action<GroupedDataFrame, string> aggregate)
    {
        // Verifies empty grouping does not return an empty result DataFrame.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = Array.Empty<string>(),
            Value = Array.Empty<int>()
        });

        Assert.Throws<ArgumentException>(() => aggregate(df.GroupBy("Department"), "Value"));
    }

    /// <summary>
    /// Verifies grouped aggregation column validation for every public method.
    /// </summary>
    [Theory]
    [MemberData(nameof(GroupedAggregationActions))]
    public void GroupByAggregation_WithInvalidAggregateColumnName_ThrowsExpectedException(Action<GroupedDataFrame, string> aggregate)
    {
        // Verifies null, empty, whitespace, and missing aggregate columns fail consistently.
        var grouped = CreateEmployeeDataFrame().GroupBy("Department");

        Assert.Throws<ArgumentNullException>(() => aggregate(grouped, null!));
        Assert.Throws<ArgumentException>(() => aggregate(grouped, ""));
        Assert.Throws<ArgumentException>(() => aggregate(grouped, " "));
        Assert.Throws<KeyNotFoundException>(() => aggregate(grouped, "Missing"));
    }

    /// <summary>
    /// Verifies grouped aggregations reject invalid aggregate types and overflow.
    /// </summary>
    [Fact]
    public void GroupByAggregation_WithInvalidAggregateTypeOrOverflow_Throws()
    {
        // Verifies numeric, comparable, and checked arithmetic contracts remain enforced per group.
        var nonNumeric = global::Runiq.Data.DataFrame.Create(new { Department = new[] { "IT" }, Value = new[] { "text" } });
        var nonComparable = global::Runiq.Data.DataFrame.Create(new { Department = new[] { "IT" }, Value = new[] { new Payload(1) } });
        var overflow = global::Runiq.Data.DataFrame.Create(new { Department = new[] { "IT", "IT" }, Value = new[] { int.MaxValue, 1 } });

        Assert.Throws<ArgumentException>(() => nonNumeric.GroupBy("Department").Sum("Value"));
        Assert.Throws<ArgumentException>(() => nonNumeric.GroupBy("Department").Average("Value"));
        Assert.Throws<ArgumentException>(() => nonComparable.GroupBy("Department").Min("Value"));
        Assert.Throws<ArgumentException>(() => nonComparable.GroupBy("Department").Max("Value"));
        Assert.Throws<OverflowException>(() => overflow.GroupBy("Department").Sum("Value"));
    }

    /// <summary>
    /// Verifies grouped numeric aggregation rejects non-numeric declared column types.
    /// </summary>
    [Theory]
    [MemberData(nameof(GroupedNumericAggregationActions))]
    public void GroupByNumericAggregation_WithNonNumericColumns_ThrowsArgumentException(Action<GroupedDataFrame, string> aggregate)
    {
        // Verifies numeric validation rejects string, DateTime, bool, and custom object columns.
        foreach (var df in NonNumericFrames())
        {
            Assert.Throws<ArgumentException>(() => aggregate(df.GroupBy("Department"), "Value"));
        }
    }

    /// <summary>
    /// Verifies grouped Sum uses checked arithmetic for every exact numeric result type.
    /// </summary>
    [Theory]
    [MemberData(nameof(GroupedSumOverflowCases))]
    public void GroupBySum_WithExactNumericOverflow_ThrowsOverflowException(Array values)
    {
        // Verifies int, uint, long, ulong, and decimal overflow fail the whole grouped operation.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "IT", "IT" },
            Value = values
        });

        Assert.Throws<OverflowException>(() => df.GroupBy("Department").Sum("Value"));
    }

    /// <summary>
    /// Verifies GroupedDataFrame snapshots source state at GroupBy time.
    /// </summary>
    [Fact]
    public void GroupedDataFrame_AfterSourceMutation_UsesOriginalSnapshot()
    {
        // Verifies rows added after GroupBy are not included in later grouped aggregations.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "IT", "HR", "IT" },
            Salary = new[] { 100, 80, 150 }
        });
        var grouped = df.GroupBy("Department");

        df.Rows.Add(new { Department = "IT", Salary = 999 });
        var result = grouped.Sum("Salary");

        Assert.Equal(new[] { "IT", "HR" }, Values<string>(result, "Department"));
        Assert.Equal(new[] { 250, 80 }, Values<int>(result, "Salary_Sum"));
        Assert.Equal(4, df.Rows.Count());
    }

    /// <summary>
    /// Verifies one GroupedDataFrame instance can run multiple independent aggregations.
    /// </summary>
    [Fact]
    public void GroupedDataFrame_CanBeReusedForMultipleAggregations()
    {
        // Verifies one aggregation call does not consume or mutate grouped state.
        var grouped = CreateEmployeeDataFrame().GroupBy("Department");

        var sum = grouped.Sum("Salary");
        var average = grouped.Average("Salary");
        var min = grouped.Min("Age");
        var max = grouped.Max("Age");

        Assert.Equal(new[] { 250, 200 }, Values<int>(sum, "Salary_Sum"));
        Assert.Equal(new[] { 125d, 100d }, Values<double>(average, "Salary_Average"));
        Assert.Equal(new[] { 30, 25 }, Values<int>(min, "Age_Min"));
        Assert.Equal(new[] { 40, 35 }, Values<int>(max, "Age_Max"));
    }

    /// <summary>
    /// Verifies a failed grouped aggregation does not prevent later valid aggregations.
    /// </summary>
    [Fact]
    public void GroupedDataFrame_AfterFailedAggregation_CanBeReused()
    {
        // Verifies validation failures do not corrupt reusable grouped snapshot state.
        var grouped = CreateEmployeeDataFrame().GroupBy("Department");

        Assert.Throws<KeyNotFoundException>(() => grouped.Sum("Missing"));
        var result = grouped.Sum("Salary");

        Assert.Equal(new[] { 250, 200 }, Values<int>(result, "Salary_Sum"));
    }

    /// <summary>
    /// Verifies grouped result DataFrames are independent from grouped state and from each other.
    /// </summary>
    [Fact]
    public void GroupedDataFrame_AfterResultMutation_ReturnsIndependentResults()
    {
        // Verifies mutating one result does not affect later results from the same grouped snapshot.
        var grouped = CreateEmployeeDataFrame().GroupBy("Department");
        var first = grouped.Sum("Salary");

        first.Rows.Update(0, new { Department = "IT", Salary_Sum = 999 });
        var second = grouped.Sum("Salary");
        var average = grouped.Average("Salary");

        Assert.Equal(new[] { 999, 200 }, Values<int>(first, "Salary_Sum"));
        Assert.Equal(new[] { 250, 200 }, Values<int>(second, "Salary_Sum"));
        Assert.Equal(new[] { 125d, 100d }, Values<double>(average, "Salary_Average"));
    }

    public static TheoryData<Array, Type, object> GroupedSumTypeCases()
    {
        return new TheoryData<Array, Type, object>
        {
            { new byte[] { 1, 2 }, typeof(int), 3 },
            { new sbyte[] { 1, 2 }, typeof(int), 3 },
            { new short[] { 1, 2 }, typeof(int), 3 },
            { new ushort[] { 1, 2 }, typeof(int), 3 },
            { new[] { 1, 2 }, typeof(int), 3 },
            { new uint[] { 1, 2 }, typeof(uint), 3u },
            { new[] { 1L, 2L }, typeof(long), 3L },
            { new ulong[] { 1, 2 }, typeof(ulong), 3UL },
            { new[] { 1f, 2f }, typeof(float), 3f },
            { new[] { 1d, 2d }, typeof(double), 3d },
            { new[] { 1m, 2m }, typeof(decimal), 3m }
        };
    }

    public static TheoryData<Array> GroupedSumOverflowCases()
    {
        return new TheoryData<Array>
        {
            new[] { int.MaxValue, 1 },
            new uint[] { uint.MaxValue, 1u },
            new[] { long.MaxValue, 1L },
            new ulong[] { ulong.MaxValue, 1UL },
            new[] { decimal.MaxValue, 1m }
        };
    }

    public static TheoryData<Action<GroupedDataFrame, string>> GroupedAggregationActions()
    {
        return new TheoryData<Action<GroupedDataFrame, string>>
        {
            (grouped, columnName) => grouped.Sum(columnName),
            (grouped, columnName) => grouped.Average(columnName),
            (grouped, columnName) => grouped.Min(columnName),
            (grouped, columnName) => grouped.Max(columnName)
        };
    }

    public static TheoryData<Action<GroupedDataFrame, string>> GroupedNumericAggregationActions()
    {
        return new TheoryData<Action<GroupedDataFrame, string>>
        {
            (grouped, columnName) => grouped.Sum(columnName),
            (grouped, columnName) => grouped.Average(columnName)
        };
    }

    private static IEnumerable<global::Runiq.Data.DataFrame> NonNumericFrames()
    {
        yield return global::Runiq.Data.DataFrame.Create(new { Department = new[] { "IT" }, Value = new[] { "text" } });
        yield return global::Runiq.Data.DataFrame.Create(new { Department = new[] { "IT" }, Value = new[] { new DateTime(2024, 1, 1) } });
        yield return global::Runiq.Data.DataFrame.Create(new { Department = new[] { "IT" }, Value = new[] { true } });
        yield return global::Runiq.Data.DataFrame.Create(new { Department = new[] { "IT" }, Value = new[] { new Payload(1) } });
    }

    private static global::Runiq.Data.DataFrame CreateEmployeeDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "IT", "HR", "IT", "HR" },
            Employee = new[] { "Ali", "Ayse", "Mehmet", "Zeynep" },
            Salary = new[] { 100, 80, 150, 120 },
            Age = new[] { 30, 35, 40, 25 }
        });
    }

    private static string[] ColumnNames(global::Runiq.Data.DataFrame df)
    {
        return df.Columns.Select(static column => column.Name).ToArray();
    }

    private static Type[] ColumnTypes(global::Runiq.Data.DataFrame df)
    {
        return df.Columns.Select(static column => column.DataType).ToArray();
    }

    private static T[] Values<T>(global::Runiq.Data.DataFrame df, string columnName)
    {
        var column = df[columnName];
        return Enumerable.Range(0, df.Rows.Count())
            .Select(index => (T)column.GetValue(index)!)
            .ToArray();
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
