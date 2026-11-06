namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The set commands (ADR-0106 decision 1, each command's reference page): <c>SADD</c>,
/// <c>SREM</c>, <c>SMEMBERS</c>, <c>SISMEMBER</c>, <c>SMISMEMBER</c>, <c>SCARD</c>, <c>SPOP</c>,
/// <c>SINTER</c>, <c>SUNION</c> and <c>SDIFF</c>. A set of members is a set in RESP3 and an array
/// in RESP2, its members in the order they were added.
/// </summary>
internal static class RedisSetCommands
{
    private static readonly RespInteger Zero = new(0);
    private static readonly RespInteger One = new(1);

    /// <summary>The set commands.</summary>
    public static IReadOnlyList<RedisCommand> Commands { get; } =
    [
        RedisKeyspaceCommand.Growing("sadd", -3, call => RedisCollections.Change<RedisSetValue>(call, null, set => new RespInteger(call.Arguments.Skip(2).Count(set.Add)))),
        RedisKeyspaceCommand.Reading("srem", -3, call => RedisCollections.Change<RedisSetValue>(call, Zero, set => new RespInteger(call.Arguments.Skip(2).Count(set.Remove)))),
        RedisKeyspaceCommand.Reading("smembers", 2, call => RedisCollections.Read<RedisSetValue>(call, set => Members(call, set?.Members ?? []))),
        RedisKeyspaceCommand.Reading("sismember", 3, call => RedisCollections.Read<RedisSetValue>(call, set => IsMember(set, call.Arguments[2]))),
        RedisKeyspaceCommand.Reading("smismember", -3, call => RedisCollections.Read<RedisSetValue>(call, set => new RespArray([.. call.Arguments.Skip(2).Select(member => IsMember(set, member))]))),
        RedisKeyspaceCommand.Reading("scard", 2, call => RedisCollections.Read<RedisSetValue>(call, set => new RespInteger(set?.Count ?? 0))),
        RedisKeyspaceCommand.Reading("spop", -2, SPop),
        RedisKeyspaceCommand.Reading("sinter", -2, call => Combine(call, Intersect)),
        RedisKeyspaceCommand.Reading("sunion", -2, call => Combine(call, Union)),
        RedisKeyspaceCommand.Reading("sdiff", -2, call => Combine(call, Difference)),
    ];

    private static RespInteger IsMember(RedisSetValue? set, byte[] member) => set?.Contains(member) == true ? One : Zero;

    /// <summary>Members as a set in RESP3 and an array in RESP2 (ADR-0106 decision 2).</summary>
    private static RespValue Members(RedisCommandCall call, IEnumerable<byte[]> members)
    {
        var elements = RedisCollections.BulkStrings(members);
        return call.Session.ProtocolVersion == 3 ? new RespSet(elements) : new RespArray(elements);
    }

    // SPOP key [count]: one member or null without a count; with one, a set of members, or the
    // null array on a missing key (ADR-0106 decision 2).
    private static RespValue SPop(RedisCommandCall call)
    {
        if (call.Arguments.Count > 3)
        {
            return RedisErrorReplies.SyntaxError;
        }

        if (call.Arguments.Count == 2)
        {
            return RedisCollections.Change<RedisSetValue>(call, call.NullReply, set => new RespBulkString(set.RemoveFirst()));
        }

        if (RedisListCommands.TryReadCount(call.Arguments[2], out var count) is { } refusal)
        {
            return refusal;
        }

        return RedisCollections.Change<RedisSetValue>(call, RedisListCommands.NullArray(call), set =>
        {
            var popped = new List<byte[]>();
            while (popped.Count < count && set.Count > 0)
            {
                popped.Add(set.RemoveFirst());
            }

            return Members(call, popped);
        });
    }

    // Every key named is read first, so one of another type is -WRONGTYPE; a missing one is empty.
    private static RespValue Combine(RedisCommandCall call, Func<IReadOnlyList<RedisSetValue?>, IEnumerable<byte[]>> combine)
    {
        var sets = new List<RedisSetValue?>();
        foreach (var key in call.Arguments.Skip(1))
        {
            sets.Add(RedisCollections.Find<RedisSetValue>(call, key, out var isWrongType));
            if (isWrongType)
            {
                return RedisErrorReplies.WrongType;
            }
        }

        return Members(call, combine(sets));
    }

    private static IEnumerable<byte[]> Intersect(IReadOnlyList<RedisSetValue?> sets) =>
        sets.Any(set => set is null) ? [] : sets[0]!.Members.Where(member => sets.Skip(1).All(set => set!.Contains(member)));

    private static IEnumerable<byte[]> Union(IReadOnlyList<RedisSetValue?> sets)
    {
        var union = new RedisSetValue();
        foreach (var member in sets.SelectMany(set => set?.Members ?? []))
        {
            union.Add(member);
        }

        return union.Members;
    }

    private static IEnumerable<byte[]> Difference(IReadOnlyList<RedisSetValue?> sets) =>
        (sets[0]?.Members ?? []).Where(member => !sets.Skip(1).Any(set => set?.Contains(member) == true));
}
