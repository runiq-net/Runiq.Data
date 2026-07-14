using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Runiq.Data.IO.Tests.IO.Sql.TestDoubles;

/// <summary>
/// Provides a DbCommand test double that exposes ownership-sensitive command and execution state.
/// </summary>
internal sealed class StubDbCommand : DbCommand
{
    private string? commandText = string.Empty;
    private readonly StubDbConnection? ownerConnection;
    private DbConnection? connection;
    private readonly StubDbParameterCollection parameters = new();

    internal StubDbCommand(StubDbConnection? connection = null)
    {
        ownerConnection = connection;
        this.connection = connection;
    }

    internal bool WasDisposed { get; private set; }

    internal StubDbTransaction? StubTransaction { get; set; }

    internal Exception? ExecuteException { get; set; }

    internal int ExecuteNonQueryCount { get; private set; }

    internal List<object?[]> ExecuteNonQueryParameterValues { get; } = [];

    internal StubDbDataReader? Reader { get; set; }

    [AllowNull]
    public override string CommandText
    {
        get => commandText!;
        set => commandText = value;
    }

    public override int CommandTimeout { get; set; } = 30;

    public override CommandType CommandType { get; set; } = CommandType.Text;

    public override bool DesignTimeVisible { get; set; }

    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection
    {
        get => connection;
        set => connection = value;
    }

    protected override DbParameterCollection DbParameterCollection => parameters;

    protected override DbTransaction? DbTransaction
    {
        get => StubTransaction;
        set => StubTransaction = (StubDbTransaction?)value;
    }

    public override void Cancel()
    {
    }

    public override int ExecuteNonQuery()
    {
        ExecuteNonQueryCount++;
        ExecuteNonQueryParameterValues.Add(parameters.Cast<DbParameter>().Select(static parameter => SnapshotValue(parameter.Value)).ToArray());

        if (ExecuteException is not null)
        {
            throw ExecuteException;
        }

        if (ownerConnection?.ExecuteException is not null)
        {
            throw ownerConnection.ExecuteException;
        }

        if (ownerConnection?.FailExecuteNonQueryOnCall == ExecuteNonQueryCount)
        {
            throw new DataException($"ExecuteNonQuery failed on call {ExecuteNonQueryCount}.");
        }

        if (ownerConnection?.ExecuteNonQueryResults.Count > 0)
        {
            return ownerConnection.ExecuteNonQueryResults.Dequeue();
        }

        return 1;
    }

    public override object? ExecuteScalar()
    {
        throw new NotSupportedException();
    }

    public override void Prepare()
    {
    }

    protected override DbParameter CreateDbParameter()
    {
        return new StubDbParameter();
    }

    private static object? SnapshotValue(object? value)
    {
        return value is byte[] bytes ? bytes.ToArray() : value;
    }

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        if (ExecuteException is not null)
        {
            throw ExecuteException;
        }

        if (ownerConnection?.ExecuteException is not null)
        {
            throw ownerConnection.ExecuteException;
        }

        return Reader ?? ownerConnection?.Reader ?? StubDbDataReader.Create([("Value", typeof(int))], [[1]]);
    }

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }
}
