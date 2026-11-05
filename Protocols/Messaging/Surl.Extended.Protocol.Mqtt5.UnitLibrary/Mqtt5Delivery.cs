using Surl.MessageBus;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// One message waiting to go to a client as a <c>PUBLISH</c>.
/// </summary>
/// <param name="Message">The bus message.</param>
/// <param name="QualityOfService">The QoS it goes at: the lower of its own and the subscription's (<c>[MQTT-3.8.4-8]</c>).</param>
/// <param name="Retain">The RETAIN flag it goes with: 1 for a retained message sent because of a subscribe, 0 for a live one (section 3.3.1.3).</param>
internal sealed record Mqtt5Delivery(BusMessage Message, int QualityOfService, bool Retain);
