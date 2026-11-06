using System.Buffers.Text;
using System.Security.Cryptography;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// One kind of thing the authorization server issues - clients, codes, access tokens or refresh tokens - held in
/// memory under keys of 32 random bytes, base64url without padding (ADR-0111 decision 6.3). An entry past its
/// expiry is never found; at most <see cref="MaximumEntries" /> live at once. Safe to call from several threads.
/// </summary>
/// <typeparam name="T">What the table holds.</typeparam>
/// <param name="randomSource">Where keys come from: <see cref="RandomNumberGenerator" /> in production.</param>
/// <param name="timeProvider">The clock expiry is measured on.</param>
internal sealed class McpExpiringTable<T>(RandomNumberGenerator randomSource, TimeProvider timeProvider)
    where T : class
{
    /// <summary>The most live entries the table holds.</summary>
    public const int MaximumEntries = 1000;

    /// <summary>The bytes of randomness in one key.</summary>
    public const int KeyBytes = 32;

    private readonly Dictionary<string, (T Value, DateTimeOffset ExpiresAt)> _entries = new(StringComparer.Ordinal);

    private readonly Lock _lock = new();

    /// <summary>Gets the time on the table's clock.</summary>
    public DateTimeOffset Now => timeProvider.GetUtcNow();

    /// <summary>Adds an entry under a new random key.</summary>
    /// <param name="value">The entry.</param>
    /// <param name="expiresAt">When it stops being found; <see cref="DateTimeOffset.MaxValue" /> for never.</param>
    /// <returns>Its key, or <see langword="null" /> when the table is full and nothing was added.</returns>
    public string? Add(T value, DateTimeOffset expiresAt)
    {
        lock (_lock)
        {
            RemoveExpired();
            if (_entries.Count >= MaximumEntries)
            {
                return null;
            }

            byte[] random = new byte[KeyBytes];
            randomSource.GetBytes(random);
            string key = Base64Url.EncodeToString(random);
            _entries[key] = (value, expiresAt);
            return key;
        }
    }

    /// <summary>Finds a live entry.</summary>
    /// <param name="key">Its key.</param>
    /// <returns>The entry, or <see langword="null" /> when no live entry has the key.</returns>
    public T? Find(string key)
    {
        lock (_lock)
        {
            return _entries.TryGetValue(key, out (T Value, DateTimeOffset ExpiresAt) entry) && Now < entry.ExpiresAt ? entry.Value : null;
        }
    }

    /// <summary>Removes an entry and returns it if it was live: a code or a refresh token is used once.</summary>
    /// <param name="key">Its key.</param>
    /// <returns>The entry, or <see langword="null" /> when no live entry had the key.</returns>
    public T? Take(string key)
    {
        lock (_lock)
        {
            return _entries.Remove(key, out (T Value, DateTimeOffset ExpiresAt) entry) && Now < entry.ExpiresAt ? entry.Value : null;
        }
    }

    private void RemoveExpired()
    {
        DateTimeOffset now = Now;
        foreach (string key in _entries.Where(entry => now >= entry.Value.ExpiresAt).Select(entry => entry.Key).ToArray())
        {
            _entries.Remove(key);
        }
    }
}
