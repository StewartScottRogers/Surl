namespace Surl.Extended.Protocol.Kafka;

/// <summary>A consumer group's state, as DescribeGroups names it (ADR-0145 decisions 3 and 5).</summary>
internal enum KafkaGroupState
{
    /// <summary>No members.</summary>
    Empty,

    /// <summary>The join phase: members are rejoining.</summary>
    PreparingRebalance,

    /// <summary>The join phase ended; the members wait for the leader's SyncGroup.</summary>
    CompletingRebalance,

    /// <summary>Every member has its assignment.</summary>
    Stable,
}
