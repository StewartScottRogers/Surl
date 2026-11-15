using Surl.Extended.Protocol.Redis;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Joins the composed Redis keyspace to whole-instance reset, snapshot and restore (ADR-0158 decisions 4 to 6).
/// The keyspace is memory only, so it is always controllable.
/// </summary>
internal static class RedisKeyspaceStoreRegistration
{
    /// <summary>
    /// Builds the entry that controls <paramref name="keyspace"/>; the keyspace itself publishes one
    /// <see cref="StoreChange.Reset"/> or <see cref="StoreChange.Restored"/> per run.
    /// </summary>
    /// <param name="keyspace">The keyspace the composed <c>redis</c> and <c>rediss</c> server holds.</param>
    /// <returns>The entry.</returns>
    public static ComposedStoreRegistration For(RedisKeyspace keyspace) =>
        new(
            StoreKind.Keyspace,
            _ =>
            {
                keyspace.Reset();
                return Task.CompletedTask;
            },
            (maxBytes, _) =>
            {
                var snapshot = keyspace.TakeSnapshot(maxBytes);
                return Task.FromResult(new TakenSnapshot(
                    StoreKind.Keyspace,
                    snapshot.ByteCount,
                    _ =>
                    {
                        keyspace.Restore(snapshot);
                        return Task.CompletedTask;
                    }));
            });
}
