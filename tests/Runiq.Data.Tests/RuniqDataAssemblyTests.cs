using System.Reflection;

namespace Runiq.Data.Tests;

/// <summary>
/// Verifies basic project wiring for the Runiq.Data assembly.
/// </summary>
public sealed class RuniqDataAssemblyTests
{
    /// <summary>
    /// Verifies that the Runiq.Data class library assembly can be loaded.
    /// </summary>
    [Fact]
    public void Assembly_can_be_loaded()
    {
        // Verifies that the referenced class library assembly is available to tests.
        var assembly = Assembly.Load("Runiq.Data");

        Assert.Equal("Runiq.Data", assembly.GetName().Name);
    }
}
