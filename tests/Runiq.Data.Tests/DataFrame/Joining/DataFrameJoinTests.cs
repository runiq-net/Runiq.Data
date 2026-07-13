namespace Runiq.Data.Tests.DataFrame.Joining;

public sealed class DataFrameJoinTests
{
    // Verifies J oi nA pi O nO ve rl oa ds R et ur nD at aF ra me An dK ee pB ui ld er Se pa ra te.
    [Fact]
    public void JoinApi_OnOverloads_ReturnDataFrameAndKeepBuilderSeparate()
    {
        // Verifies the approved fluent API shapes and that On executes directly to a DataFrame.
        var left = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 1 },
            CompanyId = new[] { 10 },
            OrderId = new[] { 100 }
        });
        var rightById = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 1 },
            RightValue = new[] { "R" }
        });
        var rightByComposite = global::Runiq.Data.DataFrame.Create(new
        {
            CompanyId = new[] { 10 },
            OrderId = new[] { 100 },
            RightValue = new[] { "R" }
        });
        var renamedRight = global::Runiq.Data.DataFrame.Create(new
        {
            TenantId = new[] { 10 },
            ExternalOrderId = new[] { 100 }
        });

        var inner = left.InnerJoin(rightById).On("Id");
        var leftJoin = left.LeftJoin(rightById).On("Id", "Id");
        var rightJoin = left.RightJoin(rightByComposite).On(["CompanyId", "OrderId"]);
        var full = left.FullJoin(renamedRight).On(("CompanyId", "TenantId"), ("OrderId", "ExternalOrderId"));
        var builder = left.LeftJoin(rightById);

        Assert.IsType<global::Runiq.Data.DataFrame>(inner);
        Assert.IsType<global::Runiq.Data.DataFrame>(leftJoin);
        Assert.IsType<global::Runiq.Data.DataFrame>(rightJoin);
        Assert.IsType<global::Runiq.Data.DataFrame>(full);
        Assert.IsNotType<global::Runiq.Data.DataFrame>(builder);
        Assert.DoesNotContain(builder.GetType().GetMethods().Select(static method => method.Name), static name => name == "Select");
    }

    // Verifies I nn er Jo in P ro du ce sM at ch es Ca rt es ia nD up li ca te sS ta bl eO rd er in gA nd Co lu mn Or de r.
    [Fact]
    public void InnerJoin_ProducesMatchesCartesianDuplicatesStableOrderingAndColumnOrder()
    {
        // Verifies inner join matching, duplicate key Cartesian expansion, stable ordering, and left-then-right schema order.
        var left = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 2, 1, 1, 3 },
            LeftValue = new[] { "L2", "L1a", "L1b", "L3" }
        });
        var right = global::Runiq.Data.DataFrame.Create(new
        {
            Key = new[] { 1, 1, 2, 4 },
            RightValue = new[] { "R1a", "R1b", "R2", "R4" }
        });

        var result = left.InnerJoin(right).On("Id", "Key");

        Assert.Equal(new[] { "Id", "LeftValue", "Key", "RightValue" }, ColumnNames(result));
        Assert.Equal(
            new object?[][]
            {
                [2, "L2", 2, "R2"],
                [1, "L1a", 1, "R1a"],
                [1, "L1a", 1, "R1b"],
                [1, "L1b", 1, "R1a"],
                [1, "L1b", 1, "R1b"]
            },
            Rows(result));
    }

    // Verifies L ef tJ oi n K ee ps Un ma tc he dL ef tR ow sA nd Us es Nu ll Ri gh tV al ue s.
    [Fact]
    public void LeftJoin_KeepsUnmatchedLeftRowsAndUsesNullRightValues()
    {
        // Verifies left join preserves left rows, expands duplicate right matches, and null-fills unmatched right columns.
        var left = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 1, 2, 3 },
            LeftValue = new[] { "L1", "L2", "L3" }
        });
        var right = global::Runiq.Data.DataFrame.Create(new
        {
            Key = new[] { 1, 1 },
            RightValue = new[] { "R1a", "R1b" }
        });

        var result = left.LeftJoin(right).On("Id", "Key");

        Assert.Equal(
            new object?[][]
            {
                [1, "L1", 1, "R1a"],
                [1, "L1", 1, "R1b"],
                [2, "L2", null, null],
                [3, "L3", null, null]
            },
            Rows(result));
    }

    // Verifies L ef tJ oi n W it hE mp ty Ri gh t P re se rv es Sc he ma An dL ef tR ow s.
    [Fact]
    public void LeftJoin_WithEmptyRight_PreservesSchemaAndLeftRows()
    {
        // Verifies left join behavior when the right DataFrame has columns but no rows.
        var left = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1, 2 }, Name = new[] { "Ali", "Ayse" } });
        var right = global::Runiq.Data.DataFrame.Create(new { Key = Array.Empty<int>(), Department = Array.Empty<string>() });

        var result = left.LeftJoin(right).On("Id", "Key");

        Assert.Equal(new[] { "Id", "Name", "Key", "Department" }, ColumnNames(result));
        Assert.Equal(new object?[][] { [1, "Ali", null, null], [2, "Ayse", null, null] }, Rows(result));
    }

    // Verifies R ig ht Jo in K ee ps Un ma tc he dR ig ht Ro ws An dU se sN ul lL ef tV al ue s.
    [Fact]
    public void RightJoin_KeepsUnmatchedRightRowsAndUsesNullLeftValues()
    {
        // Verifies right join preserves right ordering, expands duplicate left matches, and null-fills unmatched left columns.
        var left = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 1, 1 },
            LeftValue = new[] { "L1a", "L1b" }
        });
        var right = global::Runiq.Data.DataFrame.Create(new
        {
            Key = new[] { 1, 2, 1 },
            RightValue = new[] { "R1a", "R2", "R1b" }
        });

        var result = left.RightJoin(right).On("Id", "Key");

        Assert.Equal(
            new object?[][]
            {
                [1, "L1a", 1, "R1a"],
                [1, "L1b", 1, "R1a"],
                [null, null, 2, "R2"],
                [1, "L1a", 1, "R1b"],
                [1, "L1b", 1, "R1b"]
            },
            Rows(result));
    }

    // Verifies R ig ht Jo in W it hE mp ty Le ft P re se rv es Sc he ma An dR ig ht Ro ws.
    [Fact]
    public void RightJoin_WithEmptyLeft_PreservesSchemaAndRightRows()
    {
        // Verifies right join behavior when the left DataFrame has columns but no rows.
        var left = global::Runiq.Data.DataFrame.Create(new { Id = Array.Empty<int>(), Name = Array.Empty<string>() });
        var right = global::Runiq.Data.DataFrame.Create(new { Key = new[] { 1 }, Department = new[] { "Engineering" } });

        var result = left.RightJoin(right).On("Id", "Key");

        Assert.Equal(new[] { "Id", "Name", "Key", "Department" }, ColumnNames(result));
        Assert.Equal(new object?[][] { [null, null, 1, "Engineering"] }, Rows(result));
    }

    // Verifies F ul lJ oi n U se sL ef tJ oi nO rd er in gT he nU nm at ch ed Ri gh tR ow s.
    [Fact]
    public void FullJoin_UsesLeftJoinOrderingThenUnmatchedRightRows()
    {
        // Verifies full join emits the left-join portion first and appends unmatched right rows in right source order.
        var left = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new[] { 1, 2 },
            LeftValue = new[] { "L1", "L2" }
        });
        var right = global::Runiq.Data.DataFrame.Create(new
        {
            Key = new[] { 2, 3, 4 },
            RightValue = new[] { "R2", "R3", "R4" }
        });

        var result = left.FullJoin(right).On("Id", "Key");

        Assert.Equal(
            new object?[][]
            {
                [1, "L1", null, null],
                [2, "L2", 2, "R2"],
                [null, null, 3, "R3"],
                [null, null, 4, "R4"]
            },
            Rows(result));
    }

    // Verifies F ul lJ oi n W it hB ot hS id es Em pt y R et ur ns Jo in ed Sc he ma An dN oR ow s.
    [Fact]
    public void FullJoin_WithBothSidesEmpty_ReturnsJoinedSchemaAndNoRows()
    {
        // Verifies full join keeps the joined schema when both DataFrames are empty.
        var left = global::Runiq.Data.DataFrame.Create(new { Id = Array.Empty<int>(), Name = Array.Empty<string>() });
        var right = global::Runiq.Data.DataFrame.Create(new { Key = Array.Empty<int>(), Department = Array.Empty<string>() });

        var result = left.FullJoin(right).On("Id", "Key");

        Assert.Equal(new[] { "Id", "Name", "Key", "Department" }, ColumnNames(result));
        Assert.Equal(0, result.Rows.Count());
    }

    // Verifies C om po si te Ke y J oi ns On ly Wh en Al lP ar ts Ma tc hA nd Pr es er ve sD ec la ra ti on Or de r.
    [Fact]
    public void CompositeKey_JoinsOnlyWhenAllPartsMatchAndPreservesDeclarationOrder()
    {
        // Verifies same-name composite keys, duplicate composite keys, mismatch exclusion, and key declaration order.
        var left = global::Runiq.Data.DataFrame.Create(new
        {
            CompanyId = new[] { 1, 1, 1, 2 },
            OrderId = new[] { 10, 10, 11, 10 },
            LeftValue = new[] { "L10a", "L10b", "L11", "L20" }
        });
        var right = global::Runiq.Data.DataFrame.Create(new
        {
            CompanyId = new[] { 1, 1, 1 },
            OrderId = new[] { 10, 10, 12 },
            RightValue = new[] { "R10a", "R10b", "R12" }
        });

        var result = left.InnerJoin(right).On(["CompanyId", "OrderId"]);

        Assert.Equal(new[] { "CompanyId", "OrderId", "LeftValue", "RightValue" }, ColumnNames(result));
        Assert.Equal(
            new object?[][]
            {
                [1, 10, "L10a", "R10a"],
                [1, 10, "L10a", "R10b"],
                [1, 10, "L10b", "R10a"],
                [1, 10, "L10b", "R10b"]
            },
            Rows(result));
    }

    // Verifies C om po si te Ke y W it hD if fe re nt Co lu mn Na me s J oi ns By Tu pl eP ai rs.
    [Fact]
    public void CompositeKey_WithDifferentColumnNames_JoinsByTuplePairs()
    {
        // Verifies tuple-based composite keys map left and right names in pair order.
        var left = global::Runiq.Data.DataFrame.Create(new
        {
            CompanyId = new[] { 1, 1 },
            OrderId = new[] { 10, 11 },
            LeftValue = new[] { "L10", "L11" }
        });
        var right = global::Runiq.Data.DataFrame.Create(new
        {
            TenantId = new[] { 1, 1 },
            ExternalOrderId = new[] { 10, 12 },
            RightValue = new[] { "R10", "R12" }
        });

        var result = left.InnerJoin(right).On(("CompanyId", "TenantId"), ("OrderId", "ExternalOrderId"));

        Assert.Equal(new object?[][] { [1, 10, "L10", 1, 10, "R10"] }, Rows(result));
    }

    // Verifies N ul lK ey s D oN ot Ma tc hA nd Ou te rJ oi ns Ke ep Ro ws As Un ma tc he d.
    [Fact]
    public void NullKeys_DoNotMatchAndOuterJoinsKeepRowsAsUnmatched()
    {
        // Verifies SQL-like null key behavior, including null-null not matching and full join keeping both null-key rows.
        var left = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new int?[] { null, 1 },
            LeftValue = new[] { "LeftNull", "LeftOne" }
        });
        var right = global::Runiq.Data.DataFrame.Create(new
        {
            Id = new int?[] { null, 1 },
            RightValue = new[] { "RightNull", "RightOne" }
        });

        var inner = left.InnerJoin(right).On("Id");
        var leftJoin = left.LeftJoin(right).On("Id");
        var rightJoin = left.RightJoin(right).On("Id");
        var full = left.FullJoin(right).On("Id");

        Assert.Equal(new object?[][] { [1, "LeftOne", "RightOne"] }, Rows(inner));
        Assert.Equal(new object?[][] { [null, "LeftNull", null], [1, "LeftOne", "RightOne"] }, Rows(leftJoin));
        Assert.Equal(new object?[][] { [null, null, "RightNull"], [1, "LeftOne", "RightOne"] }, Rows(rightJoin));
        Assert.Equal(new object?[][] { [null, "LeftNull", null], [1, "LeftOne", "RightOne"], [null, null, "RightNull"] }, Rows(full));
    }

    // Verifies C om po si te Ke y W it hA ny Nu ll Pa rt D oe sN ot Ma tc h.
    [Fact]
    public void CompositeKey_WithAnyNullPart_DoesNotMatch()
    {
        // Verifies composite keys with any null component are treated as unmatched keys.
        var left = global::Runiq.Data.DataFrame.Create(new
        {
            CompanyId = new int?[] { 1, 1 },
            OrderId = new int?[] { null, 10 },
            LeftValue = new[] { "NullOrder", "Matched" }
        });
        var right = global::Runiq.Data.DataFrame.Create(new
        {
            CompanyId = new int?[] { 1 },
            OrderId = new int?[] { 10 },
            RightValue = new[] { "Right" }
        });

        var result = left.LeftJoin(right).On(["CompanyId", "OrderId"]);

        Assert.Equal(new object?[][] { [1, null, "NullOrder", null], [1, 10, "Matched", "Right"] }, Rows(result));
    }

    // Verifies C ol um nC on fl ic t W he nN on Ke yN am eE xi st sO nB ot hS id es F ai ls Fa st An dD oe sN ot Mu ta te So ur ce s.
    [Fact]
    public void ColumnConflict_WhenNonKeyNameExistsOnBothSides_FailsFastAndDoesNotMutateSources()
    {
        // Verifies non-key name conflicts fail before mutation and report the conflicting column name.
        var left = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 }, Name = new[] { "Ali" } });
        var right = global::Runiq.Data.DataFrame.Create(new { Key = new[] { 1 }, Name = new[] { "Engineering" } });
        var leftRows = Rows(left);
        var rightRows = Rows(right);

        var exception = Assert.Throws<ArgumentException>(() => left.InnerJoin(right).On("Id", "Key"));

        Assert.Contains("Name", exception.Message);
        Assert.Equal(leftRows, Rows(left));
        Assert.Equal(rightRows, Rows(right));
        Assert.Equal(new[] { "Id", "Name" }, ColumnNames(left));
        Assert.Equal(new[] { "Key", "Name" }, ColumnNames(right));
    }

    // Verifies S am eN am eJ oi nK ey I sN ot AC on fl ic tA nd Ap pe ar sO nc e.
    [Fact]
    public void SameNameJoinKey_IsNotAConflictAndAppearsOnce()
    {
        // Verifies a shared key column is emitted once and not treated as a column conflict.
        var left = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 }, LeftValue = new[] { "L" } });
        var right = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 }, RightValue = new[] { "R" } });

        var result = left.InnerJoin(right).On("Id");

        Assert.Equal(new[] { "Id", "LeftValue", "RightValue" }, ColumnNames(result));
        Assert.Equal(new object?[][] { [1, "L", "R"] }, Rows(result));
    }

    // Verifies S am eN am eJ oi nK ey F or Un ma tc he dR ig ht Ro ws I sF il le dF ro mR ig ht Ke y.
    [Fact]
    public void SameNameJoinKey_ForUnmatchedRightRows_IsFilledFromRightKey()
    {
        // Verifies right/full joins fill the shared key from the right row when no left row exists.
        var left = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 }, LeftValue = new[] { "L1" } });
        var right = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 2 }, RightValue = new[] { "R2" } });

        var rightJoin = left.RightJoin(right).On("Id");
        var full = left.FullJoin(right).On("Id");

        Assert.Equal(new object?[][] { [2, null, "R2"] }, Rows(rightJoin));
        Assert.Equal(new object?[][] { [1, "L1", null], [2, null, "R2"] }, Rows(full));
    }

    // Verifies J oi nS ch em aT yp es F ol lo wJ oi nK in dN ul la bi li ty Ru le s.
    [Fact]
    public void JoinSchemaTypes_FollowJoinKindNullabilityRules()
    {
        // Verifies value type columns are preserved or made nullable according to the join kind and column side.
        var left = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 }, LeftNumber = new[] { 10 } });
        var right = global::Runiq.Data.DataFrame.Create(new { Key = new[] { 1 }, RightNumber = new[] { 20 } });

        var inner = left.InnerJoin(right).On("Id", "Key");
        var leftJoin = left.LeftJoin(right).On("Id", "Key");
        var rightJoin = left.RightJoin(right).On("Id", "Key");
        var full = left.FullJoin(right).On("Id", "Key");

        Assert.Equal(typeof(int), inner.RequireColumn("LeftNumber").DataType);
        Assert.Equal(typeof(int), inner.RequireColumn("RightNumber").DataType);
        Assert.Equal(typeof(int), leftJoin.RequireColumn("LeftNumber").DataType);
        Assert.Equal(typeof(int?), leftJoin.RequireColumn("RightNumber").DataType);
        Assert.Equal(typeof(int?), rightJoin.RequireColumn("LeftNumber").DataType);
        Assert.Equal(typeof(int), rightJoin.RequireColumn("RightNumber").DataType);
        Assert.Equal(typeof(int?), full.RequireColumn("LeftNumber").DataType);
        Assert.Equal(typeof(int?), full.RequireColumn("RightNumber").DataType);
    }

    // Verifies S am eN am eJ oi nK ey Sc he ma Ty pe F ol lo ws Jo in Ki nd Nu ll ab il it yR ul es.
    [Fact]
    public void SameNameJoinKeySchemaType_FollowsJoinKindNullabilityRules()
    {
        // Verifies a coalesced same-name value type key keeps or widens its schema type according to join kind.
        var left = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 }, LeftValue = new[] { "L" } });
        var right = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 2 }, RightValue = new[] { "R" } });

        var inner = left.InnerJoin(right).On("Id");
        var leftJoin = left.LeftJoin(right).On("Id");
        var rightJoin = left.RightJoin(right).On("Id");
        var full = left.FullJoin(right).On("Id");

        Assert.Equal(typeof(int), inner.RequireColumn("Id").DataType);
        Assert.Equal(typeof(int), leftJoin.RequireColumn("Id").DataType);
        Assert.Equal(typeof(int), rightJoin.RequireColumn("Id").DataType);
        Assert.Equal(typeof(int?), full.RequireColumn("Id").DataType);
    }

    // Verifies V al id at io n I nv al id In pu ts Fa il Fa st.
    [Fact]
    public void Validation_InvalidInputsFailFast()
    {
        // Verifies null right DataFrame, null/empty keys, whitespace names, and missing columns are rejected.
        var left = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 } });
        var right = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 } });

        Assert.Throws<ArgumentNullException>(() => left.InnerJoin(null!));
        Assert.Throws<ArgumentNullException>(() => left.InnerJoin(right).On((string[])null!));
        Assert.Throws<ArgumentException>(() => left.InnerJoin(right).On([]));
        Assert.Throws<ArgumentException>(() => left.InnerJoin(right).On(Array.Empty<string>()));
        Assert.Throws<ArgumentException>(() => left.InnerJoin(right).On(" "));
        Assert.Throws<KeyNotFoundException>(() => left.InnerJoin(right).On("Missing", "Id"));
        Assert.Throws<KeyNotFoundException>(() => left.InnerJoin(right).On("Id", "Missing"));
        Assert.Throws<KeyNotFoundException>(() => left.InnerJoin(right).On(("Id", "Id"), ("Missing", "Id")));
    }

    // Verifies V al id at io n W it hE mp ty Ke y D oe sN ot Mu ta te So ur ce s.
    [Fact]
    public void Validation_WithEmptyKey_DoesNotMutateSources()
    {
        // Verifies empty key validation compiles through the public API, fails fast, and leaves sources unchanged.
        var left = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 }, Name = new[] { "Ali" } });
        var right = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 }, Department = new[] { "Engineering" } });
        var leftRows = Rows(left);
        var rightRows = Rows(right);
        var leftColumns = ColumnNames(left);
        var rightColumns = ColumnNames(right);

        Assert.Throws<ArgumentException>(() => left.InnerJoin(right).On([]));

        Assert.Equal(leftRows, Rows(left));
        Assert.Equal(rightRows, Rows(right));
        Assert.Equal(leftColumns, ColumnNames(left));
        Assert.Equal(rightColumns, ColumnNames(right));
    }

    // Verifies K ey Co mp ar is on D oe sN ot Ma tc hI nt eg er An dS tr in gV al ue s.
    [Fact]
    public void KeyComparison_DoesNotMatchIntegerAndStringValues()
    {
        // Verifies joins use typed equality rather than ToString-based comparison.
        var left = global::Runiq.Data.DataFrame.Create(new { Id = new object[] { 1 }, LeftValue = new[] { "L" } });
        var right = global::Runiq.Data.DataFrame.Create(new { Id = new object[] { "1" }, RightValue = new[] { "R" } });

        var result = left.InnerJoin(right).On("Id");

        Assert.Equal(0, result.Rows.Count());
    }

    // Verifies V al id at io n W it hU ns up po rt ed Ke yV al ue F ai ls Fa st.
    [Fact]
    public void Validation_WithUnsupportedKeyValue_FailsFast()
    {
        // Verifies key values without a safe equality contract are rejected before producing a join result.
        var left = global::Runiq.Data.DataFrame.Create(new { Id = new object[] { new UnsupportedJoinKey(1) } });
        var right = global::Runiq.Data.DataFrame.Create(new { Id = new object[] { new UnsupportedJoinKey(1) } });

        var exception = Assert.Throws<ArgumentException>(() => left.InnerJoin(right).On("Id"));

        Assert.Contains("Id", exception.Message);
        Assert.Contains("cannot be compared safely", exception.Message);
    }

    // Verifies J oi nR es ul t I sS na ps ho tA nd Do es No tS ha re So ur ce Ro ws Or Co lu mn s.
    [Fact]
    public void JoinResult_IsSnapshotAndDoesNotShareSourceRowsOrColumns()
    {
        // Verifies source row, value, and column mutations after execution do not affect the join result.
        var left = global::Runiq.Data.DataFrame.Create(new { Id = new[] { 1 }, Name = new[] { "Ali" } });
        var right = global::Runiq.Data.DataFrame.Create(new { Key = new[] { 1 }, Department = new[] { "Engineering" } });

        var result = left.LeftJoin(right).On("Id", "Key");

        left.Rows.Update(0, new { Id = 1, Name = "Changed" });
        left.Rows.Add(new { Id = 2, Name = "New" });
        right.Rows.Update(0, new { Key = 1, Department = "Changed" });
        right.Columns.Rename("Department", "DepartmentName");

        Assert.Equal(new[] { "Id", "Name", "Key", "Department" }, ColumnNames(result));
        Assert.Equal(new object?[][] { [1, "Ali", 1, "Engineering"] }, Rows(result));
    }

    private static string[] ColumnNames(global::Runiq.Data.DataFrame dataFrame)
    {
        return dataFrame.Columns.Select(static column => column.Name).ToArray();
    }

    private static object?[][] Rows(global::Runiq.Data.DataFrame dataFrame)
    {
        var columnNames = ColumnNames(dataFrame);
        return Enumerable.Range(0, dataFrame.Rows.Count())
            .Select(rowIndex => columnNames.Select(columnName => dataFrame[columnName].GetValue(rowIndex)).ToArray())
            .ToArray();
    }

    private sealed class UnsupportedJoinKey
    {
        public UnsupportedJoinKey(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }
}
