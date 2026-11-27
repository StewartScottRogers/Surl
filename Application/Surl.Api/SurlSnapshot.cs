using Surl.Composition;

namespace Surl.Api;

/// <summary>
/// An opaque, in-memory snapshot of every store a <see cref="SurlServer"/> holds, each named by a
/// <see cref="Surl.Protocol.Abstractions.StoreKind"/>, taken by <see cref="SurlServer.SnapshotAsync"/> and given back
/// by <see cref="SurlServer.RestoreAsync"/> on the same server only (ADR-0124, decision 3). Which stores a server
/// holds depends on how it was started.
/// <para>
/// A restore puts back only what the snapshot copied. No open or staged object upload (S3, Azure Storage, Google
/// Cloud Storage, OCI), OCI registry token, or MCP server OAuth state or HTTP session is copied: a restore discards,
/// revokes or ends each one there is when it runs, even one that began before the snapshot was taken, and discards the
/// Azure Storage server's remembered committed block lists too. A restore also drops every Kafka consumer group, and
/// discards the MQTT 5 brokers' pending Wills, queued deliveries and unacknowledged publishes.
/// </para>
/// <para>
/// Accounts, proxy accounts and TLS-SRP accounts are copied, though they add nothing to <see cref="ByteCount"/>. A
/// TLS-SRP account is held as its user name, salt, group and verifier, never its password. The other two are held
/// with the credentials the server derived from each password, which for a named account include the password's UTF-8
/// bytes, so a snapshot holds secrets.
/// </para>
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
