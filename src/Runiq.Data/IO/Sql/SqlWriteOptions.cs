using System.Data.Common;

namespace Runiq.Data.IO;

/// <summary>
/// Configures provider-independent SQL append behavior for <see cref="DataFrame.WriteSql(DbConnection, string, SqlWriteOptions)"/>.
/// </summary>
/// <remarks>
/// The options model controls only behavior required by SQL Write. It does not create tables,
/// replace data, configure provider-specific quoting, or enable provider-native bulk APIs.
/// </remarks>
public sealed class SqlWriteOptions
{
    /// <summary>
    /// Gets the caller-owned transaction used by SQL Write.
    /// </summary>
    /// <value>
    /// A transaction associated with the same connection passed to SQL Write, or
    /// <see langword="null"/> to let Runiq.Data create an internal transaction.
    /// </value>
    /// <remarks>
    /// External transactions remain fully caller-owned. SQL Write assigns the transaction to
    /// the insert command but never commits, rolls back, or disposes it.
    /// </remarks>
    public DbTransaction? Transaction { get; init; }

    /// <summary>
    /// Gets the command timeout applied to the generated insert command.
    /// </summary>
    /// <value>
    /// A positive timeout in seconds, or <see langword="null"/> to preserve the provider's
    /// default command timeout.
    /// </value>
    /// <remarks>
    /// Zero and negative values are rejected before any database work begins.
    /// </remarks>
    public int? CommandTimeout { get; init; }
}
