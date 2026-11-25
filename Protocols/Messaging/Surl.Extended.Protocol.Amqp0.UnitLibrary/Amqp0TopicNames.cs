using System.Text;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The routing and binding keys of one listener's scheme, mapped onto the bus's canonical names and
/// back through <see cref="MessageBus.MessageBus.Names"/>: ADR-0100 decision 3.2's rules for AMQP
/// 0-9-1, with each <c>--topic-map</c> alias of one exact key applied both ways (ADR-0102 decision 3.3).
/// </summary>
/// <param name="Lookup">The bus's topic name lookup.</param>
/// <param name="Scheme"><c>amqp</c> or <c>amqps</c>, which share one map.</param>
internal sealed record Amqp0TopicNames(TopicNameLookup Lookup, string Scheme)
{
    /// <summary>The canonical topic a message published with <paramref name="routingKey"/> is on.</summary>
    public TopicNameMapping ToCanonicalTopic(string? routingKey) => Lookup.ToCanonicalTopic(Scheme, routingKey);

    /// <summary>The canonical filter <paramref name="bindingKey"/> binds on <c>amq.topic</c>.</summary>
    public TopicNameMapping ToCanonicalFilter(string bindingKey) => Lookup.ToCanonicalFilter(Scheme, bindingKey);

    /// <summary>
    /// The routing key a client is sent for <paramref name="topic"/>; unmapped when it has none,
    /// which includes a <c>--topic-map</c> name too long for a <c>shortstr</c>.
    /// </summary>
    public TopicNameMapping FromCanonicalTopic(string topic)
    {
        var mapped = Lookup.FromCanonicalTopic(Scheme, topic);
        return mapped.IsMapped && Encoding.UTF8.GetByteCount(mapped.Name) > AmqpTopicExchangeNames.MaxKeyBytes
            ? new(TopicNameMappingOutcome.Unreachable, null)
            : mapped;
    }
}
