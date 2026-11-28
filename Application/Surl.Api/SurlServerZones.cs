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

    private readonly Action throwIfStopped;

    internal SurlServerZones(ComposedStores stores, Action throwIfStopped)
    {
        this.stores = stores;
        this.throwIfStopped = throwIfStopped;
    }

    /// <summary>
    /// Adds one zone read from zone-file text; the fault plan is not consulted. A zone whose apex is already held
    /// is refused. A reset drops a zone added here and keeps those <see cref="SurlServerOptions.DnsZones"/> gave; a
    /// server with a <see cref="SurlServerOptions.DataDirectory"/> or a share on disk refuses the reset
    /// (<see cref="SurlServer.ResetAsync"/>). Publishes a <see cref="StoreChangedEvent"/> with
    /// <see cref="StoreChange.Seeded"/> for <see cref="StoreKind.Zones"/>.
    /// </summary>
    /// <param name="apex">The zone's apex, a domain name with or without its final dot.</param>
    /// <param name="zoneFileText">The zone file's text, at most 16 MiB.</param>
    /// <param name="cancellationToken">Cancels the seed before it starts.</param>
    /// <returns>A task that completes once the zone is added.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="apex"/> or <paramref name="zoneFileText"/> is null.</exception>
    /// <exception cref="SurlSeedRefusedException">The zones refused it: text that cannot be a zone, a second zone
    /// for the apex, a bound passed, or no <c>dns</c> listen URL is served.</exception>
    public Task AddZoneAsync(string apex, string zoneFileText, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(apex);
        ArgumentNullException.ThrowIfNull(zoneFileText);
        return SeedAsync(stores, apex, zoneFileText, keepOnReset: false, cancellationToken);
    }

    /// <summary>
    /// Seeds one zone into <paramref name="stores"/>' DNS zones, saying whether a reset returns to it, and turns the
    /// zones' refusal into the exception a test sees.
    /// </summary>
    /// <exception cref="SurlSeedRefusedException">The zones refused it.</exception>
    internal static async Task SeedAsync(
        ComposedStores stores, string apex, string zoneFileText, bool keepOnReset, CancellationToken cancellationToken)
    {
        try
        {
            await stores.SeedZoneAsync(apex, zoneFileText, keepOnReset, cancellationToken);
        }
        catch (Exception refused) when (refused is not OperationCanceledException)
        {
            throw new SurlSeedRefusedException(StoreKind.Zones, refused.Message.TrimEnd('.'), $"The zone {apex}");
        }
    }

    /// <summary>
    /// Lists the apexes of the zones held, each with its final dot (<c>example.com.</c>), in the order they were
    /// added. Publishes nothing and asks no fault rule (ADR-0164, decisions 2, 3 and 9).
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The apexes; none when no <c>dns</c> listen URL is served.</returns>
    /// <exception cref="ObjectDisposedException">The server was stopped.</exception>
    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        throwIfStopped();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(stores.ListZoneApexes());
    }

    /// <summary>
    /// Reads one zone back as master-file text: a record a line, absolute names, a TTL and the class <c>IN</c>, RDATA
    /// as the zone-file reader reads it. Publishes nothing and asks no fault rule (ADR-0164, decisions 1 to 3 and 9).
    /// </summary>
    /// <param name="apex">The zone's apex, with or without its final dot.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The zone-file text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="apex"/> is null.</exception>
    /// <exception cref="SurlNotFoundException">No zone has the apex, or no <c>dns</c> listen URL is served.</exception>
    /// <exception cref="SurlReadRefusedException">The text is past 16 MiB.</exception>
    /// <exception cref="ObjectDisposedException">The server was stopped.</exception>
    public Task<string> ReadAsync(string apex, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(apex);
        throwIfStopped();
        cancellationToken.ThrowIfCancellationRequested();
        return SurlReadResults.Completed(() => SurlReadResults.ValueOrThrow(stores.ReadZone(apex)));
    }
}
