using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Runiq.Data.IO.Tests.IO.Sql.TestDoubles;

/// <summary>
/// Provides a controllable DbConnection for SQL Read and SQL Write ownership and state-transition tests.
/// </summary>
internal sealed class StubDbConnection : DbConnection
{
    private string connectionString = string.Empty;
    private ConnectionState state;

    internal StubDbConnection(ConnectionState state, StubDbDataReader? reader = null)
    {
        this.state = state;
        Reader = reader ?? StubDbDataReader.Create([("Value", typeof(int))], [[1]]);
    }

    internal StubDbDataReader Reader { get; set; }

    internal StubDbCommand? LastCreatedCommand { get; private set; }

    internal List<StubDbCommand> CreatedCommands { get; } = [];

    internal List<StubDbTransaction> CreatedTransactions { get; } = [];

    internal int OpenCount { get; private set; }

    internal int CloseCount { get; private set; }

    internal bool WasDisposed { get; private set; }

    internal Exception? ExecuteException { get; set; }

    internal Exception? RollbackException { get; set; }

    internal Queue<int> ExecuteNonQueryResults { get; } = [];

    internal int FailExecuteNonQueryOnCall { get; set; }

    [AllowNull]
    public override string ConnectionString
    {
        get => connectionString;
        set => connectionString = value ?? string.Empty;
    }

    public override string Database => "Stub";

    public override string DataSource => "Stub";

    public override string ServerVersion => "1";

    public override ConnectionState State => state;

    public override void ChangeDatabase(string databaseName)
    {
    }

    public override void Close()
    {
        CloseCount++;
        state = ConnectionState.Closed;
    }

    public override void Open()
    {
        OpenCount++;
        state = ConnectionState.Open;
    }

    protected override DbCommand CreateDbCommand()
    {
        LastCreatedCommand = new StubDbCommand(this);
        CreatedCommands.Add(LastCreatedCommand);
        return LastCreatedCommand;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        var transaction = new StubDbTransaction(this) { RollbackException = RollbackException };
        CreatedTransactions.Add(transaction);
        return transaction;
    }

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }
}
