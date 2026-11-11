namespace Surl.Extended.Protocol.Kafka;

/// <summary>One protocol a JoinGroup offers: its name and the member's metadata for it, relayed verbatim.</summary>
/// <param name="Name">The protocol's name, such as <c>range</c>.</param>
/// <param name="Metadata">The member's metadata for the protocol.</param>
internal sealed record KafkaGroupProtocol(string Name, ReadOnlyMemory<byte> Metadata);
