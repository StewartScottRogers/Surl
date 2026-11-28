using System.Text.Json;
using Surl.Api;

namespace Surl.Mcp;

/// <summary>
/// Reads the Redis key <c>set_key</c>'s arguments or one item of <c>start_server</c>'s <c>redis_keys</c> describe
/// (ADR-0158 decision 9): <c>key</c> or <c>key_base64</c>, <c>value</c> or <c>value_base64</c> (each as
/// <see cref="SeededContent"/> reads it), <c>database</c> (default 0) and <c>expire_ms</c> (default: never). An empty
/// key is refused.
/// </summary>
internal static class RedisKeyArguments
{
    /// <summary>Reads the key <paramref name="arguments"/> describe.</summary>
    /// <param name="arguments">The object holding the properties; the schema has checked their types and ranges.</param>
    /// <param name="key">The key, or <see langword="null"/> when refused.</param>
    /// <returns>Why the key is refused, never showing its value, or <see langword="null"/>.</returns>
    public static string? TryRead(JsonElement arguments, out SurlRedisKey? key)
    {
        key = null;
        byte[]? value = null;
        if ((SeededContent.TryRead(arguments, "key", "key_base64", out var name) ?? SeededContent.TryRead(arguments, "value", "value_base64", out value)) is { } refusal)
        {
            return refusal;
        }

        if (name!.Length == 0)
        {
            var property = arguments.TryGetProperty("key", out _) ? "key" : "key_base64";
            return $"{property}: a key needs at least one byte";
        }

        var database = arguments.TryGetProperty("database", out var databaseValue) ? databaseValue.GetWholeInt32() : 0;
        TimeSpan? expiry = arguments.TryGetProperty("expire_ms", out var expireValue) ? TimeSpan.FromMilliseconds(expireValue.GetWholeInt64()) : null;
        key = new SurlRedisKey(name, value!, expiry, database);
        return null;
    }
}
