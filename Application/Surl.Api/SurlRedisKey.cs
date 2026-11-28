using System.Text;

namespace Surl.Api;

/// <summary>
/// A string key the server's Redis keyspace holds before any listener binds and returns to on a reset, held in
/// <see cref="SurlServerOptions.RedisKeys"/> (ADR-0158, decisions 3 and 4). A start checks it: an empty key, a
/// database outside 0 to 15 or an expiry outside 1 millisecond to 365 days is an <see cref="ArgumentException"/>.
/// </summary>
/// <param name="Key">The key, at least one byte.</param>
/// <param name="Value">The value, which may be empty; with the key, at most 16 MiB.</param>
/// <param name="Expiry">
/// How long the key lives on <see cref="SurlServerOptions.TimeProvider"/>, measured from the start and again from each
/// reset, 1 millisecond to 365 days, or <see langword="null"/>, the default, for no expiry.
/// </param>
/// <param name="Database">The database, 0, the default, to 15.</param>
public sealed record SurlRedisKey(ReadOnlyMemory<byte> Key, ReadOnlyMemory<byte> Value, TimeSpan? Expiry = null, int Database = 0)
{
    /// <summary>A key and a value written as UTF-8.</summary>
    /// <param name="key">The key, at least one character.</param>
    /// <param name="value">The value, which may be empty.</param>
    /// <param name="expiry">How long the key lives, or <see langword="null"/>, the default, for no expiry.</param>
    /// <param name="database">The database, 0, the default, to 15.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="value"/> is null.</exception>
    public SurlRedisKey(string key, string value, TimeSpan? expiry = null, int database = 0)
        : this(Utf8BytesOf(key, nameof(key)), Utf8BytesOf(value, nameof(value)), expiry, database)
    {
    }

    private static byte[] Utf8BytesOf(string text, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(text, parameterName);
        return Encoding.UTF8.GetBytes(text);
    }
}
