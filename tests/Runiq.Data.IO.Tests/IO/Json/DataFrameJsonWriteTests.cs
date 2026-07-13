using System.Text.Json;

namespace Runiq.Data.IO.Tests.Json;

/// <summary>
/// Verifies JSON writing through the public DataFrame API.
/// </summary>
public sealed class DataFrameJsonWriteTests
{
    // Verifies that default writing creates an indented JSON array of objects with preserved row and column order.
    [Fact]
    public void WriteJson_DefaultUsage_WritesIndentedArrayOfObjects()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("employees.json");
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Name = new[] { "Ali", "Ayse" },
            Age = new int?[] { 34, null },
            Active = new[] { true, false }
        });

        df.WriteJson(path);

        var json = File.ReadAllText(path);
        Assert.Contains("\n", json);
        Assert.Contains("  \"Name\"", json);
        using var document = JsonDocument.Parse(json);
        var rows = document.RootElement.EnumerateArray().ToArray();
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Equal(["Name", "Age", "Active"], rows[0].EnumerateObject().Select(static property => property.Name));
        Assert.Equal("Ali", rows[0].GetProperty("Name").GetString());
        Assert.Equal(34, rows[0].GetProperty("Age").GetInt32());
        Assert.True(rows[0].GetProperty("Active").GetBoolean());
        Assert.Equal("Ayse", rows[1].GetProperty("Name").GetString());
        Assert.Equal(JsonValueKind.Null, rows[1].GetProperty("Age").ValueKind);
        Assert.False(rows[1].GetProperty("Active").GetBoolean());
    }

    // Verifies that explicit options can produce compact JSON without changing cell values.
    [Fact]
    public void WriteJson_WithCompactOptions_WritesCompactJson()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("employees.json");
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" }, Age = new[] { 34 } });

        df.WriteJson(path, new global::Runiq.Data.JsonWriteOptions { WriteIndented = false });

        Assert.Equal("""[{"Name":"Ali","Age":34}]""", File.ReadAllText(path));
    }

    // Verifies that a zero-row DataFrame writes an empty JSON array without requiring schema metadata.
    [Fact]
    public void WriteJson_WithNoRows_WritesEmptyArray()
    {
        using var directory = TemporaryDirectory.Create();
        var csvPath = directory.FilePath("empty.csv");
        var jsonPath = directory.FilePath("empty.json");
        File.WriteAllText(csvPath, string.Empty);
        var df = global::Runiq.Data.DataFrame.ReadCsv(
            csvPath,
            new global::Runiq.Data.CsvReadOptions { Header = global::Runiq.Data.CsvHeaderMode.Absent, Names = ["Name", "Age"] });

        df.WriteJson(jsonPath);

        Assert.Equal("[]", File.ReadAllText(jsonPath));
    }

    // Verifies native JSON primitives, ISO date/time strings, Guid strings, enum names, and string preservation rules.
    [Fact]
    public void WriteJson_WithSupportedValues_WritesExpectedJsonKinds()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("values.json");
        var guid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Text = new[] { "=SUM(A1:A2)", "true", "123", "null", "2026-07-13" },
            Boolean = new[] { true, false, true, false, true },
            IntValue = new[] { 1, 2, 3, 4, 5 },
            LongValue = new[] { 2_147_483_648L, 2L, 3L, 4L, 5L },
            DecimalValue = new[] { 12.50m, 2m, 3m, 4m, 5m },
            FloatValue = new[] { 1.5f, 2f, 3f, 4f, 5f },
            DoubleValue = new[] { 2.5d, 3d, 4d, 5d, 6d },
            CreatedAt = Enumerable.Repeat(new DateTime(2026, 7, 13, 15, 30, 0, DateTimeKind.Utc), 5).ToArray(),
            OffsetAt = Enumerable.Repeat(new DateTimeOffset(2026, 7, 13, 18, 30, 0, TimeSpan.FromHours(3)), 5).ToArray(),
            Id = Enumerable.Repeat(guid, 5).ToArray(),
            Status = Enumerable.Repeat(SampleStatus.Completed, 5).ToArray()
        });

        df.WriteJson(path, new global::Runiq.Data.JsonWriteOptions { WriteIndented = false });

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var first = document.RootElement[0];
        Assert.Equal(JsonValueKind.String, first.GetProperty("Text").ValueKind);
        Assert.Equal("=SUM(A1:A2)", first.GetProperty("Text").GetString());
        Assert.Equal(JsonValueKind.True, first.GetProperty("Boolean").ValueKind);
        Assert.Equal(1, first.GetProperty("IntValue").GetInt32());
        Assert.Equal(2_147_483_648L, first.GetProperty("LongValue").GetInt64());
        Assert.Equal(12.50m, first.GetProperty("DecimalValue").GetDecimal());
        Assert.Equal(1.5f, first.GetProperty("FloatValue").GetSingle());
        Assert.Equal(2.5d, first.GetProperty("DoubleValue").GetDouble());
        Assert.Equal("2026-07-13T15:30:00.0000000Z", first.GetProperty("CreatedAt").GetString());
        Assert.Equal("2026-07-13T18:30:00.0000000+03:00", first.GetProperty("OffsetAt").GetString());
        Assert.Equal("00112233-4455-6677-8899-aabbccddeeff", first.GetProperty("Id").GetString());
        Assert.Equal("Completed", first.GetProperty("Status").GetString());
        Assert.Equal("true", document.RootElement[1].GetProperty("Text").GetString());
        Assert.Equal("123", document.RootElement[2].GetProperty("Text").GetString());
        Assert.Equal("null", document.RootElement[3].GetProperty("Text").GetString());
        Assert.Equal("2026-07-13", document.RootElement[4].GetProperty("Text").GetString());
    }

    // Verifies that non-finite floating-point values are rejected rather than written as non-standard JSON tokens.
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void WriteJson_WithNonFiniteDouble_Throws(double value)
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("values.json");
        var df = global::Runiq.Data.DataFrame.Create(new { Value = new[] { value } });

        Assert.Throws<ArgumentException>(() => df.WriteJson(path));
    }

    // Verifies that unsupported objects, collections, and dictionaries fail without ToString fallback.
    [Theory]
    [MemberData(nameof(UnsupportedValues))]
    public void WriteJson_WithUnsupportedRuntimeValue_ThrowsDiagnosticException(object value)
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("objects.json");
        File.WriteAllText(path, "existing");
        var df = global::Runiq.Data.DataFrame.Create(new { Metadata = new object?[] { "ok", value } });

        var exception = Assert.Throws<ArgumentException>(() => df.WriteJson(path));

        Assert.Contains("Metadata", exception.Message);
        Assert.Contains("row 1", exception.Message);
        Assert.Contains(value.GetType().ToString(), exception.Message);
        Assert.Equal("existing", File.ReadAllText(path));
        Assert.Empty(Directory.EnumerateFiles(directory.Path, ".runiq-data-*.tmp.json"));
    }

    // Verifies invalid public arguments and directory targets are rejected without wrapping natural exceptions.
    [Fact]
    public void WriteJson_WithInvalidArguments_Throws()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("employees.json");
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" } });

        Assert.Throws<ArgumentNullException>(() => df.WriteJson(null!));
        Assert.Throws<ArgumentException>(() => df.WriteJson(string.Empty));
        Assert.Throws<ArgumentException>(() => df.WriteJson("   "));
        Assert.Throws<ArgumentNullException>(() => df.WriteJson(path, null!));
        Assert.ThrowsAny<ArgumentException>(() => df.WriteJson("bad\0path.json"));
        Assert.True(Record.Exception(() => df.WriteJson(directory.Path)) is IOException or UnauthorizedAccessException);
    }

    // Verifies successful replacement overwrites an existing target and leaves no temporary artifact behind.
    [Fact]
    public void WriteJson_WithExistingFile_ReplacesContentAndRemovesTemporaryFile()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("employees.json");
        File.WriteAllText(path, "this content is longer than the replacement");
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali" } });

        df.WriteJson(path, new global::Runiq.Data.JsonWriteOptions { WriteIndented = false });

        Assert.Equal("""[{"Name":"Ali"}]""", File.ReadAllText(path));
        Assert.Empty(Directory.EnumerateFiles(directory.Path, ".runiq-data-*.tmp.json"));
    }

    // Verifies that JSON output can be parsed back as a valid array of objects.
    [Fact]
    public void WriteJson_OutputParsesAsArrayOfObjects()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("employees.json");
        var df = global::Runiq.Data.DataFrame.Create(new { Name = new[] { "Ali", "Ayse" } });

        df.WriteJson(path);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.All(document.RootElement.EnumerateArray(), row => Assert.Equal(JsonValueKind.Object, row.ValueKind));
    }

    // Verifies JSON Write followed by JSON Read preserves representative primitive values supported by both APIs.
    [Fact]
    public void WriteJson_ThenReadJson_RoundTripsPrimitiveValues()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.FilePath("roundtrip.json");
        var df = global::Runiq.Data.DataFrame.Create(new
        {
            Text = new[] { "value", "other" },
            Boolean = new[] { true, false },
            IntValue = new int?[] { 1, null },
            LongValue = new[] { 2_147_483_648L, 3L },
            DecimalValue = new[] { 12.50m, 13.75m },
            DoubleValue = new[] { 1e40, 2e40 }
        });

        df.WriteJson(path);
        var loaded = global::Runiq.Data.DataFrame.ReadJson(path);

        Assert.Equal("value", loaded["Text"].GetValue(0));
        Assert.Equal(false, loaded["Boolean"].GetValue(1));
        Assert.Equal(1, loaded["IntValue"].GetValue(0));
        Assert.Null(loaded["IntValue"].GetValue(1));
        Assert.Equal(2_147_483_648L, loaded["LongValue"].GetValue(0));
        Assert.Equal(12.50m, loaded["DecimalValue"].GetValue(0));
        Assert.Equal(2e40, loaded["DoubleValue"].GetValue(1));
    }

    /// <summary>
    /// Supplies representative values that JSON Write intentionally rejects as ambiguous or nested.
    /// </summary>
    public static TheoryData<object> UnsupportedValues()
    {
        return new TheoryData<object>
        {
            new CustomMetadata(),
            new[] { "nested" },
            new Dictionary<string, object?> { ["key"] = "value" }
        };
    }

    /// <summary>
    /// Provides a stable enum value for verifying name-based JSON enum output.
    /// </summary>
    private enum SampleStatus
    {
        /// <summary>
        /// Represents an unfinished sample status.
        /// </summary>
        Pending,

        /// <summary>
        /// Represents a finished sample status.
        /// </summary>
        Completed
    }

    /// <summary>
    /// Provides a custom object type that must be rejected rather than stringified.
    /// </summary>
    private sealed class CustomMetadata
    {
        /// <summary>
        /// Gets a sample value that should not affect unsupported object diagnostics.
        /// </summary>
        public string Name { get; } = "custom";
    }

    /// <summary>
    /// Provides isolated directories and best-effort cleanup for JSON write tests.
    /// </summary>
    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        internal string Path { get; }

        // Creates a unique directory so replacement and cleanup assertions are isolated per test.
        internal static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Runiq.Data.IO.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        // Builds child paths inside the owned temporary directory without creating the file.
        internal string FilePath(string fileName)
        {
            return System.IO.Path.Combine(Path, fileName);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // Test cleanup is best-effort and must not mask assertion failures.
            }
        }
    }
}
