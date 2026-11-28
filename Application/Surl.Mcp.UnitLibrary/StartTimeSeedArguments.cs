using System.Text.Json;
using Surl.Api;

namespace Surl.Mcp;

/// <summary>
/// Reads <c>start_server</c>'s start-time seeds, <c>redis_keys</c>, <c>directory_ldif</c> and <c>dns_zones</c>, each
/// in the shape of its seed tool (ADR-0158 decisions 3 and 9), into <see cref="SurlServerOptions.RedisKeys"/>,
/// <see cref="SurlServerOptions.DirectoryLdif"/> and <see cref="SurlServerOptions.DnsZones"/>. No local path is read.
/// </summary>
internal static class StartTimeSeedArguments
{
    /// <summary>Adds the seeds <paramref name="arguments"/> give to <paramref name="options"/>.</summary>
    /// <param name="options">What is about to start.</param>
    /// <param name="arguments">The call's arguments; the schema has checked their shape.</param>
    /// <returns>
    /// Why a <c>redis_keys</c> item is refused, naming it (<c>redis_keys[1]: ...</c>) and never showing its value, or
    /// <see langword="null"/>.
    /// </returns>
    public static string? AddTo(SurlServerOptions options, JsonElement arguments)
    {
        options.DirectoryLdif = RunningServerTool.ReadOptionalString(arguments, "directory_ldif");
        options.DnsZones =
        [
            .. ReadItems(arguments, "dns_zones").Select(
                zone => new SurlDnsZone(zone.GetProperty("apex").GetString()!, zone.GetProperty("zone_text").GetString()!)),
        ];
        return AddRedisKeys(options, arguments);
    }

    private static string? AddRedisKeys(SurlServerOptions options, JsonElement arguments)
    {
        List<SurlRedisKey> keys = [];
        foreach (var item in ReadItems(arguments, "redis_keys"))
        {
            if (RedisKeyArguments.TryRead(item, out var key) is { } refusal)
            {
                return $"redis_keys[{keys.Count}]: {refusal}";
            }

            keys.Add(key!);
        }

        options.RedisKeys = keys;
        return null;
    }

    private static IEnumerable<JsonElement> ReadItems(JsonElement arguments, string name) =>
        arguments.TryGetProperty(name, out var array) ? array.EnumerateArray() : [];
}
