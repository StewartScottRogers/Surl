namespace Surl.Extended.Protocol.Kafka;

/// <summary>How a JoinGroup is answered.</summary>
/// <param name="Error">The error code; <see cref="KafkaErrorCode.None"/> for a member that joined.</param>
/// <param name="GenerationId">The generation; -1 on an error.</param>
/// <param name="ProtocolName">The protocol chosen; empty on an error.</param>
/// <param name="Leader">The leader's member id; empty on an error.</param>
/// <param name="MemberId">The member's id; on an error the one asked with, or the new one with <c>MEMBER_ID_REQUIRED</c>.</param>
/// <param name="Members">Every member in join order for the leader; empty for anyone else.</param>
internal sealed record KafkaJoinGroupResult(
    KafkaErrorCode Error,
    int GenerationId,
    string ProtocolName,
    string Leader,
    string MemberId,
    IReadOnlyList<KafkaJoinedMember> Members)
{
    /// <summary>A JoinGroup refused with <paramref name="error"/>.</summary>
    /// <param name="error">The error code.</param>
    /// <param name="memberId">The member id the answer carries.</param>
    /// <returns>The answer.</returns>
    public static KafkaJoinGroupResult Failed(KafkaErrorCode error, string memberId) => new(error, -1, "", "", memberId, []);
}
