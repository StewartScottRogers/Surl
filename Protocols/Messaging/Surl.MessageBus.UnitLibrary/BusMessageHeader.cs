namespace Surl.MessageBus;

/// <summary>
/// One string header a bus message carries (ADR-0100, decision 3.4): an MQTT 5 User
/// Property, a string AMQP header or application property, a NATS or STOMP header, or a
/// UTF-8 Kafka record header. A message's headers are ordered and a name may repeat.
/// </summary>
/// <param name="Name">The header's name.</param>
/// <param name="Value">The header's value.</param>
public sealed record BusMessageHeader(string Name, string Value);
