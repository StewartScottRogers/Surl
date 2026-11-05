using System.Globalization;
using System.Text;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The key commands (ADR-0106 decision 1, each command's reference page): <c>DEL</c>,
/// <c>UNLINK</c>, <c>EXISTS</c>, <c>EXPIRE</c>, <c>PEXPIRE</c>, <c>TTL</c>, <c>PTTL</c>,
/// <c>PERSIST</c>, <c>TYPE</c>, <c>RENAME</c>, <c>KEYS</c>, <c>SCAN</c>, <c>DBSIZE</c>,
/// <c>FLUSHDB</c> and <c>FLUSHALL</c>.
/// </summary>
internal static class RedisKeyCommands
{
    private static readonly RespSimpleString Ok = new("OK");
    private static readonly RespInteger Zero = new(0);
    private static readonly RespInteger One = new(1);

    /// <summary>The key commands.</summary>
    public static IReadOnlyList<RedisCommand> Commands { get; } =
    [
        RedisKeyspaceCommand.Reading("del", -2, Delete),
        RedisKeyspaceCommand.Reading("unlink", -2, Delete),
        RedisKeyspaceCommand.Reading("exists", -2, Exists),
        RedisKeyspaceCommand.Reading("expire", -3, call => Expire(call, "expire", true)),
        RedisKeyspaceCommand.Reading("pexpire", -3, call => Expire(call, "pexpire", false)),
        RedisKeyspaceCommand.Reading("ttl", 2, call => TimeToLive(call, true)),
        RedisKeyspaceCommand.Reading("pttl", 2, call => TimeToLive(call, false)),
        RedisKeyspaceCommand.Reading("persist", 2, Persist),
        RedisKeyspaceCommand.Reading("type", 2, TypeOf),
        RedisKeyspaceCommand.Reading("rename", 3, Rename),
        RedisKeyspaceCommand.Reading("keys", 2, Keys),
        RedisKeyspaceCommand.Reading("scan", -2, Scan),
        RedisKeyspaceCommand.Reading("dbsize", 1, call => new RespInteger(call.Database.CountKeys(call.NowMilliseconds))),
        RedisKeyspaceCommand.Reading("flushdb", -1, call => Flush(call, call.Database.Clear)),
        RedisKeyspaceCommand.Reading("flushall", -1, call => Flush(call, call.Keyspace.Clear)),
    ];

    private static RedisKeyEntry? Find(RedisCommandCall call, byte[] key) =>
        call.Database.Find(RedisKeys.Name(key), call.NowMilliseconds);

    private static RespValue Delete(RedisCommandCall call) =>
        new RespInteger(call.Arguments.Skip(1).Count(key => call.Database.Delete(RedisKeys.Name(key), call.NowMilliseconds)));

    // A key named twice is counted twice (the EXISTS page).
    private static RespValue Exists(RedisCommandCall call) =>
        new RespInteger(call.Arguments.Skip(1).Count(key => Find(call, key) is not null));

    // EXPIRE key seconds [NX | XX | GT | LT], read in Redis's order: the options, the time, the key.
    private static RespValue Expire(RedisCommandCall call, string name, bool isSeconds)
    {
        if (!RedisExpireConditions.TryRead(call.Arguments, out var condition))
        {
            return RedisErrorReplies.SyntaxError;
        }

        if (!RedisIntegers.TryParse(call.Arguments[2], out var amount))
        {
            return RedisErrorReplies.NotAnInteger;
        }

        if (!RedisExpiryTimes.TryAbsolute(amount, isSeconds, call.NowMilliseconds, out var expiresAt))
        {
            return RedisErrorReplies.InvalidExpireTime(name);
        }

        var entry = Find(call, call.Arguments[1]);
        if (entry is null || !RedisExpireConditions.Allows(condition, entry.ExpiresAtMilliseconds, expiresAt))
        {
            return Zero;
        }

        // A time already come deletes the key (the EXPIRE page); the entry then reads as expired.
        entry.ExpiresAtMilliseconds = expiresAt;
        call.Database.Find(entry.Key, call.NowMilliseconds);
        return One;
    }

    // -2 for an absent key, -1 for one without expiry; TTL rounds to the nearest second.
    private static RespValue TimeToLive(RedisCommandCall call, bool inSeconds)
    {
        var entry = Find(call, call.Arguments[1]);
        if (entry?.ExpiresAtMilliseconds is not { } expiresAt)
        {
            return new RespInteger(entry is null ? -2 : -1);
        }

        var remaining = expiresAt - call.NowMilliseconds;
        return new RespInteger(inSeconds ? (remaining + 500) / 1000 : remaining);
    }

    private static RespValue Persist(RedisCommandCall call)
    {
        var entry = Find(call, call.Arguments[1]);
        if (entry?.ExpiresAtMilliseconds is null)
        {
            return Zero;
        }

        entry.ExpiresAtMilliseconds = null;
        return One;
    }

    private static RespValue TypeOf(RedisCommandCall call) =>
        new RespSimpleString(Find(call, call.Arguments[1])?.Value.TypeName ?? "none");

    // The value and its expiry move to the new name, replacing any key there.
    private static RespValue Rename(RedisCommandCall call)
    {
        var source = Find(call, call.Arguments[1]);
        if (source is null)
        {
            return RedisErrorReplies.NoSuchKey;
        }

        var target = RedisKeys.Name(call.Arguments[2]);
        if (target == source.Key)
        {
            return Ok;
        }

        call.Database.Delete(target, call.NowMilliseconds);
        call.Database.Delete(source.Key, call.NowMilliseconds);
        call.Database.Put(target, source.Value, call.NowMilliseconds).ExpiresAtMilliseconds = source.ExpiresAtMilliseconds;
        return Ok;
    }

    private static RespValue Keys(RedisCommandCall call)
    {
        var pattern = RedisKeys.Name(call.Arguments[1]);
        return KeyArray(call.Database.LiveEntries(call.NowMilliseconds).Where(entry => RedisGlobPattern.IsMatch(pattern, entry.Key)));
    }

    // SCAN cursor [MATCH pattern] [COUNT count] [TYPE type]: MATCH and TYPE filter after the
    // count is taken, as the SCAN page allows.
    private static RespValue Scan(RedisCommandCall call)
    {
        if (!ulong.TryParse(call.Arguments[1], NumberStyles.None, CultureInfo.InvariantCulture, out var cursor))
        {
            return RedisErrorReplies.InvalidCursor;
        }

        var options = RedisScanOptions.Read(call.Arguments);
        if (options.Refusal is { } refusal)
        {
            return refusal;
        }

        var (entries, nextCursor) = call.Database.Scan(cursor, options.Count, call.NowMilliseconds);
        return new RespArray(
            new RespBulkString(Encoding.ASCII.GetBytes(nextCursor.ToString(CultureInfo.InvariantCulture))),
            KeyArray(entries.Where(options.Matches)));
    }

    // FLUSHDB [ASYNC | SYNC], FLUSHALL [ASYNC | SYNC]: both modes flush at once.
    private static RespValue Flush(RedisCommandCall call, Action clear)
    {
        var arguments = call.Arguments;
        if (arguments.Count > 2 || (arguments.Count == 2 && RedisCommandTable.UpperCaseName(arguments[1]) is not ("SYNC" or "ASYNC")))
        {
            return RedisErrorReplies.SyntaxError;
        }

        clear();
        return Ok;
    }

    private static RespArray KeyArray(IEnumerable<RedisKeyEntry> entries) =>
        new([.. entries.Select(entry => new RespBulkString(RedisKeys.Bytes(entry.Key)))]);
}
