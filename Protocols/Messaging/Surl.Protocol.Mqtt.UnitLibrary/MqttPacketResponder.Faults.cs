using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// The chosen-error fault (ADR-0124 decision 4, ADR-0139): an accepted <c>CONNECT</c>, each valid
/// <c>SUBSCRIBE</c> topic filter and each well-formed <c>PUBLISH</c> is asked about at a
/// <see cref="FaultStage.Command"/> point once the login has been judged and before the message bus
/// is touched. A matching <see cref="FaultKind.Error"/> rule's reply is an MQTT 5 reason code 128 to
/// 255, which MQTT 3.1.1 answers as it can: a <c>CONNECT</c> with the nearest <c>CONNACK</c> return
/// code and a close, a filter with <c>SUBACK</c> failure <c>0x80</c>, a <c>PUBLISH</c> - which has
/// no negative acknowledgement in 3.1.1 - with a close and nothing published.
/// </summary>
internal sealed partial class MqttPacketResponder
{
    private const string ConnectPacketName = "CONNECT";
    private const string PublishPacketName = "PUBLISH";
    private const string SubscribePacketName = "SUBSCRIBE";
    private const int LowestReasonCode = 128;
    private const byte ServerUnavailable = 0x03;

    // The MQTT 5 CONNACK reason codes that have an MQTT 3.1.1 return code of the same meaning
    // (MQTT 5.0, section 3.2.2.2); every other reason code is answered 3, server unavailable.
    private static readonly Dictionary<byte, byte> ConnectReturnCodes = new()
    {
        [0x84] = 0x01,
        [0x85] = 0x02,
        [0x86] = 0x04,
        [0x87] = 0x05,
    };

    /// <summary>
    /// The CONNACK return code MQTT 3.1.1 answers an MQTT 5 <paramref name="reasonCode"/> with.
    /// </summary>
    internal static byte ConnectReturnCodeFor(byte reasonCode) =>
        ConnectReturnCodes.GetValueOrDefault(reasonCode, ServerUnavailable);

    // The reason code a matching error rule chooses for the packet, or null to answer it as usual.
    private async ValueTask<byte?> FindChosenReasonCodeAsync(string packetName, string? topic)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = FaultListenUrl.Text(context.ListenUrl),
            Scheme = context.Scheme,
            Account = account,
            Path = topic,
            Command = packetName,
        };
        if (await context.Faults.FindAsync(point, context.CancellationToken) is not { } rule)
        {
            return null;
        }

        var target = topic is null ? packetName : $"{packetName} {topic}";
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

    // A connection whose login was accepted, answered with a chosen CONNACK and closed instead.
    private async ValueTask<bool> AnswerAcceptedConnectAsync()
    {
        if (await FindChosenReasonCodeAsync(ConnectPacketName, null) is not { } reasonCode)
        {
            connected = true;
            return await SendAndStayOpenAsync(MqttPacketEncoder.ConnectAccepted);
        }

        return await SendAndCloseAsync(MqttPacketEncoder.ConnectAcknowledgement(ConnectReturnCodeFor(reasonCode)));
    }

    // Each valid filter a rule matches is refused as an invalid one is, SUBACK 0x80, and never
    // reaches the bus.
    private async ValueTask RefuseChosenFiltersAsync(List<string?> filters)
    {
        for (var index = 0; index < filters.Count; index++)
        {
            if (filters[index] is { } filter && await FindChosenReasonCodeAsync(SubscribePacketName, filter) is not null)
            {
                filters[index] = null;
            }
        }
    }

    private async ValueTask<bool> IsPublishAnsweredByAFaultAsync(string topic) =>
        await FindChosenReasonCodeAsync(PublishPacketName, topic) is not null;
}
