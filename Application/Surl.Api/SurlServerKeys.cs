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
    private readonly Action throwIfStopped;

    internal SurlServerKeys(ComposedStores stores, Action throwIfStopped)
    {
        this.stores = stores;
        this.throwIfStopped = throwIfStopped;
    }

    /// <summary>
    /// Sets a string key as <c>SET</c> would, replacing a key that exists; the fault plan is not consulted.
    /// Publishes a <see cref="StoreChangedEvent"/> with <see cref="StoreChange.Seeded"/> for
    /// <see cref="StoreKind.Keyspace"/>. A reset drops what is set here, and sets a key
    /// <see cref="SurlServerOptions.RedisKeys"/> gave back to its start-time value.
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
    /// <exception cref="SurlSeedRefusedException">The keyspace refused the key: too large, full, or no
    /// <c>redis</c> or <c>rediss</c> listen URL is served.</exception>
    public Task SetAsync(string key, string value, TimeSpan? expiry = null, int database = 0, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        return SetAsync(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(value), expiry, database, cancellationToken);
    }

    /// <summary>
    /// Reads one live key back, its name written as UTF-8. Publishes nothing, asks no fault rule, removes no expired
    /// key and returns no secret (ADR-0164, decisions 1 to 3 and 9).
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="database">The database, 0 to 15.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The key: its string value, type, size and expiry (no value for another type).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="SurlNotFoundException">The key is absent or expired, or no Redis server is composed.</exception>
    /// <exception cref="SurlReadRefusedException">The database is outside 0 to 15, or the value is past 16 MiB.</exception>
    /// <exception cref="ObjectDisposedException">The server was stopped.</exception>
    public Task<SurlKeyContents> GetAsync(string key, int database = 0, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        return GetAsync(Encoding.UTF8.GetBytes(key), database, cancellationToken);
    }

    /// <summary>
    /// Reads one live key back from raw key bytes. Publishes nothing, asks no fault rule, removes no expired key and
    /// returns no secret (ADR-0164, decisions 1 to 3 and 9).
    /// </summary>
    /// <param name="key">The key's bytes.</param>
    /// <param name="database">The database, 0 to 15.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The key: its string value, type, size and expiry (no value for another type).</returns>
    /// <exception cref="SurlNotFoundException">The key is absent or expired, or no Redis server is composed.</exception>
    /// <exception cref="SurlReadRefusedException">The database is outside 0 to 15, or the value is past 16 MiB.</exception>
    /// <exception cref="ObjectDisposedException">The server was stopped.</exception>
    public Task<SurlKeyContents> GetAsync(ReadOnlyMemory<byte> key, int database = 0, CancellationToken cancellationToken = default)
    {
        throwIfStopped();
        cancellationToken.ThrowIfCancellationRequested();
        return SurlReadResults.Completed(() =>
        {
            var read = SurlReadResults.ValueOrThrow(stores.ReadKey(database, key));
            return new SurlKeyContents(InfoOf(read.Info), read.Value);
        });
    }

    /// <summary>
    /// Lists live keys matching a Redis <c>KEYS</c> glob in ordinal order of their bytes, one page at a time, with
    /// each key's type, size and expiry and no value. Publishes nothing and asks no fault rule (ADR-0164, decisions
    /// 2, 3 and 9).
    /// </summary>
    /// <param name="pattern">The glob, matched on bytes; <c>*</c> lists every key.</param>
    /// <param name="afterKey">The last key seen, or <see langword="null"/> to start at the first.</param>
    /// <param name="limit">The most keys to return, 1 to 1000.</param>
    /// <param name="database">The database, 0 to 15.</param>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>The page; empty when no Redis server is composed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is null.</exception>
    /// <exception cref="SurlReadRefusedException">The limit is outside 1 to 1000 or the database outside 0 to 15.</exception>
    /// <exception cref="ObjectDisposedException">The server was stopped.</exception>
    public Task<SurlKeyPage> ListAsync(string pattern, string? afterKey = null, int limit = 100, int database = 0, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        throwIfStopped();
        cancellationToken.ThrowIfCancellationRequested();
        return SurlReadResults.Completed(() =>
        {
            var after = afterKey is null ? default : Encoding.UTF8.GetBytes(afterKey);
            var page = SurlReadResults.ValueOrThrow(stores.ListKeys(database, pattern, after, limit));
            return new SurlKeyPage([.. page.Keys.Select(InfoOf)], page.HasMore);
        });
    }

    private static SurlKeyInfo InfoOf(Surl.Extended.Protocol.Redis.RedisKeyInfo info) =>
        new(info.Key, info.Type, info.ByteCount, info.ExpiresAt);

    /// <summary>
    /// Sets a string key from raw bytes as <c>SET</c> would, replacing a key that exists; the fault plan is not
    /// consulted. Publishes a <see cref="StoreChangedEvent"/> with <see cref="StoreChange.Seeded"/> for
    /// <see cref="StoreKind.Keyspace"/>. A reset drops what is set here, and sets a key
    /// <see cref="SurlServerOptions.RedisKeys"/> gave back to its start-time value.
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
    /// <exception cref="SurlSeedRefusedException">The keyspace refused the key: too large, full, or no
    /// <c>redis</c> or <c>rediss</c> listen URL is served.</exception>
    public Task SetAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, TimeSpan? expiry = null, int database = 0, CancellationToken cancellationToken = default) =>
        SeedAsync(stores, database, key, value, expiry, keepOnReset: false, cancellationToken);

    /// <summary>
    /// Seeds one string key into <paramref name="stores"/>' keyspace, saying whether a reset returns to it, and turns
    /// the keyspace's refusal into the exception a test sees.
    /// </summary>
    /// <exception cref="SurlSeedRefusedException">The keyspace refused the key.</exception>
    internal static async Task SeedAsync(
        ComposedStores stores,
        int database,
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> value,
        TimeSpan? expiry,
        bool keepOnReset,
        CancellationToken cancellationToken)
    {
        try
        {
            await stores.SeedKeyAsync(database, key, value, expiry, keepOnReset, cancellationToken);
        }
        catch (InvalidOperationException refused)
        {
            throw new SurlSeedRefusedException(StoreKind.Keyspace, refused.Message.TrimEnd('.'), $"A key in database {database}");
        }
    }
}
