namespace Surl.Protocol.Abstractions;

/// <summary>
/// What a <see cref="ConsumerGroupChangedEvent"/> says happened to its consumer group (ADR-0145 decision 10, published
/// where ADR-0145 decision 3 says).
/// </summary>
public enum ConsumerGroupChange
{
    /// <summary>
    /// A member joined the group: its JoinGroup was taken, with the member id it holds.
    /// </summary>
    MemberJoined,

    /// <summary>
    /// A member left the group by its own LeaveGroup.
    /// </summary>
    MemberLeft,

    /// <summary>
    /// The broker removed a member: its session expired, or it did not rejoin by the rebalance timeout. No exchange
    /// carries it.
    /// </summary>
    MemberRemoved,

    /// <summary>
    /// A static member's old member id was fenced, because a JoinGroup naming its <c>group_instance_id</c> gave the
    /// instance a new one.
    /// </summary>
    MemberFenced,

    /// <summary>
    /// The join phase ended and a new generation began. It names no member.
    /// </summary>
    GenerationStarted,

    /// <summary>
    /// The leader's SyncGroup was taken and its assignment handed to the members. It names no member.
    /// </summary>
    AssignmentDistributed,
}
