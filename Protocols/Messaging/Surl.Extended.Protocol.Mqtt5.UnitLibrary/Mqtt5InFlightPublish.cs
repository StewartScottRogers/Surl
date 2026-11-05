namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// A QoS 1 or 2 <c>PUBLISH</c> the broker sent and the client has not yet finished acknowledging.
/// </summary>
/// <param name="Sequence">Its place in the sending order, which a resumed session resends in (<c>[MQTT-4.4.0-1]</c>).</param>
/// <param name="Awaited">The packet awaited: <c>PUBACK</c>, <c>PUBREC</c>, or after a <c>PUBREC</c> <c>PUBCOMP</c>.</param>
/// <param name="Delivery">The message as it was sent.</param>
internal sealed record Mqtt5InFlightPublish(long Sequence, Mqtt5PacketType Awaited, Mqtt5Delivery Delivery);
