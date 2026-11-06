using Surl.Composition;

namespace Surl.Api;

/// <summary>
/// An opaque, in-memory copy of a <see cref="SurlServer"/>'s mail, files, messages and accounts, taken by
/// <see cref="SurlServer.SnapshotAsync"/> and given back by <see cref="SurlServer.RestoreAsync"/> on the same server
/// only (ADR-0124, decision 3). Accounts are held as password hashes, never a clear password.
/// </summary>
public sealed class SurlSnapshot
{
    internal SurlSnapshot(SurlServer owner, ComposedSnapshot stores)
    {
        Owner = owner;
        Stores = stores;
    }

    /// <summary>How many bytes of stored contents the snapshot holds.</summary>
    public long ByteCount => Stores.ByteCount;

    internal SurlServer Owner { get; }

    internal ComposedSnapshot Stores { get; }
}
