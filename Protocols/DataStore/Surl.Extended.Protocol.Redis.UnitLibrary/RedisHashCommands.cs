using System.Globalization;
using System.Text;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The hash commands (ADR-0106 decision 1, each command's reference page): <c>HSET</c>,
/// <c>HGET</c>, <c>HMGET</c>, <c>HDEL</c>, <c>HEXISTS</c>, <c>HLEN</c>, <c>HKEYS</c>,
/// <c>HVALS</c>, <c>HGETALL</c>, <c>HINCRBY</c> and <c>HSETNX</c>. Fields are answered in the
/// order they were added.
/// </summary>
internal static class RedisHashCommands
{
    private static readonly RespInteger Zero = new(0);

    /// <summary>The hash commands.</summary>
    public static IReadOnlyList<RedisCommand> Commands { get; } =
    [
        RedisKeyspaceCommand.Growing("hset", -4, HSet),
        RedisKeyspaceCommand.Reading("hget", 3, HGet),
        RedisKeyspaceCommand.Reading("hmget", -3, HMGet),
        RedisKeyspaceCommand.Reading("hdel", -3, HDel),
        RedisKeyspaceCommand.Reading("hexists", 3, call => RedisCollections.Read<RedisHashValue>(call, hash => new RespInteger(hash?.Get(FieldName(call)) is null ? 0 : 1))),
        RedisKeyspaceCommand.Reading("hlen", 2, call => RedisCollections.Read<RedisHashValue>(call, hash => new RespInteger(hash?.Count ?? 0))),
        RedisKeyspaceCommand.Reading("hkeys", 2, call => RedisCollections.Read<RedisHashValue>(call, hash => new RespArray(RedisCollections.BulkStrings(FieldsOf(hash).Select(field => RedisKeys.Bytes(field.Key)))))),
        RedisKeyspaceCommand.Reading("hvals", 2, call => RedisCollections.Read<RedisHashValue>(call, hash => new RespArray(RedisCollections.BulkStrings(FieldsOf(hash).Select(field => field.Value))))),
        RedisKeyspaceCommand.Reading("hgetall", 2, HGetAll),
        RedisKeyspaceCommand.Growing("hincrby", 4, HIncrBy),
        RedisKeyspaceCommand.Growing("hsetnx", 4, HSetNx),
    ];

    private static string FieldName(RedisCommandCall call) => RedisKeys.Name(call.Arguments[2]);

    private static IEnumerable<KeyValuePair<string, byte[]>> FieldsOf(RedisHashValue? hash) => hash?.Fields ?? [];

    // HSET key field value [field value ...]: the number of fields added.
    private static RespValue HSet(RedisCommandCall call)
    {
        if (call.Arguments.Count % 2 != 0)
        {
            return RedisErrorReplies.WrongNumberOfArguments("hset");
        }

        return RedisCollections.Change<RedisHashValue>(call, null, hash =>
        {
            var added = 0;
            for (var index = 2; index < call.Arguments.Count; index += 2)
            {
                added += hash.Set(RedisKeys.Name(call.Arguments[index]), call.Arguments[index + 1]) ? 1 : 0;
            }

            return new RespInteger(added);
        });
    }

    private static RespValue HGet(RedisCommandCall call) =>
        RedisCollections.Read<RedisHashValue>(call, hash => BulkOrNull(call, hash?.Get(FieldName(call))));

    private static RespValue HMGet(RedisCommandCall call) =>
        RedisCollections.Read<RedisHashValue>(call, hash => new RespArray(
            [.. call.Arguments.Skip(2).Select(field => BulkOrNull(call, hash?.Get(RedisKeys.Name(field))))]));

    private static RespValue HDel(RedisCommandCall call) =>
        RedisCollections.Change<RedisHashValue>(call, Zero, hash =>
            new RespInteger(call.Arguments.Skip(2).Count(field => hash.Remove(RedisKeys.Name(field)))));

    // A map in RESP3, a flat field-then-value array in RESP2 (the HGETALL page).
    private static RespValue HGetAll(RedisCommandCall call) =>
        RedisCollections.Read<RedisHashValue>(call, hash =>
        {
            var fields = FieldsOf(hash).Select(field =>
                new KeyValuePair<RespValue, RespValue>(new RespBulkString(RedisKeys.Bytes(field.Key)), new RespBulkString(field.Value))).ToArray();
            return call.Session.ProtocolVersion == 3
                ? new RespMap(fields)
                : new RespArray([.. fields.SelectMany(field => new[] { field.Key, field.Value })]);
        });

    // HINCRBY key field increment: an absent field counts from 0.
    private static RespValue HIncrBy(RedisCommandCall call)
    {
        if (!RedisIntegers.TryParse(call.Arguments[3], out var amount))
        {
            return RedisErrorReplies.NotAnInteger;
        }

        return RedisCollections.Change<RedisHashValue>(call, null, hash =>
        {
            var current = 0L;
            if (hash.Get(FieldName(call)) is { } value && !RedisIntegers.TryParse(value, out current))
            {
                return RedisErrorReplies.HashValueNotAnInteger;
            }

            if (amount > 0 ? current > long.MaxValue - amount : current < long.MinValue - amount)
            {
                return RedisErrorReplies.IncrementOverflow;
            }

            var result = current + amount;
            hash.Set(FieldName(call), Encoding.ASCII.GetBytes(result.ToString(CultureInfo.InvariantCulture)));
            return new RespInteger(result);
        });
    }

    private static RespValue HSetNx(RedisCommandCall call) =>
        RedisCollections.Change<RedisHashValue>(call, null, hash =>
        {
            if (hash.Get(FieldName(call)) is not null)
            {
                return Zero;
            }

            hash.Set(FieldName(call), call.Arguments[3]);
            return new RespInteger(1);
        });

    private static RespValue BulkOrNull(RedisCommandCall call, byte[]? value) =>
        value is null ? call.NullReply : new RespBulkString(value);
}
