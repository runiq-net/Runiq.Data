namespace Runiq.Data.DataFrameTests.Grouping;

/// <summary>
/// Verifies grouped multi-aggregation builder behavior and result contracts.
/// </summary>
public sealed class GroupedDataFrameMultiAggregationTests
{
    /// <summary>
    /// Verifies builder validation rejects invalid configuration shapes before result creation.
    /// </summary>
    [Fact]
    public void Aggregate_WithInvalidBuilderConfiguration_ThrowsExpectedExceptions()
    {
        // Verifies null callbacks, empty configurations, invalid columns, and duplicate generated outputs fail fast.
        var grouped = CreateEmployeeDataFrame().GroupBy("Department");

        Assert.Throws<ArgumentNullException>(() => grouped.Aggregate(null!));
        Assert.Throws<ArgumentException>(() => grouped.Aggregate(_ => null!));
        Assert.Throws<ArgumentException>(() => grouped.Aggregate(aggregation => aggregation));
        Assert.Throws<ArgumentException>(() => grouped.Aggregate(aggregation => aggregation.For("Salary")));
        Assert.Throws<ArgumentNullException>(() => grouped.Aggregate(aggregation => aggregation.For(null!).Sum()));
        Assert.Throws<ArgumentException>(() => grouped.Aggregate(aggregation => aggregation.For("").Sum()));
        Assert.Throws<ArgumentException>(() => grouped.Aggregate(aggregation => aggregation.For(" ").Sum()));
        Assert.Throws<KeyNotFoundException>(() => grouped.Aggregate(aggregation => aggregation.For("Missing").Sum()));
        Assert.Throws<ArgumentException>(() => grouped.Aggregate(aggregation => aggregation.For("Salary").Sum().Sum()));
        Assert.Throws<ArgumentException>(() => grouped.Aggregate(aggregation => aggregation.For("Salary").Sum().For("Salary").Sum()));
    }

    /// <summary>
    /// Verifies the primary multi-aggregation scenario, including values, schema, order, and immutability.
    /// </summary>
    [Fact]
    public void Aggregate_WithSingleKey_ReturnsExpectedSummary()
    {
        // Verifies result columns follow builder order and only include keys plus declared aggregate outputs.
        var df = CreateEmployeeDataFrame();
        var snapshot = Snapshot(df);

        var result = df.GroupBy("Department").Aggregate(aggregation => aggregation
            .For("Salary").Sum().Average()
            .For("Age").Min().Max());

        Assert.NotSame(df, result);
        Assert.Equal(new[] { "Department", "Salary_Sum", "Salary_Average", "Age_Min", "Age_Max" }, ColumnNames(result));
        Assert.Equal(new[] { typeof(string), typeof(int), typeof(double), typeof(int), typeof(int) }, ColumnTypes(result));
        Assert.Equal(new[] { "IT", "HR" }, Values<string>(result, "Department"));
        Assert.Equal(new[] { 250, 200 }, Values<int>(result, "Salary_Sum"));
        Assert.Equal(new[] { 125d, 100d }, Values<double>(result, "Salary_Average"));
        Assert.Equal(new[] { 30, 25 }, Values<int>(result, "Age_Min"));
        Assert.Equal(new[] { 40, 35 }, Values<int>(result, "Age_Max"));
        Assert.Equal(2, result.Rows.Count());
        Assert.Equal(5, result.Columns.Count());
        Assert.False(result.HasColumn("Employee"));
        AssertSnapshot(df, snapshot);
    }

    /// <summary>
    /// Verifies declaration order is global across operations and repeated source column selections.
    /// </summary>
    [Fact]
    public void Aggregate_PreservesBuilderDeclarationOrder()
    {
        // Verifies no alphabetical or source-schema reordering is applied to aggregate outputs.
        var result = CreateEmployeeDataFrame().GroupBy("Department").Aggregate(aggregation => aggregation
            .For("Age").Max().Min()
            .For("Salary").Average().Sum());
        var repeatedColumnResult = CreateEmployeeDataFrame().GroupBy("Department").Aggregate(aggregation => aggregation
            .For("Salary").Sum()
            .For("Age").Min()
            .For("Salary").Average());

        Assert.Equal(new[] { "Department", "Age_Max", "Age_Min", "Salary_Average", "Salary_Sum" }, ColumnNames(result));
        Assert.Equal(new[] { "Department", "Salary_Sum", "Age_Min", "Salary_Average" }, ColumnNames(repeatedColumnResult));
        Assert.Equal(new[] { 250, 200 }, Values<int>(repeatedColumnResult, "Salary_Sum"));
        Assert.Equal(new[] { 30, 25 }, Values<int>(repeatedColumnResult, "Age_Min"));
        Assert.Equal(new[] { 125d, 100d }, Values<double>(repeatedColumnResult, "Salary_Average"));
    }

    /// <summary>
    /// Verifies composite grouping uses stable first-seen ordering and correct key equality.
    /// </summary>
    [Fact]
    public void Aggregate_WithCompositeKeys_ReturnsExpectedGroups()
    {
        // Verifies all aggregate outputs are aligned to the same composite group order.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Country = new[] { "TR", "DE", "TR", "TR", "DE" },
            Department = new[] { "IT", "HR", "IT", "HR", "HR" },
            Salary = new[] { 100, 80, 150, 120, 20 },
            Age = new[] { 30, 35, 40, 25, 45 }
        });

        var result = df.GroupBy("Country", "Department").Aggregate(aggregation => aggregation
            .For("Salary").Sum().Average()
            .For("Age").Min().Max());

        Assert.Equal(new[] { "Country", "Department", "Salary_Sum", "Salary_Average", "Age_Min", "Age_Max" }, ColumnNames(result));
        Assert.Equal(new[] { "TR", "DE", "TR" }, Values<string>(result, "Country"));
        Assert.Equal(new[] { "IT", "HR", "HR" }, Values<string>(result, "Department"));
        Assert.Equal(new[] { 250, 100, 120 }, Values<int>(result, "Salary_Sum"));
        Assert.Equal(new[] { 125d, 50d, 120d }, Values<double>(result, "Salary_Average"));
        Assert.Equal(new[] { 30, 35, 25 }, Values<int>(result, "Age_Min"));
        Assert.Equal(new[] { 40, 45, 25 }, Values<int>(result, "Age_Max"));
    }

    /// <summary>
    /// Verifies numeric and comparable result type contracts across multi-aggregation output columns.
    /// </summary>
    [Theory]
    [MemberData(nameof(NumericTypeCases))]
    public void Aggregate_WithNumericTypes_ReturnsExpectedSchemaAndRuntimeTypes(Array values, Type expectedSumType, object expectedSum)
    {
        // Verifies Sum follows FE-019 result types while Average always produces double.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "IT", "IT" },
            Value = values,
            CreatedAt = new[] { new DateTime(2024, 2, 1), new DateTime(2024, 1, 1) }
        });

        var result = df.GroupBy("Department").Aggregate(aggregation => aggregation
            .For("Value").Sum().Average().Min().Max()
            .For("CreatedAt").Min().Max());

        Assert.Equal(expectedSumType, result.Schema.GetColumn("Value_Sum").DataType);
        Assert.Equal(typeof(double), result.Schema.GetColumn("Value_Average").DataType);
        Assert.Equal(values.GetType().GetElementType(), result.Schema.GetColumn("Value_Min").DataType);
        Assert.Equal(typeof(DateTime), result.Schema.GetColumn("CreatedAt_Max").DataType);
        Assert.IsType(expectedSumType, result["Value_Sum"].GetValue(0)!);
        Assert.IsType<double>(result["Value_Average"].GetValue(0)!);
        Assert.Equal(expectedSum, result["Value_Sum"].GetValue(0));
        Assert.Equal(new DateTime(2024, 1, 1), result["CreatedAt_Min"].GetValue(0));
        Assert.Equal(new DateTime(2024, 2, 1), result["CreatedAt_Max"].GetValue(0));
    }

    /// <summary>
    /// Verifies null values fail the whole operation and leave grouped snapshots reusable.
    /// </summary>
    [Fact]
    public void Aggregate_WithNullValues_ThrowsAtomicallyAndGroupedCanBeReused()
    {
        // Verifies null failures do not produce partial result DataFrames or corrupt grouped state.
        var nullKey = global::Runiq.Data.DataFrame.Create(new { Department = new string?[] { "IT", null }, Salary = new[] { 1, 2 }, Age = new[] { 30, 40 } });
        var nullFirst = global::Runiq.Data.DataFrame.Create(new { Department = new[] { "IT", "HR" }, Salary = new int?[] { null, 2 }, Age = new int?[] { 30, 40 } });
        var nullLast = global::Runiq.Data.DataFrame.Create(new { Department = new[] { "IT", "HR" }, Salary = new int?[] { 1, 2 }, Age = new int?[] { 30, null } });
        var reusable = nullLast.GroupBy("Department");
        var snapshot = Snapshot(nullLast);

        Assert.Throws<ArgumentException>(() => nullKey.GroupBy("Department").Aggregate(aggregation => aggregation.For("Salary").Sum()));
        Assert.Throws<ArgumentException>(() => nullFirst.GroupBy("Department").Aggregate(aggregation => aggregation.For("Salary").Sum().For("Age").Max()));
        Assert.Throws<ArgumentException>(() => reusable.Aggregate(aggregation => aggregation.For("Salary").Sum().For("Age").Max()));

        AssertSnapshot(nullLast, snapshot);
        Assert.Throws<ArgumentException>(() => reusable.Sum("Age"));
    }

    /// <summary>
    /// Verifies invalid source snapshots, invalid aggregate types, and overflow fail without partial results.
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidAtomicFrames))]
    public void Aggregate_WithInvalidExecutionInput_ThrowsAtomically(global::Runiq.Data.DataFrame df, Type exceptionType)
    {
        // Verifies an invalid final descriptor still fails the whole multi-aggregation call.
        var grouped = df.GroupBy("Department");

        Assert.Throws(exceptionType, () => grouped.Aggregate(aggregation => aggregation
            .For("Salary").Sum()
            .For("Value").Average()));

        var valid = grouped.Sum("Salary");
        Assert.Equal(new[] { "Department", "Salary_Sum" }, ColumnNames(valid));
    }

    /// <summary>
    /// Verifies Min and Max reject non-comparable values in multi-aggregation.
    /// </summary>
    [Fact]
    public void Aggregate_WithNonComparableMinMax_ThrowsArgumentException()
    {
        // Verifies comparable validation remains consistent for Min and Max.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "IT" },
            Salary = new[] { 1 },
            Value = new[] { new Payload(1) }
        });

        Assert.Throws<ArgumentException>(() => df.GroupBy("Department").Aggregate(aggregation => aggregation.For("Salary").Sum().For("Value").Min()));
        Assert.Throws<ArgumentException>(() => df.GroupBy("Department").Aggregate(aggregation => aggregation.For("Salary").Sum().For("Value").Max()));
    }

    /// <summary>
    /// Verifies empty grouped snapshots fail and can still be reused for later failing validation.
    /// </summary>
    [Fact]
    public void Aggregate_WithEmptySource_ThrowsArgumentException()
    {
        // Verifies empty grouped snapshots never return empty result DataFrames.
        var grouped = global::Runiq.Data.DataFrame.Create(new
        {
            Department = Array.Empty<string>(),
            Salary = Array.Empty<int>(),
            Age = Array.Empty<int>()
        }).GroupBy("Department");

        Assert.Throws<ArgumentException>(() => grouped.Aggregate(aggregation => aggregation.For("Salary").Sum().For("Age").Max()));
        Assert.Throws<ArgumentException>(() => grouped.Sum("Salary"));
    }

    /// <summary>
    /// Verifies checked arithmetic overflow fails the grouped multi-aggregation operation.
    /// </summary>
    [Theory]
    [MemberData(nameof(OverflowCases))]
    public void Aggregate_WithOverflow_ThrowsOverflowException(Array values)
    {
        // Verifies exact numeric overflow does not produce a partial multi-aggregation result.
        var grouped = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "IT", "IT" },
            Salary = new[] { 1, 2 },
            Value = values
        }).GroupBy("Department");

        Assert.Throws<OverflowException>(() => grouped.Aggregate(aggregation => aggregation.For("Salary").Sum().For("Value").Sum()));
        Assert.Equal(new[] { 3 }, Values<int>(grouped.Sum("Salary"), "Salary_Sum"));
    }

    /// <summary>
    /// Verifies generated output collisions with group keys and duplicate operations are rejected.
    /// </summary>
    [Fact]
    public void Aggregate_WithResultColumnCollision_ThrowsAndGroupedCanBeReused()
    {
        // Verifies collisions fail instead of renaming generated result columns.
        var single = global::Runiq.Data.DataFrame.Create(new { Salary_Sum = new[] { "A" }, Salary = new[] { 1 } }).GroupBy("Salary_Sum");
        var composite = global::Runiq.Data.DataFrame.Create(new { Department = new[] { "IT" }, Age_Max = new[] { "A" }, Age = new[] { 30 } }).GroupBy("Department", "Age_Max");
        var duplicate = CreateEmployeeDataFrame().GroupBy("Department");

        Assert.Throws<ArgumentException>(() => single.Aggregate(aggregation => aggregation.For("Salary").Sum()));
        Assert.Throws<ArgumentException>(() => composite.Aggregate(aggregation => aggregation.For("Age").Max()));
        Assert.Throws<ArgumentException>(() => duplicate.Aggregate(aggregation => aggregation.For("Salary").Sum().For("Salary").Sum()));
        Assert.Equal(new[] { 250, 200 }, Values<int>(duplicate.Sum("Salary"), "Salary_Sum"));
    }

    /// <summary>
    /// Verifies grouped snapshots are independent from later source row and column mutations.
    /// </summary>
    [Fact]
    public void Aggregate_AfterSourceMutation_UsesGroupedSnapshot()
    {
        // Verifies add, update, remove, and column mutation after GroupBy do not affect the grouped snapshot.
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "IT", "HR", "IT" },
            Salary = new[] { 100, 80, 150 },
            Age = new[] { 30, 35, 40 }
        });
        var grouped = df.GroupBy("Department");

        df.Rows.Add(new { Department = "IT", Salary = 999, Age = 55 });
        df.Rows.Update(0, new { Department = "Ops", Salary = 1, Age = 1 });
        df.Rows.Remove(1);
        df.Columns.Rename("Age", "Years");

        var result = grouped.Aggregate(aggregation => aggregation.For("Salary").Sum().Average().For("Age").Min().Max());

        Assert.Equal(new[] { "IT", "HR" }, Values<string>(result, "Department"));
        Assert.Equal(new[] { 250, 80 }, Values<int>(result, "Salary_Sum"));
        Assert.Equal(new[] { 125d, 80d }, Values<double>(result, "Salary_Average"));
        Assert.Equal(new[] { 30, 35 }, Values<int>(result, "Age_Min"));
        Assert.Equal(new[] { 40, 35 }, Values<int>(result, "Age_Max"));
        Assert.True(df.HasColumn("Years"));
    }

    /// <summary>
    /// Verifies grouped multi-aggregation can be reused with single aggregations and independent result DataFrames.
    /// </summary>
    [Fact]
    public void Aggregate_CanBeReusedAndReturnsIndependentResults()
    {
        // Verifies success and failure do not consume grouped state, and result mutation is isolated.
        var grouped = CreateEmployeeDataFrame().GroupBy("Department");

        var first = grouped.Aggregate(aggregation => aggregation.For("Salary").Sum().Average());
        var single = grouped.Sum("Salary");
        var second = grouped.Aggregate(aggregation => aggregation.For("Salary").Sum().Average());
        first.Rows.Update(0, new { Department = "IT", Salary_Sum = 999, Salary_Average = 999d });
        var third = grouped.Aggregate(aggregation => aggregation.For("Salary").Sum().Average());
        Assert.Throws<KeyNotFoundException>(() => grouped.Aggregate(aggregation => aggregation.For("Missing").Sum()));
        var afterFailure = grouped.Aggregate(aggregation => aggregation.For("Age").Min().Max());

        Assert.Equal(new[] { 250, 200 }, Values<int>(single, "Salary_Sum"));
        Assert.Equal(new[] { 250, 200 }, Values<int>(second, "Salary_Sum"));
        Assert.Equal(new[] { 999, 200 }, Values<int>(first, "Salary_Sum"));
        Assert.Equal(new[] { 250, 200 }, Values<int>(third, "Salary_Sum"));
        Assert.Equal(new[] { 30, 25 }, Values<int>(afterFailure, "Age_Min"));
        Assert.Equal(new[] { 40, 35 }, Values<int>(afterFailure, "Age_Max"));
    }

    /// <summary>
    /// Verifies no out-of-scope aggregate APIs are exposed.
    /// </summary>
    [Fact]
    public void Aggregate_DoesNotExposeOutOfScopePublicApis()
    {
        // Verifies multi-aggregation is only available on GroupedDataFrame.
        Assert.Null(typeof(global::Runiq.Data.DataFrame).GetMethod("Aggregate"));
        Assert.Null(typeof(global::Runiq.Data.RowOperations).GetMethod("Aggregate"));
        Assert.Null(typeof(global::Runiq.Data.ColumnOperations).GetMethod("Aggregate"));
        Assert.Null(typeof(global::Runiq.Data.DataFrame).GetMethod("Count", Type.EmptyTypes));
        Assert.Empty(typeof(global::Runiq.Data.GroupAggregationBuilder).GetConstructors());
        Assert.Empty(typeof(global::Runiq.Data.ColumnAggregationBuilder).GetConstructors());
    }

    public static TheoryData<Array, Type, object> NumericTypeCases()
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

    public static TheoryData<global::Runiq.Data.DataFrame, Type> InvalidAtomicFrames()
    {
        return new TheoryData<global::Runiq.Data.DataFrame, Type>
        {
            { global::Runiq.Data.DataFrame.Create(new { Department = new[] { "IT" }, Salary = new[] { 1 }, Value = new[] { "text" } }), typeof(ArgumentException) },
            { global::Runiq.Data.DataFrame.Create(new { Department = new[] { "IT" }, Salary = new[] { 1 }, Value = new[] { new DateTime(2024, 1, 1) } }), typeof(ArgumentException) },
            { global::Runiq.Data.DataFrame.Create(new { Department = new[] { "IT" }, Salary = new[] { 1 }, Value = new[] { true } }), typeof(ArgumentException) },
            { global::Runiq.Data.DataFrame.Create(new { Department = new[] { "IT" }, Salary = new[] { 1 }, Value = new[] { new Payload(1) } }), typeof(ArgumentException) }
        };
    }

    public static TheoryData<Array> OverflowCases()
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
