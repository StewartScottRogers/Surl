using System.Globalization;
using System.Text;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The string commands (ADR-0106 decision 1, each command's reference page): <c>GET</c>,
/// <c>SET</c>, <c>GETDEL</c>, <c>MGET</c>, <c>MSET</c>, <c>INCR</c>, <c>DECR</c>, <c>INCRBY</c>,
/// <c>DECRBY</c>, <c>APPEND</c> and <c>STRLEN</c>. A key holding another type is
/// <c>-WRONGTYPE …</c>, but to <c>MGET</c>, which answers a null for it.
/// </summary>
internal static class RedisStringCommands
{
    private static readonly RespSimpleString Ok = new("OK");

    /// <summary>The string commands.</summary>
    public static IReadOnlyList<RedisCommand> Commands { get; } =
    [
        RedisKeyspaceCommand.Reading("get", 2, Get),
        RedisKeyspaceCommand.Growing("set", -3, Set),
        RedisKeyspaceCommand.Reading("getdel", 2, GetDel),
        RedisKeyspaceCommand.Reading("mget", -2, MGet),
        RedisKeyspaceCommand.Growing("mset", -3, MSet),
        RedisKeyspaceCommand.Growing("incr", 2, call => IncrementBy(call, 1)),
        RedisKeyspaceCommand.Growing("decr", 2, call => IncrementBy(call, -1)),
        RedisKeyspaceCommand.Growing("incrby", 3, call => IncrementByArgument(call, false)),
        RedisKeyspaceCommand.Growing("decrby", 3, call => IncrementByArgument(call, true)),
        RedisKeyspaceCommand.Growing("append", 3, Append),
        RedisKeyspaceCommand.Reading("strlen", 2, StrLen),
    ];

    /// <summary>
    /// The string at <paramref name="key"/>: <see langword="null"/> when absent, and
    /// <paramref name="isWrongType"/> set when the key holds another type.
    /// </summary>
    /// <param name="call">The command.</param>
    /// <param name="key">The key's bytes.</param>
    /// <param name="isWrongType">Whether the key holds another type.</param>
    /// <returns>The string, or <see langword="null"/>.</returns>
    public static RedisStringValue? FindString(RedisCommandCall call, byte[] key, out bool isWrongType)
    {
        var value = call.Database.Find(RedisKeys.Name(key), call.NowMilliseconds)?.Value;
        isWrongType = value is not null and not RedisStringValue;
        return value as RedisStringValue;
    }

    private static RespValue Get(RedisCommandCall call)
    {
        var value = FindString(call, call.Arguments[1], out var isWrongType);
        return isWrongType ? RedisErrorReplies.WrongType : BulkOrNull(call, value);
    }

    // SET key value [NX | XX] [GET] [EX s | PX ms | EXAT s | PXAT ms | KEEPTTL]
    private static RespValue Set(RedisCommandCall call)
    {
        if (!RedisSetOptions.TryRead(call.Arguments, out var options))
        {
            return RedisErrorReplies.SyntaxError;
        }

        if (TryReadSetExpiry(call, options, out var expiresAt) is { } refusal)
        {
            return refusal;
        }

        var old = FindString(call, call.Arguments[1], out var isWrongType);
        if (options.AnswersOldValue && isWrongType)
        {
            return RedisErrorReplies.WrongType;
        }

        if (!options.AllowsSetting(isPresent: old is not null || isWrongType))
        {
            return options.AnswersOldValue ? BulkOrNull(call, old) : call.NullReply;
        }

        var entry = call.Database.Put(RedisKeys.Name(call.Arguments[1]), new RedisStringValue(call.Arguments[2]), call.NowMilliseconds);
        entry.ExpiresAtMilliseconds = options.KeepsExpiry ? entry.ExpiresAtMilliseconds : expiresAt;
        return options.AnswersOldValue ? BulkOrNull(call, old) : Ok;
    }

    // The expiry SET's option names, in Unix milliseconds, or the error refusing its time.
    private static RespSimpleError? TryReadSetExpiry(RedisCommandCall call, RedisSetOptions options, out long? expiresAt)
    {
        expiresAt = null;
        if (options.Expiry is not { } expiry)
        {
            return null;
        }

        if (!RedisIntegers.TryParse(options.ExpiryTime!, out var amount))
        {
            return RedisErrorReplies.NotAnInteger;
        }

        var baseMilliseconds = expiry.IsRelative ? call.NowMilliseconds : 0;
        if (amount <= 0 || !RedisExpiryTimes.TryAbsolute(amount, expiry.IsSeconds, baseMilliseconds, out var when))
        {
            return RedisErrorReplies.InvalidExpireTime("set");
        }

        expiresAt = when;
        return null;
    }

    private static RespValue GetDel(RedisCommandCall call)
    {
        var value = FindString(call, call.Arguments[1], out var isWrongType);
        if (isWrongType)
        {
            return RedisErrorReplies.WrongType;
        }

        call.Database.Delete(RedisKeys.Name(call.Arguments[1]), call.NowMilliseconds);
        return BulkOrNull(call, value);
    }

    private static RespValue MGet(RedisCommandCall call) =>
        new RespArray(call.Arguments.Skip(1).Select(key => BulkOrNull(call, FindString(call, key, out _))).ToArray());

    private static RespValue MSet(RedisCommandCall call)
    {
        if (call.Arguments.Count % 2 == 0)
        {
            return RedisErrorReplies.WrongNumberOfArguments("mset");
        }

        for (var index = 1; index < call.Arguments.Count; index += 2)
        {
            var entry = call.Database.Put(RedisKeys.Name(call.Arguments[index]), new RedisStringValue(call.Arguments[index + 1]), call.NowMilliseconds);
            entry.ExpiresAtMilliseconds = null;
        }

        return Ok;
    }

    private static RespValue IncrementByArgument(RedisCommandCall call, bool isDecrement)
    {
        if (!RedisIntegers.TryParse(call.Arguments[2], out var amount))
        {
            return RedisErrorReplies.NotAnInteger;
        }

        // DECRBY of the least integer cannot be negated, so it would overflow.
        return isDecrement && amount == long.MinValue
            ? RedisErrorReplies.IncrementOverflow
            : IncrementBy(call, isDecrement ? -amount : amount);
    }

    // The value is read as an integer as Redis reads arguments; an absent key counts from 0,
    // and the key keeps its expiry.
    private static RespValue IncrementBy(RedisCommandCall call, long amount)
    {
        var value = FindString(call, call.Arguments[1], out var isWrongType);
        if (isWrongType)
        {
            return RedisErrorReplies.WrongType;
        }

        var current = 0L;
        if (value is not null && !RedisIntegers.TryParse(value.Bytes, out current))
        {
            return RedisErrorReplies.NotAnInteger;
        }

        if (amount > 0 ? current > long.MaxValue - amount : current < long.MinValue - amount)
        {
            return RedisErrorReplies.IncrementOverflow;
        }

        var result = current + amount;
        var text = Encoding.ASCII.GetBytes(result.ToString(CultureInfo.InvariantCulture));
        call.Database.Put(RedisKeys.Name(call.Arguments[1]), new RedisStringValue(text), call.NowMilliseconds);
        return new RespInteger(result);
    }

    private static RespValue Append(RedisCommandCall call)
    {
        var value = FindString(call, call.Arguments[1], out var isWrongType);
        if (isWrongType)
        {
            return RedisErrorReplies.WrongType;
        }

        byte[] appended = [.. value?.Bytes ?? [], .. call.Arguments[2]];
        call.Database.Put(RedisKeys.Name(call.Arguments[1]), new RedisStringValue(appended), call.NowMilliseconds);
        return new RespInteger(appended.Length);
    }

    private static RespValue StrLen(RedisCommandCall call)
    {
        var value = FindString(call, call.Arguments[1], out var isWrongType);
        return isWrongType ? RedisErrorReplies.WrongType : new RespInteger(value?.Bytes.Length ?? 0);
    }

    private static RespValue BulkOrNull(RedisCommandCall call, RedisStringValue? value) =>
        value is null ? call.NullReply : new RespBulkString(value.Bytes);
}
