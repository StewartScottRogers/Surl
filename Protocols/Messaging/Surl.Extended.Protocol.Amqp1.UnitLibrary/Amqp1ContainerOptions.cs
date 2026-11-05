using System.Security.Cryptography;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// What the composition tells an AMQP 1.0 connection beyond its exchange limits (ADR-0107
/// decision 3).
/// </summary>
public sealed record Amqp1ContainerOptions
{
    /// <summary>What every <c>container-id</c> starts with.</summary>
    public const string ContainerIdPrefix = "surl-";

    /// <summary>The options when nothing is given: a fresh container id and an idle timeout of 120 seconds.</summary>
    public static Amqp1ContainerOptions Default { get; } = new();

    /// <summary>
    /// The <c>container-id</c> the container's <c>open</c> carries, fixed for the server's life:
    /// <see cref="ContainerIdPrefix"/> and 32 lower-case hex digits unless given.
    /// </summary>
    public string ContainerId { get; init; } = NewContainerId();

    /// <summary>
    /// ADR-0006's <c>--idle-timeout</c>: 120 seconds unless given; zero or
    /// <see cref="Timeout.InfiniteTimeSpan"/> when it is off.
    /// </summary>
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>How long the client may send nothing before the container closes; infinite when the idle timeout is off.</summary>
    public TimeSpan SilenceLimit => IdleTimeout > TimeSpan.Zero ? IdleTimeout : Timeout.InfiniteTimeSpan;

    /// <summary>
    /// The <c>idle-time-out</c> the container's <c>open</c> advertises, in milliseconds: half of
    /// <see cref="IdleTimeout"/> (Part 2 section 2.4.5), at most <see cref="uint.MaxValue"/>;
    /// <see langword="null"/> when the idle timeout is off.
    /// </summary>
    public uint? AdvertisedIdleTimeOutMilliseconds => IdleTimeout > TimeSpan.Zero
        ? (uint)Math.Min(Math.Floor(IdleTimeout.TotalMilliseconds / 2), uint.MaxValue)
        : null;

    /// <summary>A new container id: <see cref="ContainerIdPrefix"/> and 32 lower-case hex digits from <see cref="RandomNumberGenerator"/>.</summary>
    /// <returns>The id.</returns>
    public static string NewContainerId() => ContainerIdPrefix + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
}
