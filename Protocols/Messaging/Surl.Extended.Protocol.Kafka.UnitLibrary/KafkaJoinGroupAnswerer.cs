using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers JoinGroup v0 to v5 through the broker's <see cref="KafkaGroupCoordinator"/> (ADR-0145
/// decisions 3, 6, 9 and 10; the Kafka protocol guide, JoinGroup).
/// </summary>
/// <remarks>
/// <para>
/// The request is first put to the exchange's fault plan (<see cref="KafkaCommandFault"/>, command
/// <c>JoinGroup</c>, path the group id): a drop closes with no bytes, an error rule's code is the
/// <c>error_code</c> and nothing changes. Then an empty <c>group_id</c> is <c>INVALID_GROUP_ID</c>
/// (24), a caller who may not use the group <c>GROUP_AUTHORIZATION_FAILED</c> (30), and the
/// coordinator makes the rest of the checks and holds the answer until the join phase ends, the
/// exchange's idle clock paused meanwhile (<see cref="KafkaHeldAnswer"/>).
/// </para>
/// <para>
/// v0 has no <c>rebalance_timeout_ms</c> and uses <c>session_timeout_ms</c>; a negative one is read
/// as 0, and one past 1800000 as 1800000. v4 and v5 answer a dynamic member's first join
/// <c>MEMBER_ID_REQUIRED</c> (79); v5 reads <c>group_instance_id</c> and lists it for each member.
/// v2 to v5 start with <c>throttle_time_ms</c> 0. A refusal answers generation -1, an empty protocol
/// name and leader, the member id asked with (or the new one with 79) and no members.
/// </para>
/// </remarks>
/// <param name="coordinator">The broker's group coordinator.</param>
/// <param name="bus">The shared message bus, which answers the group access check.</param>
public sealed class KafkaJoinGroupAnswerer(KafkaGroupCoordinator coordinator, MessageBus.MessageBus bus) : IKafkaApiAnswerer
{
    /// <summary>The command a fault rule names to match JoinGroup: the guide's API name.</summary>
    public const string FaultCommand = "JoinGroup";

    private readonly KafkaGroupCoordinator coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    private readonly MessageBus.MessageBus bus = bus ?? throw new ArgumentNullException(nameof(bus));

    /// <inheritdoc/>
    public KafkaApiKey ApiKey => KafkaApiKey.JoinGroup;

    /// <inheritdoc/>
    public async ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var version = request.Header.ApiVersion;
        var asked = Read(request, version);
        var fault = await KafkaCommandFault.FindAsync(request, context, FaultCommand, asked.Group);
        if (fault.DropNote is { } note)
        {
            return KafkaAnswer.Close(note);
        }

        var refusal = fault.Error
            ?? (asked.Group.Length == 0 ? KafkaErrorCode.InvalidGroupId
            : !bus.MayUseGroup(request.Connection.AccountName, asked.Group) ? KafkaErrorCode.GroupAuthorizationFailed
            : KafkaErrorCode.None);
        var result = refusal != KafkaErrorCode.None
            ? KafkaJoinGroupResult.Failed(refusal, asked.MemberId)
            : await KafkaHeldAnswer.WaitAsync(coordinator.JoinAsync(asked, KafkaGroupCaller.Of(request, context)), context);
        return KafkaAnswer.Respond(Encode(version, result));
    }

    private static KafkaJoinGroupRequest Read(KafkaRequest request, short version)
    {
        var fields = request.Fields;
        var group = fields.ReadString();
        var sessionTimeout = fields.ReadInt32();
        var rebalanceTimeout = version >= 1 ? fields.ReadInt32() : sessionTimeout;
        var memberId = fields.ReadString();
        var instanceId = version >= 5 ? fields.ReadNullableString() : null;
        var protocolType = fields.ReadString();
        var count = fields.ReadArrayLength().GetValueOrDefault();
        var protocols = new List<KafkaGroupProtocol>();
        for (var index = 0; index < count; index++)
        {
            protocols.Add(new KafkaGroupProtocol(fields.ReadString(), fields.ReadBytes()));
        }

        return new KafkaJoinGroupRequest(
            group,
            sessionTimeout,
            Math.Clamp(rebalanceTimeout, 0, KafkaGroupCoordinator.MaxSessionTimeoutMs),
            memberId,
            instanceId,
            request.Header.ClientId,
            protocolType,
            protocols,
            IsMemberIdRequired: version >= 4);
    }

    private static byte[] Encode(short version, KafkaJoinGroupResult result)
    {
        var body = new KafkaPrimitiveWriter();
        if (version >= 2)
        {
            body.WriteInt32(0);
        }

        body.WriteInt16((short)result.Error);
        body.WriteInt32(result.GenerationId);
        body.WriteString(result.ProtocolName);
        body.WriteString(result.Leader);
        body.WriteString(result.MemberId);
        body.WriteArrayLength(result.Members.Count);
        foreach (var member in result.Members)
        {
            body.WriteString(member.MemberId);
            if (version >= 5)
            {
                body.WriteNullableString(member.InstanceId);
            }

            body.WriteBytes(member.Metadata.Span);
        }

        return body.ToArray();
    }
}
