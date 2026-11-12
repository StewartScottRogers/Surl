namespace Surl.Extended.Protocol.Kafka;

/// <summary>One member as the leader's JoinGroup answer lists it.</summary>
/// <param name="MemberId">The member id.</param>
/// <param name="InstanceId">The group instance id; null for a dynamic member.</param>
/// <param name="Metadata">The member's metadata for the protocol chosen.</param>
internal sealed record KafkaJoinedMember(string MemberId, string? InstanceId, ReadOnlyMemory<byte> Metadata);
