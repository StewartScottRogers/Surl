using System.Collections.Frozen;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The pub/sub commands (ADR-0106 decision 1, the "Pub/Sub" page and each command's page):
/// <c>SUBSCRIBE</c>, <c>UNSUBSCRIBE</c>, <c>PSUBSCRIBE</c>, <c>PUNSUBSCRIBE</c> and
/// <c>PUBLISH</c>, and what a subscribed RESP2 connection may run.
/// </summary>
internal static class RedisPubSubCommands
{
    /// <summary>The pub/sub commands.</summary>
    public static IReadOnlyList<RedisCommand> Commands { get; } =
    [
        new("subscribe", -2, false, call => ValueTask.FromResult(call.PubSub.Subscribe(call.Subscriber, Names(call), false))),
        new("unsubscribe", -1, false, call => ValueTask.FromResult(call.PubSub.Unsubscribe(call.Subscriber, Names(call), false))),
        new("psubscribe", -2, false, call => ValueTask.FromResult(call.PubSub.Subscribe(call.Subscriber, Names(call), true))),
        new("punsubscribe", -1, false, call => ValueTask.FromResult(call.PubSub.Unsubscribe(call.Subscriber, Names(call), true))),
        new("publish", 3, false, call => ValueTask.FromResult<RespValue>(new RespInteger(call.PubSub.Publish(call.Arguments[1], call.Arguments[2])))),
    ];

    /// <summary>The commands a subscribed RESP2 connection may run (the "Pub/Sub" page).</summary>
    public static FrozenSet<string> AllowedWhileSubscribed { get; } =
        new[] { "subscribe", "unsubscribe", "psubscribe", "punsubscribe", "ping", "quit", "reset" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The refusal of <paramref name="command"/> on a connection subscribed in RESP2, or
    /// <see langword="null"/> when it may run: in RESP3 every command may (ADR-0106 decision 1).
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="subscriber">The connection.</param>
    /// <returns>The refusal, or <see langword="null"/>.</returns>
    public static RespSimpleError? RefusalWhileSubscribed(RedisCommand command, RedisSubscriber subscriber) =>
        subscriber.IsSubscribed && subscriber.Session.ProtocolVersion == 2 && !AllowedWhileSubscribed.Contains(command.Name)
            ? RedisErrorReplies.NotAllowedWhileSubscribed(command.Name)
            : null;

    private static byte[][] Names(RedisCommandCall call) => [.. call.Arguments.Skip(1)];
}
