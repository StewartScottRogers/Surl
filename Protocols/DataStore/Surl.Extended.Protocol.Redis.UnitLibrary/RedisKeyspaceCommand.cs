using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Makes the <see cref="RedisCommand"/> of a command that reads or changes the keyspace: it runs
/// whole under <see cref="RedisKeyspace.SyncRoot"/>, so connections see each command as atomic,
/// and one that may grow the keyspace is refused with <c>-OOM …</c> once a bound is reached
/// (ADR-0106 decision 6), as Redis refuses a write past <c>maxmemory</c> before running it, or
/// when an <see cref="FaultKind.OutOfSpace"/> rule matches one of its keys (ADR-0158 decision 7).
/// Once a command has answered, each key it wrote or deleted is published as a
/// <see cref="StoreChangedEvent"/> (ADR-0158 decision 2).
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

    /// <summary>A command that may grow the keyspace at its first key, refused while it is full.</summary>
    /// <param name="name">The name in lower case.</param>
    /// <param name="arity">Redis's arity.</param>
    /// <param name="answer">Answers it, under the keyspace's lock.</param>
    /// <returns>The command.</returns>
    public static RedisCommand Growing(string name, int arity, Func<RedisCommandCall, RespValue> answer) =>
        GrowingAt(name, arity, answer, call => [call.Arguments[1]]);

    /// <summary>A command that may grow the keyspace at each key <paramref name="keysOf"/> names, refused while it is full.</summary>
    /// <param name="name">The name in lower case.</param>
    /// <param name="arity">Redis's arity.</param>
    /// <param name="answer">Answers it, under the keyspace's lock.</param>
    /// <param name="keysOf">The keys the command may write.</param>
    /// <returns>The command.</returns>
    public static RedisCommand GrowingAt(string name, int arity, Func<RedisCommandCall, RespValue> answer, Func<RedisCommandCall, IEnumerable<byte[]>> keysOf) =>
        new(name, arity, false, async call =>
            await RunsOutOfSpaceAsync(call, keysOf(call)) ? RedisErrorReplies.OutOfMemory : AnswerLocked(call, answer, true));

    private static RespValue AnswerLocked(RedisCommandCall call, Func<RedisCommandCall, RespValue> answer, bool mayGrow)
    {
        lock (call.Keyspace.SyncRoot)
        {
            var reply = mayGrow && call.Keyspace.IsFullOfLiveKeys(call.NowMilliseconds) ? RedisErrorReplies.OutOfMemory : answer(call);
            foreach (var change in call.Keyspace.Journal.Drain())
            {
                call.Context.Events.Publish(new StoreChangedEvent(StoreKind.Keyspace, change.Change, change.Name, change.ByteCount, call.Context.ExchangeId));
            }

            return reply;
        }
    }

    // Asks the fault plan before the write, naming each key as a keyspace event does; one
    // out-of-space rule refuses the whole command with nothing written.
    private static async ValueTask<bool> RunsOutOfSpaceAsync(RedisCommandCall call, IEnumerable<byte[]> keys)
    {
        var context = call.Context;
        foreach (var key in keys)
        {
            var path = RedisKeyspaceChange.NameOf(call.Session.DatabaseIndex, RedisKeys.Name(key));
            var point = new FaultPoint(FaultStage.StoreWrite)
            {
                ExchangeId = context.ExchangeId,
                ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
                Scheme = context.Scheme,
                Account = call.Session.UserName,
                Path = path,
            };
            if (await context.Faults.FindAsync(point, context.CancellationToken) is not { Kind: FaultKind.OutOfSpace } rule)
            {
                continue;
            }

            context.Log.Note($"Write to {path} was refused as out of space by fault rule {rule.Id}.");
            context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.OutOfSpace, FaultStage.StoreWrite, context.ExchangeId, path));
            return true;
        }

        return false;
    }
}
