using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// What one <see cref="SurlEventSubscription"/> asks of a <see cref="SurlEventStream"/>
/// (ADR-0124, decision 1).
/// </summary>
public sealed record SurlEventSubscriptionOptions
{
    /// <summary>
    /// The smallest <see cref="Capacity"/> a subscription may ask for.
    /// </summary>
    public const int MinCapacity = 1;

    /// <summary>
    /// The largest <see cref="Capacity"/> a subscription may ask for.
    /// </summary>
    public const int MaxCapacity = 1_048_576;

    /// <summary>
    /// The <see cref="Capacity"/> a subscription gets when it does not choose one.
    /// </summary>
    public const int DefaultCapacity = 65_536;

    /// <summary>
    /// How many undelivered events the subscription holds, <see cref="MinCapacity"/> to
    /// <see cref="MaxCapacity"/>; past it the oldest undelivered event is dropped.
    /// </summary>
    public int Capacity { get; init; } = DefaultCapacity;

    /// <summary>
    /// Whether the subscription receives <see cref="ExchangeBytesEvent"/>s. While no subscription
    /// asks for them, the engine copies no bytes.
    /// </summary>
    public bool IncludeBytes { get; init; }

    /// <summary>
    /// Which events the subscription receives, asked at publish time; <see langword="null"/> for
    /// every event. A filter that throws is taken as refusing the event.
    /// </summary>
    public Func<SurlEvent, bool>? Filter { get; init; }
}
