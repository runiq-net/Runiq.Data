namespace Runiq.Data.IO.Tests.Json;

/// <summary>
/// Verifies JSON reading through the public DataFrame API.
/// </summary>
public sealed class DataFrameJsonReadTests
{
    // Verifies the first supported shape: a JSON array of flat objects with native primitive values.
    [Fact]
    public void ReadJson_WithArrayOfObjects_ReadsRowsColumnsAndNativeTypes()
    {
        using var file = JsonFile("""
            [
              { "name": "Ali", "department": "Engineering", "age": 34, "active": true },
              { "name": "Ayse", "department": "Finance", "age": null, "active": false }
            ]
            """);

        var df = global::Runiq.Data.DataFrame.ReadJson(file.Path);

        Assert.Equal(2, df.Rows.Count());
        Assert.Equal(new[] { "name", "department", "age", "active" }, df.Columns.Select(static c => c.Name));
        Assert.Equal(typeof(string), df["name"].DataType);
        Assert.Equal(typeof(string), df["department"].DataType);
        Assert.Equal(typeof(int?), df["age"].DataType);
        Assert.Equal(typeof(bool), df["active"].DataType);
        Assert.Equal("Ali", df["name"].GetValue(0));
        Assert.Equal("Finance", df["department"].GetValue(1));
        Assert.Null(df["age"].GetValue(1));
        Assert.False((bool)df["active"].GetValue(1)!);
    }

    // Verifies that properties discovered after the first object are appended and missing values become null.
    [Fact]
    public void ReadJson_WithLateProperty_AppendsColumnAndFillsMissingValuesWithNull()
    {
        using var file = JsonFile("""
            [
              { "name": "Ali", "age": 34 },
              { "department": "Finance", "name": "Ayse" }
            ]
            """);

        var df = global::Runiq.Data.DataFrame.ReadJson(file.Path);

        Assert.Equal(new[] { "name", "age", "department" }, df.Columns.Select(static c => c.Name));
        Assert.Equal(typeof(int?), df["age"].DataType);
        Assert.Null(df["department"].GetValue(0));
        Assert.Null(df["age"].GetValue(1));
        Assert.Equal("Finance", df["department"].GetValue(1));
    }

    // Verifies numeric inference keeps the narrowest safe JSON numeric column type.
    [Fact]
    public void ReadJson_InfersSupportedNumericTypes()
    {
        using var file = JsonFile("""
            [
              { "intValue": 1, "longValue": 2147483648, "decimalValue": 12.50, "doubleValue": 1e40 },
              { "intValue": 2, "longValue": 2147483649, "decimalValue": 13.75, "doubleValue": 2e40 }
            ]
            """);

        var df = global::Runiq.Data.DataFrame.ReadJson(file.Path);

        Assert.Equal(typeof(int), df["intValue"].DataType);
        Assert.Equal(typeof(long), df["longValue"].DataType);
        Assert.Equal(typeof(decimal), df["decimalValue"].DataType);
        Assert.Equal(typeof(double), df["doubleValue"].DataType);
        Assert.Equal(2147483649L, df["longValue"].GetValue(1));
        Assert.Equal(13.75m, df["decimalValue"].GetValue(1));
    }

    // Verifies safe numeric promotion across rows without falling back to string.
    [Fact]
    public void ReadJson_WithMixedNumericKinds_UsesSafeNumericPromotion()
    {
        using var file = JsonFile("""
            [
              { "longValue": 1, "decimalValue": 2, "doubleValue": 3.5 },
              { "longValue": 2147483648, "decimalValue": 4.25, "doubleValue": 1e40 }
            ]
            """);

        var df = global::Runiq.Data.DataFrame.ReadJson(file.Path);

        Assert.Equal(typeof(long), df["longValue"].DataType);
        Assert.Equal(typeof(decimal), df["decimalValue"].DataType);
        Assert.Equal(typeof(double), df["doubleValue"].DataType);
        Assert.Equal(1L, df["longValue"].GetValue(0));
        Assert.Equal(2m, df["decimalValue"].GetValue(0));
        Assert.Equal(1e40, df["doubleValue"].GetValue(1));
    }

    // Verifies that an empty array is rejected because no schema can be inferred.
    [Fact]
    public void ReadJson_WithEmptyArray_Throws()
    {
        using var file = JsonFile("[]");

        var exception = Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadJson(file.Path));

        Assert.Contains("schema", exception.Message);
    }

    // Verifies that only an array root is accepted in the first JSON Read release.
    [Fact]
    public void ReadJson_WithObjectRoot_Throws()
    {
        using var file = JsonFile("""{ "name": "Ali" }""");

        var exception = Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadJson(file.Path));

        Assert.Contains("root", exception.Message);
    }

    // Verifies that every array item must be a JSON object.
    [Fact]
    public void ReadJson_WithNonObjectArrayItem_Throws()
    {
        using var file = JsonFile("""[{ "name": "Ali" }, 42]""");

        var exception = Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadJson(file.Path));

        Assert.Contains("item", exception.Message);
    }

    // Verifies nested JSON objects are rejected explicitly instead of being flattened or stringified.
    [Fact]
    public void ReadJson_WithNestedObject_Throws()
    {
        using var file = JsonFile("""[{ "name": "Ali", "department": { "name": "Engineering" } }]""");

        var exception = Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadJson(file.Path));

        Assert.Contains("nested object", exception.Message);
    }

    // Verifies array properties are rejected explicitly instead of being flattened or stringified.
    [Fact]
    public void ReadJson_WithArrayProperty_Throws()
    {
        using var file = JsonFile("""[{ "name": "Ali", "skills": ["C#"] }]""");

        var exception = Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadJson(file.Path));

        Assert.Contains("array", exception.Message);
    }

    // Verifies native JSON type conflicts fail fast rather than silently converting the column to string.
    [Fact]
    public void ReadJson_WithIncompatiblePrimitiveTypes_Throws()
    {
        using var file = JsonFile("""[{ "age": 34 }, { "age": "unknown" }]""");

        var exception = Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadJson(file.Path));

        Assert.Contains("incompatible", exception.Message);
    }

    // Verifies DataFrame's case-insensitive column-name contract is preserved for JSON input.
    [Fact]
    public void ReadJson_WithDuplicatePropertyNamesByCase_Throws()
    {
        using var file = JsonFile("""[{ "name": "Ali", "Name": "Ayse" }]""");

        var exception = Assert.Throws<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadJson(file.Path));

        Assert.Contains("duplicate", exception.Message);
    }

    // Verifies invalid paths are rejected before file access.
    [Fact]
    public void ReadJson_WithEmptyPath_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => global::Runiq.Data.DataFrame.ReadJson(" "));
    }

    private static TemporaryJsonFile JsonFile(string content)
    {
        return new TemporaryJsonFile(content);
    }

    private sealed class TemporaryJsonFile : IDisposable
    {
        public TemporaryJsonFile(string content)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
            File.WriteAllText(Path, content);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
