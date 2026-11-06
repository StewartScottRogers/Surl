namespace Surl.Core;

/// <summary>
/// The connection-level limits the serving engine enforces (ADR-0006, section 1): how many
/// connections it holds at once, in total and from one remote address, and how long one
/// exchange may sit idle or last.
/// </summary>
/// <remarks>
/// 0 (or <see cref="TimeSpan.Zero"/>) means no limit, as on the command line. Every value is
/// checked when it is set, by the constructor or by a <see langword="with"/> expression, and
/// an out-of-range one throws <see cref="ArgumentOutOfRangeException"/> naming the property.
/// </remarks>
public sealed record ConnectionLimits
{
    /// <summary>
    /// The longest timeout a <see cref="CancellationTokenSource"/> can count down:
    /// 4294967294 milliseconds, about 49.7 days.
    /// </summary>
    public static readonly TimeSpan MaxTimeout = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    /// <summary>
    /// ADR-0006 section 1's defaults: 1024 connections, 100 from one address, a 120-second
    /// idle timeout and a 3600-second maximum exchange duration.
    /// </summary>
    public static ConnectionLimits Default { get; } = new(
        maxConnections: 1024,
        maxConnectionsPerAddress: 100,
        TimeSpan.FromSeconds(120),
        TimeSpan.FromSeconds(3600));

    /// <summary>
    /// No limit at all: every value 0.
    /// </summary>
    public static ConnectionLimits None { get; } = new(0, 0, TimeSpan.Zero, TimeSpan.Zero);

    /// <summary>
    /// Creates a set of limits (ADR-0006, section 1).
    /// </summary>
    /// <param name="maxConnections">The value of <see cref="MaxConnections"/>.</param>
    /// <param name="maxConnectionsPerAddress">The value of <see cref="MaxConnectionsPerAddress"/>.</param>
    /// <param name="idleTimeout">The value of <see cref="IdleTimeout"/>.</param>
    /// <param name="maxExchangeDuration">The value of <see cref="MaxExchangeDuration"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A count is negative, or a duration is negative or above <see cref="MaxTimeout"/>.
    /// </exception>
    public ConnectionLimits(
        int maxConnections, int maxConnectionsPerAddress, TimeSpan idleTimeout, TimeSpan maxExchangeDuration)
    {
        MaxConnections = maxConnections;
        MaxConnectionsPerAddress = maxConnectionsPerAddress;
        IdleTimeout = idleTimeout;
        MaxExchangeDuration = maxExchangeDuration;
    }

    /// <summary>
    /// <c>--max-connections</c>: the most connections held at once, all listeners together.
    /// 0 means no limit.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int MaxConnections
    {
        get;
        init => field = RequireCount(value, nameof(MaxConnections));
    }

    /// <summary>
    /// <c>--max-connections-per-address</c>: the most connections held at once from one remote
    /// IP address, an IPv4-mapped IPv6 address counting as its IPv4 address. 0 means no limit.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int MaxConnectionsPerAddress
    {
        get;
        init => field = RequireCount(value, nameof(MaxConnectionsPerAddress));
    }

    /// <summary>
    /// <c>--idle-timeout</c>: how long an exchange may go with no byte read or written before
    /// the engine cancels it. <see cref="TimeSpan.Zero"/> means no limit.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or above <see cref="MaxTimeout"/>.</exception>
    public TimeSpan IdleTimeout
    {
        get;
        init => field = RequireTimeout(value, nameof(IdleTimeout));
    }

    /// <summary>
    /// <c>--max-time</c>: how long an exchange may last from accept before the engine cancels
    /// it. <see cref="TimeSpan.Zero"/> means no limit.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or above <see cref="MaxTimeout"/>.</exception>
    public TimeSpan MaxExchangeDuration
    {
        get;
        init => field = RequireTimeout(value, nameof(MaxExchangeDuration));
    }

    private static int RequireCount(int value, string propertyName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, propertyName);
        return value;
    }

    private static TimeSpan RequireTimeout(TimeSpan value, string propertyName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero, propertyName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxTimeout, propertyName);
        return value;
    }
}
