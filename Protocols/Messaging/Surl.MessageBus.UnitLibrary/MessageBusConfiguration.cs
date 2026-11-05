namespace Surl.MessageBus;

/// <summary>
/// What <c>--topic</c>, <c>--queue</c>, <c>--topic-map</c> and <c>--topic-access</c> configure
/// on a <see cref="MessageBus"/> (ADR-0102, decision 3), handed to its constructor by the
/// composition. <see cref="None"/>, the default, configures nothing: every name maps by
/// ADR-0100 decision 3.2's default rules and every caller may publish and subscribe.
/// </summary>
public sealed record MessageBusConfiguration
{
    /// <summary>The configuration of a bus nothing was configured for.</summary>
    public static MessageBusConfiguration None { get; } = new();

    /// <summary>The declared topics (<c>--topic</c>).</summary>
    public IReadOnlyList<DeclaredTopic> Topics { get; init; } = [];

    /// <summary>The declared queues (<c>--queue</c>); no two share a name.</summary>
    public IReadOnlyList<DeclaredQueue> Queues { get; init; } = [];

    /// <summary>The name aliases (<c>--topic-map</c>).</summary>
    public IReadOnlyList<TopicMap> TopicMaps { get; init; } = [];

    /// <summary>The access grants (<c>--topic-access</c>); empty means every caller may publish
    /// and subscribe.</summary>
    public IReadOnlyList<TopicAccessGrant> AccessGrants { get; init; } = [];
}
