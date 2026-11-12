using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// The chosen-error fault (ADR-0124 decisions 4 and 9, ADR-0142): each <c>PUBLISH</c> the broker
/// would publish, and each <c>SUBSCRIBE</c> Topic Filter, is asked about at a
/// <see cref="FaultStage.Command"/> point - the packet type as its command, the topic or filter as its
/// path, the account the login proved - before the bus is touched. A matching
/// <see cref="FaultKind.Error"/> rule's reason code 128 to 255 is sent where MQTT 5.0 puts a reason
/// code: in the filter's <c>SUBACK</c> slot, in the <c>PUBACK</c> or <c>PUBREC</c> of a QoS 1 or 2
/// <c>PUBLISH</c>, and in a <c>DISCONNECT</c> after a QoS 0 one, which has no answer. A code the
/// packet may not carry (tables in sections 3.4.2.1, 3.5.2.1, 3.9.3 and 3.14.2.1) is sent as
/// <c>0x80</c>, Unspecified error.
/// </summary>
internal sealed partial class Mqtt5BrokerConnection
{
    private const string PublishPacketName = "PUBLISH";
    private const string SubscribePacketName = "SUBSCRIBE";
    private const int LowestReasonCode = 128;

    // Section 3.4.2.1 (PUBACK) and 3.5.2.1 (PUBREC): the same error codes.
    private static readonly HashSet<byte> PublishResponseErrors = [0x80, 0x83, 0x87, 0x90, 0x91, 0x97, 0x99];

    // Section 3.9.3 (SUBACK).
    private static readonly HashSet<byte> SubscribeAcknowledgementErrors = [0x80, 0x83, 0x87, 0x8F, 0x91, 0x97, 0x9E, 0xA1, 0xA2];

    // Section 3.14.2.1: the DISCONNECT error codes a server may send.
    private static readonly HashSet<byte> ServerDisconnectErrors =
        [0x80, 0x81, 0x82, 0x83, 0x87, 0x89, 0x8B, 0x8D, 0x8E, 0x8F, 0x90, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0x9B, 0x9C, 0x9D, 0x9E, 0x9F, 0xA0, 0xA1, 0xA2];

    /// <summary>
    /// The reason code a packet whose allowed error codes are <paramref name="allowed"/> carries for
    /// the chosen <paramref name="reasonCode"/>: itself, or <c>0x80</c> when the packet may not carry it.
    /// </summary>
    internal static Mqtt5ReasonCode ReasonCodeIn(IReadOnlySet<byte> allowed, byte reasonCode) =>
        allowed.Contains(reasonCode) ? (Mqtt5ReasonCode)reasonCode : Mqtt5ReasonCode.UnspecifiedError;

    // The reason code a matching error rule chooses, or null to answer the packet as usual.
    private async ValueTask<byte?> FindChosenReasonCodeAsync(string packetName, string topic)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = session!.Account,
            Path = topic,
            Command = packetName,
        };
        if (await context.Faults.FindAsync(point, context.CancellationToken) is not { } rule)
        {
            return null;
        }

        var target = $"{packetName} {topic}";
        if (rule.Kind != FaultKind.Error || !TryReadReasonCode(rule.Reply, out var reasonCode))
        {
            context.Log.Note($"{target}: fault rule {rule.Id} is not an error with an MQTT reason code 128 to 255; not applied.");
            return null;
        }

        context.Log.Note($"{target} answered by fault rule {rule.Id} with reason code {reasonCode}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, target));
        return reasonCode;
    }

    private static bool TryReadReasonCode(string? reply, out byte reasonCode) =>
        byte.TryParse(reply, NumberStyles.None, CultureInfo.InvariantCulture, out reasonCode) && reasonCode >= LowestReasonCode;

    // A PUBLISH a rule matches is not published: QoS 0 ends with DISCONNECT, QoS 1 and 2 are
    // acknowledged with the code and the connection goes on.
    private async ValueTask<Mqtt5ConnectionEnd?> AnswerPublishByAFaultAsync(Mqtt5PublishPacket publish, Mqtt5ApplicationMessage message)
    {
        // The topic as the bus names it, so a Topic Alias resolves to the topic it stands for.
        var topic = message.Topic;
        if (await FindChosenReasonCodeAsync(PublishPacketName, topic) is not { } reasonCode)
        {
            return null;
        }

        return publish.QualityOfService == 0
            ? await DisconnectAsync(ReasonCodeIn(ServerDisconnectErrors, reasonCode), $"A QoS 0 PUBLISH to {topic} was answered by a fault rule")
            : await AcknowledgePublishAsync(publish, ReasonCodeIn(PublishResponseErrors, reasonCode));
    }

    // Each filter a rule matches gets the code in its SUBACK slot and never reaches the bus.
    private async ValueTask<Mqtt5ReasonCode?> FindChosenSubscribeCodeAsync(Mqtt5Subscription subscription) =>
        await FindChosenReasonCodeAsync(SubscribePacketName, subscription.TopicFilter) is { } reasonCode
            ? ReasonCodeIn(SubscribeAcknowledgementErrors, reasonCode)
            : null;
}
