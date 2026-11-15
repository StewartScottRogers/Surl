using Surl.Extended.Protocol.DnsQuery;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Joins the composed DNS zones to whole-instance reset, snapshot and restore (ADR-0158 decisions 4 to 6).
/// Zones loaded from <c>--directory</c> are an in-memory copy of files read once, so reset returns to them and
/// no file is ever written.
/// </summary>
internal static class DnsZoneStoreRegistration
{
    /// <summary>
    /// Builds the entry that controls <paramref name="zones"/>; the set itself publishes one
    /// <see cref="StoreChange.Reset"/> or <see cref="StoreChange.Restored"/> per run.
    /// </summary>
    /// <param name="zones">The zones the composed <c>dns</c> server answers from.</param>
    /// <returns>The entry.</returns>
    public static ComposedStoreRegistration For(DnsZoneSet zones) =>
        new(
            StoreKind.Zones,
            _ =>
            {
                zones.Reset();
                return Task.CompletedTask;
            },
            (maxBytes, _) =>
            {
                var snapshot = zones.TakeSnapshot(maxBytes);
                return Task.FromResult(new TakenSnapshot(
                    StoreKind.Zones,
                    snapshot.ByteCount,
                    _ =>
                    {
                        zones.Restore(snapshot);
                        return Task.CompletedTask;
                    }));
            });
}
