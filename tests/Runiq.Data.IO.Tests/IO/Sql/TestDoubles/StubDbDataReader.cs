using System.Collections;
using System.Data;
using System.Data.Common;

namespace Runiq.Data.IO.Tests.IO.Sql.TestDoubles;

/// <summary>
/// Simulates DbDataReader schema, rows, failures, multiple results, and disposal behavior.
/// </summary>
internal sealed class StubDbDataReader : DbDataReader
{
    private readonly IReadOnlyList<(string Name, Type Type)> columns;
    private readonly IReadOnlyList<object?[]> rows;
    private int rowIndex = -1;
    private bool nextResultChecked;

    private StubDbDataReader(IReadOnlyList<(string Name, Type Type)> columns, IReadOnlyList<object?[]> rows)
    {
        this.columns = columns;
        this.rows = rows;
    }

    internal bool WasDisposed { get; private set; }

    internal bool HasSecondResult { get; set; }

    internal Exception? ReadException { get; set; }

    internal Exception? GetValueException { get; set; }

    internal static StubDbDataReader Create(IReadOnlyList<(string Name, Type Type)> columns, IReadOnlyList<object?[]> rows)
    {
        return new StubDbDataReader(columns, rows);
    }

    public override int FieldCount => columns.Count;

    public override bool HasRows => rows.Count > 0;

    public override bool IsClosed => WasDisposed;

    public override int RecordsAffected => -1;

    public override int Depth => 0;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool Read()
    {
        if (ReadException is not null)
        {
            throw ReadException;
        }

        if (rowIndex + 1 >= rows.Count)
        {
            return false;
        }

        rowIndex++;
        return true;
    }

    public override bool NextResult()
    {
        if (nextResultChecked)
        {
            return false;
        }

        nextResultChecked = true;
        return HasSecondResult;
    }

    public override string GetName(int ordinal)
    {
        return columns[ordinal].Name;
    }

    public override Type GetFieldType(int ordinal)
    {
        return columns[ordinal].Type;
    }

    public override object GetValue(int ordinal)
    {
        if (GetValueException is not null)
        {
            throw GetValueException;
        }

        return rows[rowIndex][ordinal] ?? DBNull.Value;
    }

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var index = 0; index < count; index++)
        {
            values[index] = GetValue(index);
        }

        return count;
    }

    public override int GetOrdinal(string name)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (columns[index].Name == name)
            {
                return index;
            }
        }

        throw new IndexOutOfRangeException(name);
    }

    public override bool IsDBNull(int ordinal)
    {
        return GetValue(ordinal) == DBNull.Value;
    }

    public override IEnumerator GetEnumerator()
    {
        throw new NotSupportedException();
    }

    public override DataTable? GetSchemaTable()
    {
        throw new NotSupportedException();
    }

    public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);

    public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

    public override char GetChar(int ordinal) => (char)GetValue(ordinal);

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

    public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);

    public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);

    public override double GetDouble(int ordinal) => (double)GetValue(ordinal);

    public override float GetFloat(int ordinal) => (float)GetValue(ordinal);

    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);

    public override short GetInt16(int ordinal) => (short)GetValue(ordinal);

    public override int GetInt32(int ordinal) => (int)GetValue(ordinal);

    public override long GetInt64(int ordinal) => (long)GetValue(ordinal);

    public override string GetString(int ordinal) => (string)GetValue(ordinal);

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }
}
