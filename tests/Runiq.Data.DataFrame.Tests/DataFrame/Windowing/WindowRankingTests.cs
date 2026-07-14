using Runiq.Data.Series;

namespace Runiq.Data.DataFrameTests.Windowing;

/// <summary>
/// Verifies window Rank and DenseRank partitioning, ordering, alignment, validation, and mutation contracts.
/// </summary>
public sealed class WindowRankingTests
{
    // Verifies that global Rank assigns one-based values by ascending ordering.
    [Fact]
    public void Rank_WithGlobalAscendingOrdering_ReturnsOneBasedValues()
    {
        var df = CreateEmployeeDataFrame();

        var rank = df.Window()
            .OrderBy("Salary")
            .Rank();

        Assert.Equal(new[] { 4, 1, 3, 2, 5 }, Values(rank));
    }

    // Verifies that global Rank assigns one-based values by descending ordering.
    [Fact]
    public void Rank_WithGlobalDescendingOrdering_ReturnsOneBasedValues()
    {
        var df = CreateEmployeeDataFrame();

        var rank = df.Window()
            .OrderByDescending("Salary")
            .Rank();

        Assert.Equal(new[] { 2, 5, 3, 4, 1 }, Values(rank));
    }

    // Verifies that partitioned Rank restarts at one for each key value.
    [Fact]
    public void Rank_WithPartition_RestartsInsideEachPartition()
    {
        var df = CreateEmployeeDataFrame();

        var rank = df.Window()
            .PartitionBy("Department")
            .OrderByDescending("Salary")
            .Rank();

        Assert.Equal(new[] { 1, 3, 2, 1, 1 }, Values(rank));
    }

    // Verifies that global DenseRank assigns one-based dense values by descending ordering.
    [Fact]
    public void DenseRank_WithGlobalDescendingOrdering_ReturnsDenseValues()
    {
        var df = CreateTiedSalaryDataFrame();

        var denseRank = df.Window()
            .OrderByDescending("Salary")
            .DenseRank();

        Assert.Equal(new[] { 1, 1, 2, 3, 3, 4 }, Values(denseRank));
    }

    // Verifies that partitioned DenseRank restarts at one for each key value.
    [Fact]
    public void DenseRank_WithPartition_RestartsInsideEachPartition()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales", "Sales", "Ops", "Ops" },
            Salary = new[] { 100, 90, 90, 70, 70 }
        });

        var denseRank = df.Window()
            .PartitionBy("Department")
            .OrderByDescending("Salary")
            .DenseRank();

        Assert.Equal(new[] { 1, 2, 2, 1, 1 }, Values(denseRank));
    }

    // Verifies that Rank leaves a gap after tied ordering keys.
    [Fact]
    public void Rank_WithTie_LeavesGapAfterTie()
    {
        var df = CreateTiedSalaryDataFrame();

        var rank = df.Window()
            .OrderByDescending("Salary")
            .Rank();

        Assert.Equal(new[] { 1, 1, 3, 4, 4, 6 }, Values(rank));
    }

    // Verifies that DenseRank does not leave a gap after tied ordering keys.
    [Fact]
    public void DenseRank_WithTie_DoesNotLeaveGapAfterTie()
    {
        var df = CreateTiedSalaryDataFrame();

        var denseRank = df.Window()
            .OrderByDescending("Salary")
            .DenseRank();

        Assert.Equal(new[] { 1, 1, 2, 3, 3, 4 }, Values(denseRank));
    }

    // Verifies that multiple tie groups are ranked independently in one ordered result.
    [Fact]
    public void Rank_WithMultipleTieGroups_ComputesAllGaps()
    {
        var df = CreateTiedSalaryDataFrame();

        var rank = df.Window()
            .OrderByDescending("Salary")
            .Rank();

        Assert.Equal(new[] { 1, 1, 3, 4, 4, 6 }, Values(rank));
    }

    // Verifies that composite ordering uses all ordering columns when deciding Rank ties.
    [Fact]
    public void Rank_WithCompositeOrdering_UsesAllOrderingColumnsForTies()
    {
        var df = CreateCompositeOrderingDataFrame();

        var rank = df.Window()
            .PartitionBy("Department")
            .OrderByDescending("Salary")
            .ThenBy("Name")
            .Rank();

        Assert.Equal(new[] { 1, 2, 2, 4 }, Values(rank));
    }

    // Verifies that composite ordering uses all ordering columns when deciding DenseRank ties.
    [Fact]
    public void DenseRank_WithCompositeOrdering_UsesAllOrderingColumnsForTies()
    {
        var df = CreateCompositeOrderingDataFrame();

        var denseRank = df.Window()
            .PartitionBy("Department")
            .OrderByDescending("Salary")
            .ThenBy("Name")
            .DenseRank();

        Assert.Equal(new[] { 1, 2, 2, 3 }, Values(denseRank));
    }

    // Verifies that ascending and descending ordering clauses can be combined for ranking.
    [Fact]
    public void Rank_WithAscendingAndDescendingOrderingCombination_OrdersByEachDirection()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Grade = new[] { 2, 1, 1, 2, 1 },
            Salary = new[] { 100, 100, 200, 300, 200 }
        });

        var rank = df.Window()
            .OrderBy("Grade")
            .ThenByDescending("Salary")
            .Rank();

        Assert.Equal(new[] { 5, 3, 1, 4, 1 }, Values(rank));
    }

    // Verifies that aligned ranking values map back to original source row positions.
    [Fact]
    public void Rank_ReturnsValuesAlignedToSourceRows()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Third", "First", "Second" },
            Salary = new[] { 80, 100, 90 }
        });

        var rank = df.Window()
            .OrderByDescending("Salary")
            .Rank();

        Assert.Equal(new[] { 3, 1, 2 }, Values(rank));
    }

    // Verifies that fully equal ordering keys preserve source-row stability for terminal ranking.
    [Fact]
    public void Rank_WithFullyEqualOrderingKeys_PreservesSourceRowStability()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "First", "Second", "Third" },
            Salary = new[] { 100, 100, 100 }
        });

        var rowNumbers = df.Window()
            .OrderByDescending("Salary")
            .RowNumber();
        var rank = df.Window()
            .OrderByDescending("Salary")
            .Rank();

        Assert.Equal(new[] { 1, 2, 3 }, Values(rowNumbers));
        Assert.Equal(new[] { 1, 1, 1 }, Values(rank));
    }

    // Verifies that null partition keys are valid and ranked together.
    [Fact]
    public void Rank_WithNullPartitionKey_GroupsNullValuesTogether()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new string?[] { null, "Sales", null, "Sales" },
            Salary = new[] { 200, 50, 100, 100 }
        });

        var rank = df.Window()
            .PartitionBy("Department")
            .OrderBy("Salary")
            .Rank();

        Assert.Equal(new[] { 2, 1, 1, 2 }, Values(rank));
    }

    // Verifies that null ordering values fail through the same validation path used by RowNumber.
    [Fact]
    public void Rank_WithNullOrderingValue_ThrowsLikeRowNumber()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Salary = new int?[] { 100, null }
        });

        var exception = Assert.Throws<ArgumentException>(() => df.Window().OrderBy("Salary"));

        Assert.Contains("Salary", exception.Message);
        Assert.Contains("null", exception.Message);
    }

    // Verifies that Rank returns an empty aligned result for an empty DataFrame.
    [Fact]
    public void Rank_WithEmptyDataFrame_ReturnsEmptySeries()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = Array.Empty<string>(),
            Salary = Array.Empty<int>()
        });

        var rank = df.Window()
            .PartitionBy("Department")
            .OrderBy("Salary")
            .Rank();

        Assert.Empty(Values(rank));
    }

    // Verifies that single-row partitions receive rank one.
    [Fact]
    public void DenseRank_WithSingleRowPartition_ReturnsOne()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Ops" },
            Salary = new[] { 100, 90 }
        });

        var denseRank = df.Window()
            .PartitionBy("Department")
            .OrderByDescending("Salary")
            .DenseRank();

        Assert.Equal(new[] { 1, 1 }, Values(denseRank));
    }

    // Verifies that unsupported ordering values fail through the existing sorting validation semantics.
    [Fact]
    public void Rank_WithUnsupportedOrderingValues_Throws()
    {
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Payload = new[] { new SortPayload(2), new SortPayload(1) }
        });

        var exception = Assert.Throws<ArgumentException>(() => df.Window().OrderBy("Payload"));

        Assert.Contains("Payload", exception.Message);
        Assert.Contains("cannot be compared", exception.Message);
    }

    // Verifies that Rank and DenseRank outputs can be appended through the existing Columns.Add API.
    [Fact]
    public void RankingResults_CanBeAddedAsColumns()
    {
        var df = CreateTiedSalaryDataFrame();
        var rank = df.Window()
            .OrderByDescending("Salary")
            .Rank();
        var denseRank = df.Window()
            .OrderByDescending("Salary")
            .DenseRank();

        df.Columns.Add("Rank", rank);
        df.Columns.Add("DenseRank", denseRank);

        Assert.Equal(new[] { 1, 1, 3, 4, 4, 6 }, IntColumn(df, "Rank"));
        Assert.Equal(new[] { 1, 1, 2, 3, 3, 4 }, IntColumn(df, "DenseRank"));
    }

    // Verifies that ranking does not mutate source rows, source columns, or schema.
    [Fact]
    public void Ranking_DoesNotMutateSourceDataFrame()
    {
        var df = CreateEmployeeDataFrame();
        var schema = df.Schema;
        var columnNames = ColumnNames(df);
        var salaries = IntColumn(df, "Salary");

        _ = df.Window()
            .PartitionBy("Department")
            .OrderByDescending("Salary")
            .Rank();
        _ = df.Window()
            .PartitionBy("Department")
            .OrderByDescending("Salary")
            .DenseRank();

        Assert.Same(schema, df.Schema);
        Assert.Equal(columnNames, ColumnNames(df));
        Assert.Equal(salaries, IntColumn(df, "Salary"));
    }

    private static global::Runiq.Data.DataFrame CreateEmployeeDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales", "Sales", "Ops", "Finance" },
            Name = new[] { "Ali", "Ayse", "Mehmet", "Zeynep", "Can" },
            Salary = new[] { 120000, 90000, 110000, 95000, 150000 }
        });
    }

    private static global::Runiq.Data.DataFrame CreateTiedSalaryDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Salary = new[] { 100, 100, 90, 80, 80, 70 }
        });
    }

    private static global::Runiq.Data.DataFrame CreateCompositeOrderingDataFrame()
    {
        return global::Runiq.Data.DataFrame.Create(new
        {
            Department = new[] { "Sales", "Sales", "Sales", "Sales" },
            Salary = new[] { 200, 100, 100, 100 },
            Name = new[] { "Mehmet", "Ali", "Ali", "Zeynep" }
        });
    }

    private static int[] Values(Series<int> series)
    {
        return series.Values.ToArray();
    }

    private static string[] ColumnNames(global::Runiq.Data.DataFrame df)
    {
        return df.Columns.Select(static column => column.Name).ToArray();
    }

    private static int[] IntColumn(global::Runiq.Data.DataFrame df, string columnName)
    {
        return Enumerable.Range(0, df.Rows.Count())
            .Select(index => (int)df[columnName].GetValue(index)!)
            .ToArray();
    }

    /// <summary>
    /// Represents a deliberately non-comparable value used to verify fail-fast ordering behavior.
    /// </summary>
    private sealed record SortPayload(int Value);
}
