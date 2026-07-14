using System.Data;
using System.Data.Common;

namespace Runiq.Data.IO.Tests.IO.Sql.TestDoubles;

/// <summary>
/// Provides a stable transaction reference for command ownership tests.
/// </summary>
internal sealed class StubDbTransaction(DbConnection connection) : DbTransaction
{
    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

    protected override DbConnection DbConnection { get; } = connection;

    public override void Commit()
    {
    }

    public override void Rollback()
    {
    }
}
