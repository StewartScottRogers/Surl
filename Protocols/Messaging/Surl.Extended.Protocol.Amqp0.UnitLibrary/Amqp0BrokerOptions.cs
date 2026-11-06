namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// What the composition tells the AMQP 0-9-1 broker beyond its exchange limits (ADR-0112
/// decisions 2 and 3).
/// </summary>
public sealed record Amqp0BrokerOptions
{
    /// <summary>The largest heartbeat interval <c>connection.tune</c> can carry, in seconds.</summary>
    public const ushort LargestHeartbeatSeconds = ushort.MaxValue;

    /// <summary>The options when nothing is given: an idle timeout of 120 seconds, no <c>ANONYMOUS</c>.</summary>
    public static Amqp0BrokerOptions Default { get; } = new();

    /// <summary>
    /// ADR-0006's <c>--idle-timeout</c>: 120 seconds unless given; zero or
    /// <see cref="Timeout.InfiniteTimeSpan"/> when it is off.
    /// </summary>
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>Whether <c>ANONYMOUS</c> is offered last, as <c>--allow-anonymous</c> asks.</summary>
    public bool OffersAnonymous { get; init; }

    /// <summary>
    /// The heartbeat <c>connection.tune</c> offers: half of <see cref="IdleTimeout"/> in whole
    /// seconds, at most <see cref="LargestHeartbeatSeconds"/>; 0 when the idle timeout is off.
    /// </summary>
    public ushort OfferedHeartbeatSeconds => IdleTimeout <= TimeSpan.Zero
        ? (ushort)0
        : (ushort)Math.Min(Math.Floor(IdleTimeout.TotalSeconds / 2), LargestHeartbeatSeconds);
}
