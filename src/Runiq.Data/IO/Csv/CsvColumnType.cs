namespace Runiq.Data.IO;

/// <summary>
/// Stores the inferred CLR type and nullability contract for one CSV column.
/// </summary>
internal readonly record struct CsvColumnType(Type DataType, bool IsNullable);
