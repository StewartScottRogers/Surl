namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// A consumer group held by <see cref="KafkaGroupCoordinator"/>, as DescribeGroups writes it
/// (ADR-0145 decision 5).
/// </summary>
/// <param name="State">The state's name: <c>Empty</c>, <c>PreparingRebalance</c>, <c>CompletingRebalance</c> or <c>Stable</c>.</param>
/// <param name="ProtocolType">The protocol type the members share; empty when the group has none.</param>
/// <param name="ProtocolName">The protocol chosen, in <c>CompletingRebalance</c> and <c>Stable</c>; empty otherwise.</param>
/// <param name="Members">The members in join order.</param>
internal sealed record KafkaGroupDescription(
    string State,
    string ProtocolType,
    string ProtocolName,
    IReadOnlyList<KafkaGroupMemberDescription> Members);
