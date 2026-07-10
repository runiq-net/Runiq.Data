using Runiq.Data.Series;

namespace Runiq.Data;

/// <summary>
/// Defines grouped aggregations for one or more source columns.
/// </summary>
/// <remarks>
/// Instances are supplied by <see cref="GroupedDataFrame.Aggregate(Func{GroupAggregationBuilder, GroupAggregationBuilder})"/>
/// and are not created directly by user code. Call <see cref="For(string)"/> to select the
/// source column to aggregate, chain one or more operations for that column, and call
/// <see cref="For(string)"/> again from the same fluent chain to move to another column.
/// Declaration order is preserved in the result DataFrame.
/// </remarks>
public sealed class GroupAggregationBuilder
{
    private readonly GroupedDataFrame groupedDataFrame;
    private readonly List<AggregationDescriptor> descriptors = [];
    private readonly HashSet<string> resultColumnNames = new(StringComparer.OrdinalIgnoreCase);

    internal GroupAggregationBuilder(GroupedDataFrame groupedDataFrame)
    {
        this.groupedDataFrame = groupedDataFrame;
    }

    /// <summary>
    /// Selects a source column for one or more grouped aggregation operations.
    /// </summary>
    /// <param name="columnName">The source column to aggregate.</param>
    /// <returns>
    /// A column-level builder that can chain <c>Sum</c>, <c>Average</c>, <c>Min</c>, and
    /// <c>Max</c>, then switch to another source column with another <c>For</c> call.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when the source column does not exist.</exception>
    public ColumnAggregationBuilder For(string columnName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
        var descriptorProbe = groupedDataFrame.ResolveAggregateColumn(columnName);
        return new ColumnAggregationBuilder(this, descriptorProbe.Name);
    }

    internal IReadOnlyList<AggregationDescriptor> Build()
    {
        return descriptors.ToArray();
    }

    internal void Add(string columnName, AggregationKind kind)
    {
        var descriptor = groupedDataFrame.CreateDescriptor(columnName, kind);
        if (!resultColumnNames.Add(descriptor.ResultColumnName))
        {
            throw new ArgumentException(
                $"Generated result column '{descriptor.ResultColumnName}' was configured more than once.");
        }

        descriptors.Add(descriptor);
    }
}

/// <summary>
/// Defines grouped aggregation operations for the source column selected by <see cref="GroupAggregationBuilder.For(string)"/>.
/// </summary>
/// <remarks>
/// Instances are supplied by the grouped aggregation builder and are not created directly by
/// user code. Operations can be chained for the same source column, and <see cref="For(string)"/>
/// can be called to continue the fluent chain with another source column.
/// </remarks>
public sealed class ColumnAggregationBuilder
{
    private readonly GroupAggregationBuilder parent;
    private readonly string columnName;

    internal ColumnAggregationBuilder(GroupAggregationBuilder parent, string columnName)
    {
        this.parent = parent;
        this.columnName = columnName;
    }

    /// <summary>
    /// Converts a column-level builder back to the owning grouped aggregation builder so fluent
    /// chains can be returned from the aggregate configuration callback.
    /// </summary>
    /// <param name="builder">The column-level builder supplied by the fluent chain.</param>
    public static implicit operator GroupAggregationBuilder(ColumnAggregationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.parent;
    }

    /// <summary>
    /// Adds a grouped sum operation that creates a <c>{ColumnName}_Sum</c> result column.
    /// </summary>
    /// <returns>The same column builder so more operations or another <c>For</c> call can be chained.</returns>
    /// <remarks>
    /// Sum supports numeric columns only. Small integer source types produce <see cref="int"/>;
    /// <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>,
    /// <see cref="double"/>, and <see cref="decimal"/> preserve their result types. Integer
    /// and decimal addition is checked. Declaring the same generated result column more than
    /// once fails during configuration.
    /// </remarks>
    public ColumnAggregationBuilder Sum()
    {
        parent.Add(columnName, AggregationKind.Sum);
        return this;
    }

    /// <summary>
    /// Adds a grouped average operation that creates a <c>{ColumnName}_Average</c> result column.
    /// </summary>
    /// <returns>The same column builder so more operations or another <c>For</c> call can be chained.</returns>
    /// <remarks>
    /// Average supports numeric columns only and always produces <see cref="double"/> result
    /// values. Integer source values are divided as doubles. Declaring the same generated result
    /// column more than once fails during configuration.
    /// </remarks>
    public ColumnAggregationBuilder Average()
    {
        parent.Add(columnName, AggregationKind.Average);
        return this;
    }

    /// <summary>
    /// Adds a grouped minimum operation that creates a <c>{ColumnName}_Min</c> result column.
    /// </summary>
    /// <returns>The same column builder so more operations or another <c>For</c> call can be chained.</returns>
    /// <remarks>
    /// Min supports columns whose declared type uses default .NET comparison, including numeric,
    /// string, and <see cref="DateTime"/> values. The result column preserves the source column
    /// type. Declaring the same generated result column more than once fails during configuration.
    /// </remarks>
    public ColumnAggregationBuilder Min()
    {
        parent.Add(columnName, AggregationKind.Min);
        return this;
    }

    /// <summary>
    /// Adds a grouped maximum operation that creates a <c>{ColumnName}_Max</c> result column.
    /// </summary>
    /// <returns>The same column builder so more operations or another <c>For</c> call can be chained.</returns>
    /// <remarks>
    /// Max supports columns whose declared type uses default .NET comparison, including numeric,
    /// string, and <see cref="DateTime"/> values. The result column preserves the source column
    /// type. Declaring the same generated result column more than once fails during configuration.
    /// </remarks>
    public ColumnAggregationBuilder Max()
    {
        parent.Add(columnName, AggregationKind.Max);
        return this;
    }

    /// <summary>
    /// Selects another source column and continues the grouped aggregation fluent chain.
    /// </summary>
    /// <param name="columnName">The next source column to aggregate.</param>
    /// <returns>A column-level builder for the selected source column.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="columnName"/> is empty or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="columnName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when the source column does not exist.</exception>
    public ColumnAggregationBuilder For(string columnName)
    {
        return parent.For(columnName);
    }
}

internal enum AggregationKind
{
    Sum,
    Average,
    Min,
    Max
}

internal sealed record AggregationDescriptor(
    ISeries Column,
    string ColumnName,
    AggregationKind Kind,
    string ResultColumnName,
    Type ResultColumnType);
