using Surl.Composition;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// A running <see cref="SurlServer"/>'s DNS zones, as a test seeds them (ADR-0158, decision 9): the zones the
/// <c>dns</c> server answers from. Thread-safe.
/// </summary>
public sealed class SurlServerZones
{
    private readonly ComposedStores stores;

    internal SurlServerZones(ComposedStores stores) => this.stores = stores;

    /// <summary>
    /// Adds one zone read from zone-file text; the fault plan is not consulted. A zone whose apex is already held
    /// is refused: a reset drops it. Publishes a <see cref="StoreChangedEvent"/> with
    /// <see cref="StoreChange.Seeded"/> for <see cref="StoreKind.Zones"/>.
    /// </summary>
    /// <param name="apex">The zone's apex, a domain name with or without its final dot.</param>
    /// <param name="zoneFileText">The zone file's text, at most 16 MiB.</param>
    /// <param name="cancellationToken">Cancels the seed before it starts.</param>
    /// <returns>A task that completes once the zone is added.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="apex"/> or <paramref name="zoneFileText"/> is null.</exception>
    /// <exception cref="SurlSeedRefusedException">The zones refused it: text that cannot be a zone, a second zone
    /// for the apex, a bound passed, or no DNS server is composed.</exception>
    public async Task AddZoneAsync(string apex, string zoneFileText, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(apex);
        ArgumentNullException.ThrowIfNull(zoneFileText);
        try
        {
            await stores.SeedZoneAsync(apex, zoneFileText, cancellationToken);
        }
        catch (Exception refused) when (refused is not OperationCanceledException)
        {
            throw new SurlSeedRefusedException(StoreKind.Zones, refused.Message.TrimEnd('.'), $"The zone {apex}");
        }
    }
}
