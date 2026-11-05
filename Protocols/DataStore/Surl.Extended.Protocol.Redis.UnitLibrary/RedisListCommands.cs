namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The list commands (ADR-0106 decision 1, each command's reference page): <c>LPUSH</c>,
/// <c>RPUSH</c>, <c>LPOP</c>, <c>RPOP</c>, <c>LLEN</c>, <c>LRANGE</c>, <c>LINDEX</c>,
/// <c>LSET</c>, <c>LREM</c> and <c>LTRIM</c>. An index counts from 0 at the head, and a negative
/// one from -1 at the tail.
/// </summary>
internal static class RedisListCommands
{
    private static readonly RespSimpleString Ok = new("OK");
    private static readonly RespInteger Zero = new(0);

    /// <summary>The list commands.</summary>
    public static IReadOnlyList<RedisCommand> Commands { get; } =
    [
        RedisKeyspaceCommand.Growing("lpush", -3, call => Push(call, true)),
        RedisKeyspaceCommand.Growing("rpush", -3, call => Push(call, false)),
        RedisKeyspaceCommand.Reading("lpop", -2, call => Pop(call, "lpop", true)),
        RedisKeyspaceCommand.Reading("rpop", -2, call => Pop(call, "rpop", false)),
        RedisKeyspaceCommand.Reading("llen", 2, call => RedisCollections.Read<RedisListValue>(call, list => new RespInteger(list?.Count ?? 0))),
        RedisKeyspaceCommand.Reading("lrange", 4, LRange),
        RedisKeyspaceCommand.Reading("lindex", 3, LIndex),
        RedisKeyspaceCommand.Growing("lset", 4, LSet),
        RedisKeyspaceCommand.Reading("lrem", 4, LRem),
        RedisKeyspaceCommand.Reading("ltrim", 4, LTrim),
    ];

    /// <summary>
    /// The indexes <paramref name="start"/> to <paramref name="stop"/> name in a list of
    /// <paramref name="length"/>, as the <c>LRANGE</c> page reads them: negative ones from the
    /// tail, a start before the head taken as the head, a stop past the tail as the tail.
    /// </summary>
    /// <param name="start">The first index asked for.</param>
    /// <param name="stop">The last index asked for.</param>
    /// <param name="length">The list's length.</param>
    /// <param name="first">The first index within the list.</param>
    /// <param name="last">The last index within the list.</param>
    /// <returns>Whether the range holds any element.</returns>
    public static bool TryClampRange(long start, long stop, int length, out int first, out int last)
    {
        start = start < 0 ? Math.Max(start + length, 0) : start;
        stop = stop < 0 ? stop + length : Math.Min(stop, length - 1L);
        first = (int)Math.Min(start, int.MaxValue);
        last = (int)Math.Max(stop, -1);
        return start <= stop;
    }

    private static RespValue Push(RedisCommandCall call, bool atHead) =>
        RedisCollections.Change<RedisListValue>(call, null, list =>
        {
            foreach (var element in call.Arguments.Skip(2))
            {
                list.Push(element, atHead);
            }

            return new RespInteger(list.Count);
        });

    // LPOP key [count]: one element or null without a count; with one, an array, or the null
    // array on a missing key (ADR-0106 decision 2).
    private static RespValue Pop(RedisCommandCall call, string name, bool atHead)
    {
        if (call.Arguments.Count > 3)
        {
            return RedisErrorReplies.WrongNumberOfArguments(name);
        }

        if (call.Arguments.Count == 2)
        {
            return RedisCollections.Change<RedisListValue>(call, call.NullReply, list =>
                new RespBulkString(list.RemoveAt(atHead ? 0 : list.Count - 1)));
        }

        if (TryReadCount(call.Arguments[2], out var count) is { } refusal)
        {
            return refusal;
        }

        return RedisCollections.Change<RedisListValue>(call, NullArray(call), list =>
        {
            var popped = new List<byte[]>();
            while (popped.Count < count && list.Count > 0)
            {
                popped.Add(list.RemoveAt(atHead ? 0 : list.Count - 1));
            }

            return new RespArray(RedisCollections.BulkStrings(popped));
        });
    }

    /// <summary>A count argument of a pop: an integer, at least 0, or the error refusing it.</summary>
    /// <param name="argument">The argument.</param>
    /// <param name="count">The count, when read.</param>
    /// <returns>The error, or <see langword="null"/> when the count was read.</returns>
    public static RespSimpleError? TryReadCount(byte[] argument, out long count)
    {
        if (!RedisIntegers.TryParse(argument, out count))
        {
            return RedisErrorReplies.NotAnInteger;
        }

        return count < 0 ? RedisErrorReplies.NotPositive : null;
    }

    /// <summary>A missing array: <c>*-1</c> in RESP2, <c>_</c> in RESP3 (ADR-0106 decision 2).</summary>
    /// <param name="call">The command.</param>
    /// <returns>The reply.</returns>
    public static RespValue NullArray(RedisCommandCall call) =>
        call.Session.ProtocolVersion == 3 ? RespNull.Instance : RespNullArray.Instance;

    private static RespValue LRange(RedisCommandCall call)
    {
        if (!RedisIntegers.TryParse(call.Arguments[2], out var start) || !RedisIntegers.TryParse(call.Arguments[3], out var stop))
        {
            return RedisErrorReplies.NotAnInteger;
        }

        return RedisCollections.Read<RedisListValue>(call, list =>
            list is not null && TryClampRange(start, stop, list.Count, out var first, out var last)
                ? new RespArray(RedisCollections.BulkStrings(list.Range(first, last)))
                : new RespArray());
    }

    private static RespValue LIndex(RedisCommandCall call)
    {
        if (!RedisIntegers.TryParse(call.Arguments[2], out var index))
        {
            return RedisErrorReplies.NotAnInteger;
        }

        return RedisCollections.Read<RedisListValue>(call, list =>
            list is not null && TryResolveIndex(index, list.Count, out var position)
                ? new RespBulkString(list[position])
                : call.NullReply);
    }

    // LSET key index element: checked in Redis's order, the key, its type, then the index.
    private static RespValue LSet(RedisCommandCall call) =>
        RedisCollections.Change<RedisListValue>(call, RedisErrorReplies.NoSuchKey, list =>
        {
            if (!RedisIntegers.TryParse(call.Arguments[2], out var index))
            {
                return RedisErrorReplies.NotAnInteger;
            }

            if (!TryResolveIndex(index, list.Count, out var position))
            {
                return RedisErrorReplies.IndexOutOfRange;
            }

            list.Replace(position, call.Arguments[3]);
            return Ok;
        });

    // LREM key count element: from the head when count > 0, from the tail when < 0, all when 0.
    private static RespValue LRem(RedisCommandCall call)
    {
        if (!RedisIntegers.TryParse(call.Arguments[2], out var count))
        {
            return RedisErrorReplies.NotAnInteger;
        }

        return RedisCollections.Change<RedisListValue>(call, Zero, list => new RespInteger(list.RemoveMatching(call.Arguments[3], count)));
    }

    private static RespValue LTrim(RedisCommandCall call)
    {
        if (!RedisIntegers.TryParse(call.Arguments[2], out var start) || !RedisIntegers.TryParse(call.Arguments[3], out var stop))
        {
            return RedisErrorReplies.NotAnInteger;
        }

        return RedisCollections.Change<RedisListValue>(call, Ok, list =>
        {
            if (TryClampRange(start, stop, list.Count, out var first, out var last))
            {
                list.Keep(first, last);
            }
            else
            {
                list.Clear();
            }

            return Ok;
        });
    }

    private static bool TryResolveIndex(long index, int length, out int position)
    {
        var resolved = index < 0 ? index + length : index;
        position = (int)Math.Clamp(resolved, 0, Math.Max(length - 1, 0));
        return resolved >= 0 && resolved < length;
    }
}
