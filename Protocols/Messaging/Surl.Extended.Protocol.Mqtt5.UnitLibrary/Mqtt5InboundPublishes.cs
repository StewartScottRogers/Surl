using System.Text;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// One connection's state for the <c>PUBLISH</c>es its client sends (ADR-0093 decision 4): the
/// Topic Aliases the client has set (<c>[MQTT-3.3.2-7]</c>: they belong to the connection, never
/// the session), and, kept in the session, the QoS 2 Packet Identifiers received whose <c>PUBREL</c> has not come
/// (method B of section 4.3.3).
/// </summary>
/// <param name="names">The bus's name lookup, which maps a Topic Name onto the canonical topic.</param>
/// <param name="scheme">The connection's scheme, <c>mqtt5</c> or <c>mqtt5s</c>.</param>
/// <param name="awaitingRelease">The session's QoS 2 Packet Identifiers awaiting <c>PUBREL</c>.</param>
internal sealed class Mqtt5InboundPublishes(TopicNameLookup names, string scheme, HashSet<ushort> awaitingRelease)
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly Dictionary<uint, string> topicByAlias = [];

    /// <summary>
    /// Judges one <c>PUBLISH</c>, in this order: a Subscription Identifier (<c>[MQTT-3.3.4-6]</c>)
    /// or a Payload Format Indicator above 1 (3.3.2.3.2) is <c>0x82</c>; a Topic Alias of 0 or
    /// above <see cref="Mqtt5Broker.TopicAliasMaximum"/> is <c>0x94</c>; an empty Topic Name with
    /// no alias, or an alias never set, is <c>0x82</c>; a Topic Name the bus cannot map is
    /// <c>0x81</c>; a QoS 2 Packet Identifier still awaiting <c>PUBREL</c> is acknowledged
    /// Success and not published again; a QoS 2 <c>PUBLISH</c> past
    /// <see cref="Mqtt5Broker.ReceiveMaximum"/> awaiting <c>PUBREL</c> is <c>0x93</c>; a payload
    /// its Payload Format Indicator calls UTF-8 that is not is <c>0x99</c>, a <c>DISCONNECT</c>
    /// at QoS 0 and an acknowledgement above.
    /// </summary>
    /// <param name="publish">The packet.</param>
    /// <returns>The verdict.</returns>
    public Mqtt5InboundPublishVerdict Judge(Mqtt5PublishPacket publish)
    {
        if (FaultInProperties(publish.Properties) is { } propertyFault)
        {
            return Mqtt5InboundPublishVerdict.Refuse(propertyFault);
        }

        if (FaultInTopic(publish, out var topic) is { } topicFault)
        {
            return Mqtt5InboundPublishVerdict.Refuse(topicFault);
        }

        if (publish.QualityOfService == 2 && awaitingRelease.Contains(publish.PacketIdentifier))
        {
            return Mqtt5InboundPublishVerdict.AnswerOnly(Mqtt5ReasonCode.Success);
        }

        if (publish.QualityOfService == 2 && awaitingRelease.Count >= Mqtt5Broker.ReceiveMaximum)
        {
            return Mqtt5InboundPublishVerdict.Refuse(new(Mqtt5ReasonCode.ReceiveMaximumExceeded, "4.9"));
        }

        return JudgePayload(publish, topic!);
    }

    /// <summary>
    /// Remembers a QoS 2 Packet Identifier whose message was taken, until its <c>PUBREL</c>.
    /// </summary>
    /// <param name="packetIdentifier">The identifier.</param>
    public void AwaitRelease(ushort packetIdentifier) => awaitingRelease.Add(packetIdentifier);

    /// <summary>
    /// Whether a QoS 2 Packet Identifier awaits its <c>PUBREL</c>, so a <c>SUBSCRIBE</c> or
    /// <c>UNSUBSCRIBE</c> reusing it is answered <c>0x91</c> Packet Identifier in use.
    /// </summary>
    /// <param name="packetIdentifier">The identifier.</param>
    /// <returns><see langword="true"/> when it awaits release.</returns>
    public bool IsAwaitingRelease(ushort packetIdentifier) => awaitingRelease.Contains(packetIdentifier);

    /// <summary>
    /// Forgets a QoS 2 Packet Identifier on its <c>PUBREL</c>.
    /// </summary>
    /// <param name="packetIdentifier">The identifier.</param>
    /// <returns><see langword="true"/> when it was awaiting release; otherwise <c>PUBCOMP</c> answers <c>0x92</c>.</returns>
    public bool Release(ushort packetIdentifier) => awaitingRelease.Remove(packetIdentifier);

    private static Mqtt5PacketFault? FaultInProperties(IReadOnlyList<Mqtt5Property> properties)
    {
        if (Mqtt5ConnectJudge.Find(properties, Mqtt5PropertyIdentifier.SubscriptionIdentifier) is not null)
        {
            return Mqtt5PacketFault.ProtocolError("[MQTT-3.3.4-6]");
        }

        if (Mqtt5ConnectJudge.Find(properties, Mqtt5PropertyIdentifier.ResponseTopic) is { } responseTopic
            && responseTopic.Text.AsSpan().IndexOfAny('+', '#') >= 0)
        {
            return Mqtt5PacketFault.ProtocolError("[MQTT-3.3.2-14]");
        }

        return Mqtt5ConnectJudge.Find(properties, Mqtt5PropertyIdentifier.PayloadFormatIndicator)?.Integer > 1
            ? Mqtt5PacketFault.ProtocolError("3.3.2.3.2")
            : null;
    }

    private static bool IsWellFormedUtf8(byte[] payload)
    {
        try
        {
            StrictUtf8.GetCharCount(payload);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private Mqtt5PacketFault? FaultInTopic(Mqtt5PublishPacket publish, out string? canonicalTopic)
    {
        canonicalTopic = null;
        if (ResolveAlias(publish) is not { } topicName)
        {
            return AliasFault(publish);
        }

        canonicalTopic = names.ToCanonicalTopic(scheme, topicName).Name;
        return canonicalTopic is null ? Mqtt5PacketFault.Malformed("[MQTT-4.7.3-1]") : null;
    }

    // The topic a PUBLISH names: its own, setting its alias when it has one, or its alias's.
    private string? ResolveAlias(Mqtt5PublishPacket publish)
    {
        var alias = Mqtt5ConnectJudge.Find(publish.Properties, Mqtt5PropertyIdentifier.TopicAlias)?.Integer;
        if (alias is 0 or > Mqtt5Broker.TopicAliasMaximum)
        {
            return null;
        }

        if (publish.TopicName.Length > 0)
        {
            if (alias is { } newAlias)
            {
                topicByAlias[newAlias] = publish.TopicName;
            }

            return publish.TopicName;
        }

        return alias is { } usedAlias ? topicByAlias.GetValueOrDefault(usedAlias) : null;
    }

    private static Mqtt5PacketFault AliasFault(Mqtt5PublishPacket publish) =>
        Mqtt5ConnectJudge.Find(publish.Properties, Mqtt5PropertyIdentifier.TopicAlias)?.Integer is 0 or > Mqtt5Broker.TopicAliasMaximum
            ? new(Mqtt5ReasonCode.TopicAliasInvalid, "3.3.2.3.4")
            : Mqtt5PacketFault.ProtocolError("3.3.2.3.4");

    private static Mqtt5InboundPublishVerdict JudgePayload(Mqtt5PublishPacket publish, string topic)
    {
        var utf8Payload = Mqtt5ConnectJudge.Find(publish.Properties, Mqtt5PropertyIdentifier.PayloadFormatIndicator)?.Integer == 1;
        if (utf8Payload && !IsWellFormedUtf8(publish.Payload))
        {
            return publish.QualityOfService == 0
                ? Mqtt5InboundPublishVerdict.Refuse(new(Mqtt5ReasonCode.PayloadFormatInvalid, "3.3.2.3.2"))
                : Mqtt5InboundPublishVerdict.AnswerOnly(Mqtt5ReasonCode.PayloadFormatInvalid);
        }

        var properties = publish.Properties.Where(property => property.Identifier != Mqtt5PropertyIdentifier.TopicAlias).ToList();
        return Mqtt5InboundPublishVerdict.Publish(new(topic, publish.Payload, publish.QualityOfService, publish.Retain, properties));
    }
}
