using System.Reflection;
using Runiq.Data.Series;

namespace Runiq.Data.Tests.Series;

/// <summary>
/// Verifies the behavior of immutable typed value series.
/// </summary>
public sealed class SeriesTests
{
    /// <summary>
    /// Verifies that a valid integer series can be created.
    /// </summary>
    [Fact]
    public void Create_WithValidIntegerValues_Succeeds()
    {
        // Verifies that integer values can be captured in a typed series.
        var series = Series<int>.Create("Id", [1, 2, 3]);

        Assert.NotNull(series);
    }

    /// <summary>
    /// Verifies that a valid string series can be created.
    /// </summary>
    [Fact]
    public void Create_WithValidStringValues_Succeeds()
    {
        // Verifies that string values can be captured in a typed series.
        var series = Series<string>.Create("Name", ["Ada", "Grace"]);

        Assert.NotNull(series);
    }

    /// <summary>
    /// Verifies that the series name is preserved.
    /// </summary>
    [Fact]
    public void Create_PreservesName()
    {
        // Verifies that the public series exposes the same name supplied by the caller.
        var series = Series<int>.Create("Score", [10]);

        Assert.Equal("Score", series.Name);
    }

    /// <summary>
    /// Verifies that Count reports the number of values.
    /// </summary>
    [Fact]
    public void Count_ReturnsNumberOfValues()
    {
        // Verifies that Count reflects the immutable value snapshot.
        var series = Series<int>.Create("Score", [10, 20, 30]);

        Assert.Equal(3, series.Count);
    }

    /// <summary>
    /// Verifies that DataType reports the generic type.
    /// </summary>
    [Fact]
    public void DataType_ReturnsGenericType()
    {
        // Verifies that the series declares the CLR type represented by its values.
        var series = Series<DateTime>.Create("CreatedAt", [DateTime.UnixEpoch]);

        Assert.Equal(typeof(DateTime), series.DataType);
    }

    /// <summary>
    /// Verifies that value order is preserved.
    /// </summary>
    [Fact]
    public void Values_PreserveInputOrder()
    {
        // Verifies that enumeration order matches the caller's input order.
        var series = Series<string>.Create("Code", ["A", "B", "C"]);

        Assert.Equal(["A", "B", "C"], series.Values);
    }

    /// <summary>
    /// Verifies that indexed access returns the expected value.
    /// </summary>
    [Fact]
    public void Indexer_WithValidIndex_ReturnsValueAtPosition()
    {
        // Verifies that zero-based row index access returns the matching value.
        var series = Series<int>.Create("Score", [10, 20, 30]);

        Assert.Equal(20, series[1]);
    }

    /// <summary>
    /// Verifies that the public values collection cannot be modified.
    /// </summary>
    [Fact]
    public void Values_CannotBeModifiedFromPublicApi()
    {
        // Verifies that consumers receive a read-only view of the series values.
        var series = Series<int>.Create("Score", [10, 20, 30]);

        Assert.False(series.Values is ICollection<int> { IsReadOnly: false });
    }

    /// <summary>
    /// Verifies that a null series name is rejected.
    /// </summary>
    [Fact]
    public void Create_WithNullName_ThrowsArgumentNullException()
    {
        // Verifies that a series requires an actual name value.
        Assert.Throws<ArgumentNullException>(() => Series<int>.Create(null!, [1]));
    }

    /// <summary>
    /// Verifies that an empty series name is rejected.
    /// </summary>
    [Fact]
    public void Create_WithEmptyName_ThrowsArgumentException()
    {
        // Verifies that a series cannot be identified by an empty name.
        Assert.Throws<ArgumentException>(() => Series<int>.Create(string.Empty, [1]));
    }

    /// <summary>
    /// Verifies that a whitespace series name is rejected.
    /// </summary>
    [Fact]
    public void Create_WithWhitespaceName_ThrowsArgumentException()
    {
        // Verifies that whitespace-only names are not valid series identifiers.
        Assert.Throws<ArgumentException>(() => Series<int>.Create("   ", [1]));
    }

    /// <summary>
    /// Verifies that a null values collection is rejected.
    /// </summary>
    [Fact]
    public void Create_WithNullValues_ThrowsArgumentNullException()
    {
        // Verifies that callers must supply an actual values collection.
        Assert.Throws<ArgumentNullException>(() => Series<int>.Create("Id", null!));
    }

    /// <summary>
    /// Verifies that an empty values collection is allowed.
    /// </summary>
    [Fact]
    public void Create_WithEmptyValues_Succeeds()
    {
        // Verifies that a series can represent an empty typed value sequence.
        var series = Series<int>.Create("Id", []);

        Assert.Empty(series.Values);
    }

    /// <summary>
    /// Verifies that a negative index is rejected.
    /// </summary>
    [Fact]
    public void Indexer_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        // Verifies that row indexes cannot be negative.
        var series = Series<int>.Create("Id", [1]);

        Assert.Throws<ArgumentOutOfRangeException>(() => series[-1]);
    }

    /// <summary>
    /// Verifies that an index equal to Count is rejected.
    /// </summary>
    [Fact]
    public void Indexer_WithIndexEqualToCount_ThrowsArgumentOutOfRangeException()
    {
        // Verifies that valid indexes stop before Count.
        var series = Series<int>.Create("Id", [1, 2]);

        Assert.Throws<ArgumentOutOfRangeException>(() => series[series.Count]);
    }

    /// <summary>
    /// Verifies that an index greater than Count is rejected.
    /// </summary>
    [Fact]
    public void Indexer_WithIndexGreaterThanCount_ThrowsArgumentOutOfRangeException()
    {
        // Verifies that indexes beyond the value range fail clearly.
        var series = Series<int>.Create("Id", [1, 2]);

        Assert.Throws<ArgumentOutOfRangeException>(() => series[series.Count + 1]);
    }

    /// <summary>
    /// Verifies that reference type series allow null values.
    /// </summary>
    [Fact]
    public void Create_WithReferenceTypeNullValues_PreservesNullValues()
    {
        // Verifies that null references remain valid values in a reference type series.
        var series = Series<string?>.Create("Name", ["Ada", null, "Grace"]);

        Assert.Null(series[1]);
    }

    /// <summary>
    /// Verifies that nullable value type series allow null values.
    /// </summary>
    [Fact]
    public void Create_WithNullableValueTypeNullValues_PreservesNullValues()
    {
        // Verifies that null nullable values remain valid values in a nullable value type series.
        var series = Series<int?>.Create("Score", [10, null, 30]);

        Assert.Null(series[1]);
    }

    /// <summary>
    /// Verifies that non-nullable value type series report IsNullable as false.
    /// </summary>
    [Fact]
    public void IsNullable_ForNonNullableValueType_ReturnsFalse()
    {
        // Verifies that simple nullability metadata treats int as non-nullable.
        var series = Series<int>.Create("Score", [10]);

        Assert.False(series.IsNullable);
    }

    /// <summary>
    /// Verifies that nullable value type series report IsNullable as true.
    /// </summary>
    [Fact]
    public void IsNullable_ForNullableValueType_ReturnsTrue()
    {
        // Verifies that simple nullability metadata detects Nullable<T>.
        var series = Series<int?>.Create("Score", [10, null]);

        Assert.True(series.IsNullable);
    }

    /// <summary>
    /// Verifies that reference type series report IsNullable as true.
    /// </summary>
    [Fact]
    public void IsNullable_ForReferenceType_ReturnsTrue()
    {
        // Verifies that simple nullability metadata treats reference types as nullable.
        var series = Series<string>.Create("Name", ["Ada"]);

        Assert.True(series.IsNullable);
    }

    /// <summary>
    /// Verifies that the series exposes no mutable public properties.
    /// </summary>
    [Fact]
    public void PublicApi_DoesNotExposePropertyMutation()
    {
        // Verifies that consumers cannot replace series state after it is created.
        var writableProperties = typeof(Series<int>)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.SetMethod is not null)
            .Select(static property => property.Name);

        Assert.Empty(writableProperties);
    }

    /// <summary>
    /// Verifies that source collection mutations do not change the series.
    /// </summary>
    [Fact]
    public void Create_SnapshotsValuesBeforeSourceCollectionChanges()
    {
        // Verifies that the series owns an immutable snapshot of caller-provided values.
        var source = new List<int> { 1, 2, 3 };
        var series = Series<int>.Create("Id", source);

        source[1] = 99;
        source.Add(4);

        Assert.Equal([1, 2, 3], series.Values);
    }
}
