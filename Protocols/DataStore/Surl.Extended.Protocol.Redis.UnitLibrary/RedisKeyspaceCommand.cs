namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Makes the <see cref="RedisCommand"/> of a command that reads or changes the keyspace: it runs
/// whole under <see cref="RedisKeyspace.SyncRoot"/>, so connections see each command as atomic,
/// and one that may grow the keyspace is refused with <c>-OOM …</c> once a bound is reached
/// (ADR-0106 decision 6), as Redis refuses a write past <c>maxmemory</c> before running it.
/// </summary>
internal static class RedisKeyspaceCommand
{
    /// <summary>A command that reads the keyspace or only shrinks it.</summary>
    /// <param name="name">The name in lower case.</param>
    /// <param name="arity">Redis's arity.</param>
    /// <param name="answer">Answers it, under the keyspace's lock.</param>
    /// <returns>The command.</returns>
    public static RedisCommand Reading(string name, int arity, Func<RedisCommandCall, RespValue> answer) =>
        new(name, arity, false, call => ValueTask.FromResult(AnswerLocked(call, answer, false)));

    /// <summary>A command that may grow the keyspace, refused while it is full.</summary>
    /// <param name="name">The name in lower case.</param>
    /// <param name="arity">Redis's arity.</param>
    /// <param name="answer">Answers it, under the keyspace's lock.</param>
    /// <returns>The command.</returns>
    public static RedisCommand Growing(string name, int arity, Func<RedisCommandCall, RespValue> answer) =>
        new(name, arity, false, call => ValueTask.FromResult(AnswerLocked(call, answer, true)));

    private static RespValue AnswerLocked(RedisCommandCall call, Func<RedisCommandCall, RespValue> answer, bool mayGrow)
    {
        lock (call.Keyspace.SyncRoot)
        {
            return mayGrow && call.Keyspace.IsFullOfLiveKeys(call.NowMilliseconds) ? RedisErrorReplies.OutOfMemory : answer(call);
        }
    }
}
