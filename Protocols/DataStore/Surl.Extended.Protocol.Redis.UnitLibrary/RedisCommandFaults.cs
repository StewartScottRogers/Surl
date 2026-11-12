using System.Collections.Frozen;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Asks the exchange's fault plan, for each command about to run, whether to misbehave instead
/// (ADR-0124, decisions 4 and 9): the point names the scheme (<c>redis</c> or <c>rediss</c>), the
/// account logged in as, the command's name in upper case as <c>command</c> (<c>GET</c>,
/// <c>CLIENT|SETNAME</c>), and its first key - or a pub/sub command's first channel - as
/// <c>path</c>, which a command with neither leaves out.
/// </summary>
/// <remarks>
/// A command is asked about only once it would run: an unknown command, a wrong argument count, a
/// command before the login it needs and one a subscribed connection may not send keep their own
/// errors, so a fault never masks a refusal. A <see cref="FaultKind.Delay"/> or
/// <see cref="FaultKind.Drop"/> rule is applied as every server applies it
/// (<see cref="CommandStageFaults"/>). A <see cref="FaultKind.Error"/> rule answers the command with
/// its reply as a RESP simple error, <c>-&lt;reply&gt;</c>, in place of running it, and publishes one
/// <see cref="FaultAppliedEvent"/> naming the path, or else the command; a reply that is empty or
/// holds CR or LF cannot be a simple error, so it is noted as not applied and the command runs.
/// </remarks>
internal static class RedisCommandFaults
{
    // The commands that name no key: the connection commands and the commands over a whole database.
    private static readonly FrozenSet<string> KeylessCommands = new[]
    {
        "hello", "auth", "select", "ping", "echo", "quit", "reset",
        "client|setname", "client|getname", "client|id", "client|setinfo",
        "keys", "scan", "dbsize", "flushdb", "flushall",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Finds and applies the rule matching a command about to run.</summary>
    /// <param name="context">The exchange: its fault plan, clock, log and events.</param>
    /// <param name="connection">The connection a drop aborts.</param>
    /// <param name="command">The command found for <paramref name="arguments"/>.</param>
    /// <param name="arguments">The command's name then its arguments.</param>
    /// <param name="session">The connection's state: the account logged in as.</param>
    /// <returns>What the server does instead of, or before, running the command.</returns>
    public static async ValueTask<RedisCommandFaultOutcome> FindAndApplyAsync(
        ExchangeContext context, IConnection connection, RedisCommand command, IReadOnlyList<byte[]> arguments, RedisSession session)
    {
        var path = PathOf(command, arguments);
        var verb = command.Name.ToUpperInvariant();
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = session.UserName,
            Path = path,
            Command = verb,
        };
        var target = path ?? verb;
        var rule = await CommandStageFaults.FindAndApplyAsync(context, connection, point, target);
        return rule switch
        {
            { Kind: FaultKind.Drop } => RedisCommandFaultOutcome.Dropped,
            { Kind: FaultKind.Error } => ChosenError(context, rule, verb, target),
            _ => RedisCommandFaultOutcome.None,
        };
    }

    /// <summary>
    /// The path a fault rule matches a command by: its first key, or a pub/sub command's first
    /// channel, as the keyspace names it (Latin-1); <see langword="null"/> for a command with neither.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="arguments">The command's name then its arguments.</param>
    /// <returns>The path, or <see langword="null"/>.</returns>
    public static string? PathOf(RedisCommand command, IReadOnlyList<byte[]> arguments) =>
        arguments.Count > 1 && !KeylessCommands.Contains(command.Name) ? RedisKeys.Name(arguments[1]) : null;

    private static RedisCommandFaultOutcome ChosenError(ExchangeContext context, FaultRule rule, string verb, string target)
    {
        var reply = rule.Reply;
        if (string.IsNullOrEmpty(reply) || reply.AsSpan().IndexOfAny('\r', '\n') >= 0)
        {
            context.Log.Note($"Fault rule {rule.Id} not applied to {verb}: its reply does not fit the redis error shape.");
            return RedisCommandFaultOutcome.None;
        }

        context.Log.Note($"{verb} was answered with an error by fault rule {rule.Id}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, target));
        return new RedisCommandFaultOutcome(false, new RespSimpleError(reply));
    }
}
