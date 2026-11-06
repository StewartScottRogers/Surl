using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// One request of a served API and version, its header read.
/// </summary>
/// <param name="Header">The request header.</param>
/// <param name="Range">The served range the request's version lies in.</param>
/// <param name="Fields">A reader positioned at the request's own fields, after the header.</param>
/// <param name="Connection">What the broker keeps of the connection the request came on: its TLS session and SASL state.</param>
/// <param name="Transport">The connection itself, which a fault rule's drop aborts (ADR-0141).</param>
public sealed record KafkaRequest(KafkaRequestHeader Header, KafkaApiVersionRange Range, KafkaPrimitiveReader Fields, KafkaConnectionState Connection, IConnection Transport);
