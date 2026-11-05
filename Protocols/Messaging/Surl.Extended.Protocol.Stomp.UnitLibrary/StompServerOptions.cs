namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// What the composition tells the STOMP broker beyond its exchange limits (ADR-0118 decision 7).
/// </summary>
public sealed record StompServerOptions
{
    /// <summary>How often the server says it can send a heart-beat: every 1000 milliseconds.</summary>
    public const long HeartBeatCanSendEveryMilliseconds = 1000;

    /// <summary>The heart-beat the server wants when the idle timeout is off: 30 seconds.</summary>
    public static readonly TimeSpan HeartBeatWantedWithoutIdleTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The options when nothing is given: an idle timeout of 120 seconds.</summary>
    public static StompServerOptions Default { get; } = new();

    /// <summary>
    /// ADR-0006's <c>--idle-timeout</c>: 120 seconds unless given; zero or
    /// <see cref="Timeout.InfiniteTimeSpan"/> when it is off.
    /// </summary>
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// How often the server wants something from a heart-beating client, the second number of
    /// CONNECTED's <c>heart-beat</c>: a quarter of <see cref="IdleTimeout"/>, or
    /// <see cref="HeartBeatWantedWithoutIdleTimeout"/> when it is off.
    /// </summary>
    public TimeSpan HeartBeatWanted => IdleTimeout > TimeSpan.Zero ? IdleTimeout / 4 : HeartBeatWantedWithoutIdleTimeout;

    /// <summary>The server's offer, CONNECTED's <c>heart-beat</c> header in 1.1 and 1.2.</summary>
    internal StompHeartBeat HeartBeatOffer =>
        new(HeartBeatCanSendEveryMilliseconds, (long)HeartBeatWanted.TotalMilliseconds);
}
