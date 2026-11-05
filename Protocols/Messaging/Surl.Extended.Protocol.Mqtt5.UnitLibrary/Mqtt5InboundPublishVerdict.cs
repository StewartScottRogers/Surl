namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// What <see cref="Mqtt5InboundPublishes.Judge"/> made of one client <c>PUBLISH</c>: exactly one
/// of a message to publish, a fault to answer with <c>DISCONNECT</c>, or an answer to acknowledge
/// with and publish nothing.
/// </summary>
/// <param name="Message">The message to publish, its topic canonical; <see langword="null"/> when there is none.</param>
/// <param name="Fault">The fault the connection is closed for; <see langword="null"/> when there is none.</param>
/// <param name="Answer">The reason code acknowledged without publishing: Success for a repeated
/// QoS 2 <c>PUBLISH</c>, Payload format invalid for a mislabelled payload at QoS 1 or 2.</param>
internal sealed record Mqtt5InboundPublishVerdict(
    Mqtt5ApplicationMessage? Message,
    Mqtt5PacketFault? Fault,
    Mqtt5ReasonCode Answer)
{
    /// <summary>A message to publish.</summary>
    public static Mqtt5InboundPublishVerdict Publish(Mqtt5ApplicationMessage message) => new(message, null, Mqtt5ReasonCode.Success);

    /// <summary>A fault that closes the connection.</summary>
    public static Mqtt5InboundPublishVerdict Refuse(Mqtt5PacketFault fault) => new(null, fault, Mqtt5ReasonCode.Success);

    /// <summary>An acknowledgement with nothing published.</summary>
    public static Mqtt5InboundPublishVerdict AnswerOnly(Mqtt5ReasonCode answer) => new(null, null, answer);
}
