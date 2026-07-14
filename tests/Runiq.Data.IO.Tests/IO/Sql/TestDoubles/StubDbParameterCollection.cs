using System.Collections;
using System.Data.Common;

namespace Runiq.Data.IO.Tests.IO.Sql.TestDoubles;

/// <summary>
/// Tracks DbCommand parameters so SQL tests can verify command ownership and parameter reuse.
/// </summary>
internal sealed class StubDbParameterCollection : DbParameterCollection
{
    private readonly List<object> values = [];

    public override int Count => values.Count;

    public override object SyncRoot => ((ICollection)values).SyncRoot;

    public override int Add(object value)
    {
        values.Add(value);
        return values.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var value in values)
        {
            Add(value!);
        }
    }

    public override void Clear()
    {
        values.Clear();
    }

    public override bool Contains(object value) => values.Contains(value);

    public override bool Contains(string value) => values.OfType<DbParameter>().Any(parameter => parameter.ParameterName == value);

    public override void CopyTo(Array array, int index) => values.ToArray().CopyTo(array, index);

    public override IEnumerator GetEnumerator() => values.GetEnumerator();

    public override int IndexOf(object value) => values.IndexOf(value);

    public override int IndexOf(string parameterName)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (values[index] is DbParameter parameter && parameter.ParameterName == parameterName)
            {
                return index;
            }
        }

        return -1;
    }

    public override void Insert(int index, object value) => values.Insert(index, value);

    public override void Remove(object value) => values.Remove(value);

    public override void RemoveAt(int index) => values.RemoveAt(index);

    public override void RemoveAt(string parameterName)
    {
        var index = IndexOf(parameterName);
        if (index >= 0)
        {
            RemoveAt(index);
        }
    }

    protected override DbParameter GetParameter(int index) => (DbParameter)values[index];

    protected override DbParameter GetParameter(string parameterName) => (DbParameter)values[IndexOf(parameterName)];

    protected override void SetParameter(int index, DbParameter value) => values[index] = value;

    protected override void SetParameter(string parameterName, DbParameter value) => values[IndexOf(parameterName)] = value;
}
