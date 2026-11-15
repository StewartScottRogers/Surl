using System.Text;
using Surl.Composition;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// A running <see cref="SurlServer"/>'s Redis keyspace, as a test seeds it (ADR-0158, decision 9): the one
/// keyspace the <c>redis</c> server reads and writes. Only string keys are seeded; any other Redis type is made
/// by the commands a client sends. Thread-safe.
/// </summary>
public sealed class SurlServerKeys
{
    private readonly ComposedStores stores;

    internal SurlServerKeys(ComposedStores stores) => this.stores = stores;

    /// <summary>
    /// Sets a string key as <c>SET</c> would, replacing a key that exists; the fault plan is not consulted.
    /// Publishes a <see cref="StoreChangedEvent"/> with <see cref="StoreChange.Seeded"/> for
    /// <see cref="StoreKind.Keyspace"/>.
    /// </summary>
    /// <param name="key">The key, at least one character, written as UTF-8.</param>
    /// <param name="value">The value, written as UTF-8.</param>
    /// <param name="expiry">How long the key lives, or <see langword="null"/> for no expiry.</param>
    /// <param name="database">The database, 0 to 15.</param>
    /// <param name="cancellationToken">Cancels the seed before it starts.</param>
    /// <returns>A task that completes once the key is written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="database"/> is outside 0 to 15, or
    /// <paramref name="expiry"/> is outside 1 millisecond to 365 days.</exception>
    /// <exception cref="SurlSeedRefusedException">The keyspace refused the key: too large, full, or no Redis server
    /// is composed.</exception>
    public Task SetAsync(string key, string value, TimeSpan? expiry = null, int database = 0, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        return SetAsync(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(value), expiry, database, cancellationToken);
    }

    /// <summary>
    /// Sets a string key from raw bytes as <c>SET</c> would, replacing a key that exists; the fault plan is not
    /// consulted. Publishes a <see cref="StoreChangedEvent"/> with <see cref="StoreChange.Seeded"/> for
    /// <see cref="StoreKind.Keyspace"/>.
    /// </summary>
    /// <param name="key">The key, at least one byte.</param>
    /// <param name="value">The value.</param>
    /// <param name="expiry">How long the key lives, or <see langword="null"/> for no expiry.</param>
    /// <param name="database">The database, 0 to 15.</param>
    /// <param name="cancellationToken">Cancels the seed before it starts.</param>
    /// <returns>A task that completes once the key is written.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="database"/> is outside 0 to 15, or
    /// <paramref name="expiry"/> is outside 1 millisecond to 365 days.</exception>
    /// <exception cref="SurlSeedRefusedException">The keyspace refused the key: too large, full, or no Redis server
    /// is composed.</exception>
    public async Task SetAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, TimeSpan? expiry = null, int database = 0, CancellationToken cancellationToken = default)
    {
        try
        {
            await stores.SeedKeyAsync(database, key, value, expiry, cancellationToken);
        }
        catch (InvalidOperationException refused)
        {
            throw new SurlSeedRefusedException(StoreKind.Keyspace, refused.Message.TrimEnd('.'), $"A key in database {database}");
        }
    }
}
