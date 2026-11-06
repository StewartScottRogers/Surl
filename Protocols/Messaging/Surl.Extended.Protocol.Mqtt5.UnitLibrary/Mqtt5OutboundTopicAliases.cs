namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// The Topic Aliases the broker uses toward one client (ADR-0093 decision 4): none when the
/// client's <c>CONNECT</c> sent no Topic Alias Maximum above 0 (<c>[MQTT-3.1.2-27]</c>);
/// otherwise the first delivery of a topic gives it the next free alias with the full Topic Name,
/// later ones send the alias and an empty Topic Name, and once the client's maximum is used up the
/// alias assigned longest ago is assigned again (<c>[MQTT-3.3.2-11]</c>, <c>[MQTT-3.1.2-26]</c>).
/// </summary>
/// <param name="maximum">The client's Topic Alias Maximum; 0 for none.</param>
internal sealed class Mqtt5OutboundTopicAliases(uint maximum)
{
    private readonly Dictionary<string, uint> aliasByTopic = new(StringComparer.Ordinal);
    private readonly Queue<string> topicsInAssignmentOrder = new();

    /// <summary>
    /// The Topic Name and Topic Alias a delivery on <paramref name="topic"/> carries.
    /// </summary>
    /// <param name="topic">The topic, as the client names it.</param>
    /// <returns>The Topic Name - empty when the alias stands for it - and the alias, or
    /// <see langword="null"/> when aliases are not used.</returns>
    public (string TopicName, Mqtt5Property? Alias) Assign(string topic)
    {
        if (maximum == 0)
        {
            return (topic, null);
        }

        if (aliasByTopic.TryGetValue(topic, out var known))
        {
            return (string.Empty, AliasProperty(known));
        }

        var alias = (uint)topicsInAssignmentOrder.Count + 1;
        if (topicsInAssignmentOrder.Count == maximum)
        {
            var oldest = topicsInAssignmentOrder.Dequeue();
            alias = aliasByTopic[oldest];
            aliasByTopic.Remove(oldest);
        }

        aliasByTopic[topic] = alias;
        topicsInAssignmentOrder.Enqueue(topic);
        return (topic, AliasProperty(alias));
    }

    private static Mqtt5Property AliasProperty(uint alias) => Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.TopicAlias, alias);
}
