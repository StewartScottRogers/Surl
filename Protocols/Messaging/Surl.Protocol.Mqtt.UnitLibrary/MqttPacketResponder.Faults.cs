using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// The command-stage faults (ADR-0124 decision 4, ADR-0139, ADR-0141): an accepted <c>CONNECT</c>,
/// each valid <c>SUBSCRIBE</c> topic filter and each well-formed <c>PUBLISH</c> is asked about at a
/// <see cref="FaultStage.Command"/> point once the login has been judged and before the message bus
/// is touched. A matching <see cref="FaultKind.Delay"/> rule holds the packet's answer for the rule's
/// <c>ms</c> with the idle clock paused, then answers it as usual; a <see cref="FaultKind.Drop"/> rule
/// aborts the connection with no answer to the packet (<see cref="CommandStageFaults"/>). A matching
/// <see cref="FaultKind.Error"/> rule's reply is an MQTT 5 reason code 128 to 255, which MQTT 3.1.1
/// answers as it can: a <c>CONNECT</c> with the nearest <c>CONNACK</c> return code and a close, a
/// filter with <c>SUBACK</c> failure <c>0x80</c>, a <c>PUBLISH</c> - which has no negative
/// acknowledgement in 3.1.1 - with a close and nothing published.
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

    // Set once a drop rule has aborted the connection: nothing more is answered.
    private bool isDroppedByAFault;

    /// <summary>
    /// The CONNACK return code MQTT 3.1.1 answers an MQTT 5 <paramref name="reasonCode"/> with.
    /// </summary>
    internal static byte ConnectReturnCodeFor(byte reasonCode) =>
        ConnectReturnCodes.GetValueOrDefault(reasonCode, ServerUnavailable);

    // Applies the packet's delay or drop, if any, and returns the reason code a matching error
    // rule chooses for it, or null to answer it as usual (or not at all after a drop).
    private async ValueTask<byte?> FindChosenReasonCodeAsync(string packetName, string? topic)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = account,
            Path = topic,
            Command = packetName,
        };
        var target = topic is null ? packetName : $"{packetName} {topic}";

        return await CommandStageFaults.FindAndApplyAsync(context, connection, point, target) switch
        {
            null or { Kind: FaultKind.Delay } => null,
            { Kind: FaultKind.Drop } => Dropped(),
            var rule => ChosenReasonCode(rule, target),
        };
    }

    private byte? Dropped()
    {
        isDroppedByAFault = true;
        return null;
    }

    private byte? ChosenReasonCode(FaultRule rule, string target)
    {
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

    // A connection whose login was accepted, answered with a chosen CONNACK and closed instead,
    // or with nothing after a drop.
    private async ValueTask<bool> AnswerAcceptedConnectAsync()
    {
        var chosen = await FindChosenReasonCodeAsync(ConnectPacketName, null);
        if (isDroppedByAFault)
        {
            return false;
        }

        if (chosen is not { } reasonCode)
        {
            connected = true;
            return await SendAndStayOpenAsync(MqttPacketEncoder.ConnectAccepted);
        }

        return await SendAndCloseAsync(MqttPacketEncoder.ConnectAcknowledgement(ConnectReturnCodeFor(reasonCode)));
    }

    // Each valid filter an error rule matches is refused as an invalid one is, SUBACK 0x80, and
    // never reaches the bus; a drop stops at its filter.
    private async ValueTask RefuseChosenFiltersAsync(List<string?> filters)
    {
        for (var index = 0; index < filters.Count && !isDroppedByAFault; index++)
        {
            if (filters[index] is { } filter && await FindChosenReasonCodeAsync(SubscribePacketName, filter) is not null)
            {
                filters[index] = null;
            }
        }
    }

    private async ValueTask<bool> IsPublishAnsweredByAFaultAsync(string topic) =>
        await FindChosenReasonCodeAsync(PublishPacketName, topic) is not null || isDroppedByAFault;
}
