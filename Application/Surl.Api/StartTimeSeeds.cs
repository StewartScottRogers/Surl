using Surl.Composition;

namespace Surl.Api;

/// <summary>
/// Applies the seeds <see cref="SurlServerOptions"/> gives to a composed server's stores, before any listener binds
/// (ADR-0158, decision 3).
/// </summary>
internal static class StartTimeSeeds
{
    /// <summary>
    /// Seeds <see cref="SurlServerOptions.RedisKeys"/>, then <see cref="SurlServerOptions.DirectoryLdif"/>, then
    /// <see cref="SurlServerOptions.DnsZones"/>, each as what a reset returns to, publishing what a seed made while
    /// serving publishes. The first refusal stops it.
    /// </summary>
    /// <param name="stores">The composed server's stores.</param>
    /// <param name="options">The options, already checked by <see cref="SurlServerArguments.ThrowIfMisused"/>.</param>
    /// <param name="cancellationToken">Cancels before the next seed.</param>
    /// <returns>A task that completes once every seed is held.</returns>
    /// <exception cref="SurlSeedRefusedException">A store refused a seed, or no listen URL serves it.</exception>
    public static async Task ApplyAsync(ComposedStores stores, SurlServerOptions options, CancellationToken cancellationToken)
    {
        foreach (var key in options.RedisKeys)
        {
            await SurlServerKeys.SeedAsync(
                stores, key.Database, key.Key, key.Value, key.Expiry, keepOnReset: true, cancellationToken);
        }

        if (options.DirectoryLdif is { } ldif)
        {
            await SurlServerDirectory.SeedAsync(stores, ldif, keepOnReset: true, cancellationToken);
        }

        foreach (var zone in options.DnsZones)
        {
            await SurlServerZones.SeedAsync(stores, zone.Apex, zone.ZoneFileText, keepOnReset: true, cancellationToken);
        }
    }
}
