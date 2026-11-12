namespace Surl.Extended.Protocol.Kafka;

/// <summary>One member of a <see cref="KafkaGroupDescription"/>.</summary>
/// <param name="MemberId">The member id.</param>
/// <param name="InstanceId">The group instance id; null for a dynamic member.</param>
/// <param name="ClientId">The client id of the member's latest JoinGroup; empty when it sent none.</param>
/// <param name="ClientHost">The remote address of that JoinGroup, as <c>/&lt;address&gt;</c>.</param>
/// <param name="Metadata">The member's metadata for the chosen protocol; empty when none is chosen.</param>
/// <param name="Assignment">The member's assignment in a <c>Stable</c> group; empty otherwise.</param>
internal sealed record KafkaGroupMemberDescription(
    string MemberId,
    string? InstanceId,
    string ClientId,
    string ClientHost,
    ReadOnlyMemory<byte> Metadata,
    ReadOnlyMemory<byte> Assignment);
