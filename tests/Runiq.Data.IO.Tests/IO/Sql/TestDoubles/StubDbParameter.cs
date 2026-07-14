using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Runiq.Data.IO.Tests.IO.Sql.TestDoubles;

/// <summary>
/// Provides the minimal DbParameter implementation required by command ownership tests.
/// </summary>
internal sealed class StubDbParameter : DbParameter
{
    private string parameterName = string.Empty;
    private string sourceColumn = string.Empty;

    public override DbType DbType { get; set; }

    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

    public override bool IsNullable { get; set; }

    [AllowNull]
    public override string ParameterName
    {
        get => parameterName;
        set => parameterName = value ?? string.Empty;
    }

    [AllowNull]
    public override string SourceColumn
    {
        get => sourceColumn;
        set => sourceColumn = value ?? string.Empty;
    }

    public override object? Value { get; set; }

    public override bool SourceColumnNullMapping { get; set; }

    public override int Size { get; set; }

    public override void ResetDbType()
    {
    }
}
