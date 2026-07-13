namespace Runiq.Data;

/// <summary>
/// Represents a read-only DataFrame cell value used by filter-specific row views.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CellValue"/> is returned by <see cref="FilterRow"/> to keep predicates
/// natural, for example <c>row["Age"] &gt;= 30</c>, while <see cref="Row"/> continues
/// to expose raw object values for direct row access compatibility. Numeric comparison
/// operators support <see cref="int"/>, <see cref="long"/>, <see cref="decimal"/>, and
/// <see cref="double"/> values when the stored value has the same CLR type as the literal being
/// compared. String and Boolean equality are supported directly.
/// </para>
/// <para>
/// No string-to-number or other implicit data conversion is performed. Type mismatches throw
/// <see cref="ArgumentException"/> so invalid predicates do not silently exclude rows.
/// </para>
/// </remarks>
public readonly struct CellValue : IEquatable<CellValue>
{
    internal CellValue(string columnName, int rowIndex, object? value)
    {
        ColumnName = columnName;
        RowIndex = rowIndex;
        Value = value;
    }

    /// <summary>
    /// Gets the canonical DataFrame column name for this value.
    /// </summary>
    public string ColumnName { get; }

    /// <summary>
    /// Gets the zero-based row index where this value was read.
    /// </summary>
    public int RowIndex { get; }

    /// <summary>
    /// Gets the raw object value stored in the DataFrame cell.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// Determines whether the underlying string cell value contains the specified substring.
    /// </summary>
    /// <param name="value">The substring to search for.</param>
    /// <returns><see langword="true"/> when the string cell value contains the specified substring.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the cell value is not a string.
    /// </exception>
    public bool Contains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Require<string>().Contains(value);
    }

    /// <summary>
    /// Determines whether the underlying string cell value starts with the specified prefix.
    /// </summary>
    /// <param name="value">The prefix to compare with the start of the string cell value.</param>
    /// <returns><see langword="true"/> when the string cell value starts with the specified prefix.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the cell value is not a string.
    /// </exception>
    public bool StartsWith(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Require<string>().StartsWith(value);
    }

    /// <summary>
    /// Determines whether the underlying string cell value ends with the specified suffix.
    /// </summary>
    /// <param name="value">The suffix to compare with the end of the string cell value.</param>
    /// <returns><see langword="true"/> when the string cell value ends with the specified suffix.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the cell value is not a string.
    /// </exception>
    public bool EndsWith(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Require<string>().EndsWith(value);
    }

    /// <summary>
    /// Compares an integer cell value to an integer literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The integer literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell value is greater than or equal to <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null integer.</exception>
    public static bool operator >=(CellValue left, int right) => left.Require<int>() >= right;

    /// <summary>
    /// Compares an integer cell value to an integer literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The integer literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell value is greater than <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null integer.</exception>
    public static bool operator >(CellValue left, int right) => left.Require<int>() > right;

    /// <summary>
    /// Compares an integer cell value to an integer literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The integer literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell value is less than or equal to <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null integer.</exception>
    public static bool operator <=(CellValue left, int right) => left.Require<int>() <= right;

    /// <summary>
    /// Compares an integer cell value to an integer literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The integer literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell value is less than <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null integer.</exception>
    public static bool operator <(CellValue left, int right) => left.Require<int>() < right;

    /// <summary>
    /// Compares a long cell value to a long literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The long literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell value is greater than or equal to <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null long.</exception>
    public static bool operator >=(CellValue left, long right) => left.Require<long>() >= right;

    /// <summary>
    /// Compares a long cell value to a long literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The long literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell value is greater than <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null long.</exception>
    public static bool operator >(CellValue left, long right) => left.Require<long>() > right;

    /// <summary>
    /// Compares a long cell value to a long literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The long literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell value is less than or equal to <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null long.</exception>
    public static bool operator <=(CellValue left, long right) => left.Require<long>() <= right;

    /// <summary>
    /// Compares a long cell value to a long literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The long literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell value is less than <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null long.</exception>
    public static bool operator <(CellValue left, long right) => left.Require<long>() < right;

    /// <summary>
    /// Compares a decimal cell value to a decimal literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The decimal literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell value is greater than or equal to <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null decimal.</exception>
    public static bool operator >=(CellValue left, decimal right) => left.Require<decimal>() >= right;

    /// <summary>
    /// Compares a decimal cell value to a decimal literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The decimal literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell value is greater than <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null decimal.</exception>
    public static bool operator >(CellValue left, decimal right) => left.Require<decimal>() > right;

    /// <summary>
    /// Compares a decimal cell value to a decimal literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The decimal literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell value is less than or equal to <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null decimal.</exception>
    public static bool operator <=(CellValue left, decimal right) => left.Require<decimal>() <= right;

    /// <summary>
    /// Compares a decimal cell value to a decimal literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The decimal literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell value is less than <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null decimal.</exception>
    public static bool operator <(CellValue left, decimal right) => left.Require<decimal>() < right;

    /// <summary>
    /// Compares a double cell value to a double literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The double literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell value is greater than or equal to <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null double.</exception>
    public static bool operator >=(CellValue left, double right) => left.Require<double>() >= right;

    /// <summary>
    /// Compares a double cell value to a double literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The double literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell value is greater than <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null double.</exception>
    public static bool operator >(CellValue left, double right) => left.Require<double>() > right;

    /// <summary>
    /// Compares a double cell value to a double literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The double literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell value is less than or equal to <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null double.</exception>
    public static bool operator <=(CellValue left, double right) => left.Require<double>() <= right;

    /// <summary>
    /// Compares a double cell value to a double literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The double literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell value is less than <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null double.</exception>
    public static bool operator <(CellValue left, double right) => left.Require<double>() < right;

    /// <summary>
    /// Compares a string cell value to a string literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The string literal to compare with.</param>
    /// <returns><see langword="true"/> when both strings are equal using ordinal comparison.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a string.</exception>
    public static bool operator ==(CellValue left, string? right) => string.Equals(left.RequireNullable<string>(), right, StringComparison.Ordinal);

    /// <summary>
    /// Compares a string cell value to a string literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The string literal to compare with.</param>
    /// <returns><see langword="true"/> when the strings are not equal using ordinal comparison.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a string.</exception>
    public static bool operator !=(CellValue left, string? right) => !(left == right);

    /// <summary>
    /// Compares a Boolean cell value to a Boolean literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The Boolean literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell Boolean equals <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null Boolean.</exception>
    public static bool operator ==(CellValue left, bool right) => left.Require<bool>() == right;

    /// <summary>
    /// Compares a Boolean cell value to a Boolean literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The Boolean literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell Boolean does not equal <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null Boolean.</exception>
    public static bool operator !=(CellValue left, bool right) => !(left == right);

    /// <summary>
    /// Compares an integer cell value to an integer literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The integer literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell integer equals <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null integer.</exception>
    public static bool operator ==(CellValue left, int right) => left.Require<int>() == right;

    /// <summary>
    /// Compares an integer cell value to an integer literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The integer literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell integer does not equal <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null integer.</exception>
    public static bool operator !=(CellValue left, int right) => !(left == right);

    /// <summary>
    /// Compares a long cell value to a long literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The long literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell long equals <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null long.</exception>
    public static bool operator ==(CellValue left, long right) => left.Require<long>() == right;

    /// <summary>
    /// Compares a long cell value to a long literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The long literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell long does not equal <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null long.</exception>
    public static bool operator !=(CellValue left, long right) => !(left == right);

    /// <summary>
    /// Compares a decimal cell value to a decimal literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The decimal literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell decimal equals <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null decimal.</exception>
    public static bool operator ==(CellValue left, decimal right) => left.Require<decimal>() == right;

    /// <summary>
    /// Compares a decimal cell value to a decimal literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The decimal literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell decimal does not equal <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null decimal.</exception>
    public static bool operator !=(CellValue left, decimal right) => !(left == right);

    /// <summary>
    /// Compares a double cell value to a double literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The double literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell double equals <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null double.</exception>
    public static bool operator ==(CellValue left, double right) => left.Require<double>() == right;

    /// <summary>
    /// Compares a double cell value to a double literal and throws for incompatible types.
    /// </summary>
    /// <param name="left">The DataFrame cell value to compare.</param>
    /// <param name="right">The double literal to compare with.</param>
    /// <returns><see langword="true"/> when the cell double does not equal <paramref name="right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the cell does not contain a non-null double.</exception>
    public static bool operator !=(CellValue left, double right) => !(left == right);

    /// <summary>
    /// Determines whether this value stores the same raw value as another cell value.
    /// </summary>
    /// <param name="other">The other cell value to compare with.</param>
    /// <returns><see langword="true"/> when both raw values are equal.</returns>
    public bool Equals(CellValue other) => Equals(Value, other.Value);

    /// <summary>
    /// Determines whether this value stores the same raw value as another object.
    /// </summary>
    /// <param name="obj">The object to compare with this value.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is a matching <see cref="CellValue"/>.</returns>
    public override bool Equals(object? obj) => obj is CellValue other && Equals(other);

    /// <summary>
    /// Returns the hash code for the raw value stored in this cell.
    /// </summary>
    /// <returns>A hash code derived from <see cref="Value"/>.</returns>
    public override int GetHashCode() => Value?.GetHashCode() ?? 0;

    /// <summary>
    /// Returns a string representation of the raw cell value.
    /// </summary>
    /// <returns>The raw value text, or an empty string for null.</returns>
    public override string ToString() => Value?.ToString() ?? string.Empty;

    internal T Require<T>()
    {
        if (Value is T typedValue)
        {
            return typedValue;
        }

        throw CreateTypeMismatch(typeof(T));
    }

    private T? RequireNullable<T>()
    {
        if (Value is null)
        {
            return default;
        }

        if (Value is T typedValue)
        {
            return typedValue;
        }

        throw CreateTypeMismatch(typeof(T));
    }

    private ArgumentException CreateTypeMismatch(Type expectedType)
    {
        var actualTypeName = Value is null ? "null" : Value.GetType().Name;
        return new ArgumentException(
            $"Column '{ColumnName}' in row {RowIndex} contains value type '{actualTypeName}' but expected '{expectedType.Name}' for comparison.");
    }
}
