namespace Surl.Extended.Protocol.Kafka;

/// <summary>A JoinGroup request's fields, as <see cref="KafkaGroupCoordinator"/> takes them.</summary>
/// <param name="Group">The group id, not empty.</param>
/// <param name="SessionTimeoutMs">The session timeout asked for.</param>
/// <param name="RebalanceTimeoutMs">The rebalance timeout: v0's session timeout, otherwise the field, within 0 to 1800000.</param>
/// <param name="MemberId">The member id; empty for a member joining for the first time.</param>
/// <param name="InstanceId">The static member's group instance id (v5); null for a dynamic member.</param>
/// <param name="ClientId">The request header's client id; null written as empty in a member id.</param>
/// <param name="ProtocolType">The protocol type, such as <c>consumer</c>.</param>
/// <param name="Protocols">The protocols offered, in the member's order of preference.</param>
/// <param name="IsMemberIdRequired">Whether a dynamic member joining with an empty member id is answered
/// <c>MEMBER_ID_REQUIRED</c> (v4 and v5).</param>
internal sealed record KafkaJoinGroupRequest(
    string Group,
    int SessionTimeoutMs,
    int RebalanceTimeoutMs,
    string MemberId,
    string? InstanceId,
    string? ClientId,
    string ProtocolType,
    IReadOnlyList<KafkaGroupProtocol> Protocols,
    bool IsMemberIdRequired);
