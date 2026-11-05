namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// What the composition tells the NATS server beyond its exchange limits (ADR-0113 decisions 2
/// and 5).
/// </summary>
public sealed record NatsServerOptions
{
    /// <summary>
    /// How many of the server's PINGs may go unanswered: at an interval when this many are,
    /// the server sends <c>-ERR 'Stale Connection'</c> and closes instead of another PING.
    /// </summary>
    public const int MaxUnansweredPings = 2;

    /// <summary>The PING interval when the idle timeout is off: 120 seconds.</summary>
    public static readonly TimeSpan PingIntervalWithoutIdleTimeout = TimeSpan.FromSeconds(120);

    /// <summary>The options when nothing is given: an idle timeout of 120 seconds, logins required.</summary>
    public static NatsServerOptions Default { get; } = new();

    /// <summary>
    /// ADR-0006's <c>--idle-timeout</c>: 120 seconds unless given; zero or
    /// <see cref="Timeout.InfiniteTimeSpan"/> when it is off.
    /// </summary>
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Whether <c>--allow-anonymous</c> was given, so INFO says <c>auth_required: false</c>.
    /// Whether a CONNECT without credentials is served is still the authentication policy's call.
    /// </summary>
    public bool AllowsAnonymous { get; init; }

    /// <summary>
    /// How often the server sends its own PING once CONNECT is accepted: a quarter of
    /// <see cref="IdleTimeout"/>, or <see cref="PingIntervalWithoutIdleTimeout"/> when it is off.
    /// </summary>
    public TimeSpan PingInterval => IdleTimeout > TimeSpan.Zero ? IdleTimeout / 4 : PingIntervalWithoutIdleTimeout;
}
