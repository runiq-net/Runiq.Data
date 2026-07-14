using System.Data;
using System.Data.Common;

namespace Runiq.Data.IO.Tests.IO.Sql.TestDoubles;

/// <summary>
/// Provides a stable transaction reference for command ownership and SQL Write transaction tests.
/// </summary>
internal sealed class StubDbTransaction(DbConnection connection) : DbTransaction
{
    private readonly DbConnection? connection = connection;

    internal int CommitCount { get; private set; }

    internal int RollbackCount { get; private set; }

    internal bool WasDisposed { get; private set; }

    internal Exception? RollbackException { get; set; }

    internal bool ClearConnection { get; set; }

    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

    protected override DbConnection? DbConnection => ClearConnection ? null : connection;

    public override void Commit()
    {
        CommitCount++;
    }

    public override void Rollback()
    {
        RollbackCount++;
        if (RollbackException is not null)
        {
            throw RollbackException;
        }
    }

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }
}
