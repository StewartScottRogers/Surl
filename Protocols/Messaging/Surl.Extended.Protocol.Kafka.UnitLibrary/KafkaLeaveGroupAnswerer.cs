using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers LeaveGroup v0 to v3 through the broker's <see cref="KafkaGroupCoordinator"/> (ADR-0145
/// decisions 3, 6 and 10; the Kafka protocol guide, LeaveGroup).
/// </summary>
/// <remarks>
/// The request is first put to the exchange's fault plan (<see cref="KafkaCommandFault"/>, command
/// <c>LeaveGroup</c>, path the group id): a drop closes with no bytes, an error rule's code is the
/// top-level <c>error_code</c> and nothing changes. Then a caller who may not use the group is
/// <c>GROUP_AUTHORIZATION_FAILED</c> (30) at the top level. v0 to v2 remove <c>member_id</c>, its
/// error the top-level one (25 for a member the group does not hold); v3 removes each of
/// <c>members</c> by member id, or by group instance id with an empty member id, each answered with
/// its own error (0, 25 or 82) and the top-level <c>error_code</c> 0; a top-level refusal lists no
/// members. v1 to v3 start with <c>throttle_time_ms</c> 0.
/// </remarks>
/// <param name="coordinator">The broker's group coordinator.</param>
/// <param name="bus">The shared message bus, which answers the group access check.</param>
public sealed class KafkaLeaveGroupAnswerer(KafkaGroupCoordinator coordinator, MessageBus.MessageBus bus) : IKafkaApiAnswerer
{
    /// <summary>The command a fault rule names to match LeaveGroup: the guide's API name.</summary>
    public const string FaultCommand = "LeaveGroup";

    private const short BatchVersion = 3;

    private readonly KafkaGroupCoordinator coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    private readonly MessageBus.MessageBus bus = bus ?? throw new ArgumentNullException(nameof(bus));

    /// <inheritdoc/>
    public KafkaApiKey ApiKey => KafkaApiKey.LeaveGroup;

    /// <inheritdoc/>
    public async ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var version = request.Header.ApiVersion;
        var group = request.Fields.ReadString();
        var leaving = version >= BatchVersion ? ReadMembers(request.Fields) : [new Leaver(request.Fields.ReadString(), null)];
        var fault = await KafkaCommandFault.FindAsync(request, context, FaultCommand, group);
        if (fault.DropNote is { } note)
        {
            return KafkaAnswer.Close(note);
        }

        var refusal = fault.Error
            ?? (!bus.MayUseGroup(request.Connection.AccountName, group) ? KafkaErrorCode.GroupAuthorizationFailed : KafkaErrorCode.None);
        if (refusal != KafkaErrorCode.None)
        {
            return KafkaAnswer.Respond(Encode(version, refusal, []));
        }

        var caller = KafkaGroupCaller.Of(request, context);
        var answered = leaving.Select(member => member with { Error = coordinator.Leave(group, member.MemberId, member.InstanceId, caller) }).ToList();
        return KafkaAnswer.Respond(version >= BatchVersion ? Encode(version, KafkaErrorCode.None, answered) : Encode(version, answered[0].Error, []));
    }

    private static List<Leaver> ReadMembers(KafkaPrimitiveReader fields)
    {
        var count = fields.ReadArrayLength().GetValueOrDefault();
        var members = new List<Leaver>();
        for (var index = 0; index < count; index++)
        {
            members.Add(new Leaver(fields.ReadString(), fields.ReadNullableString()));
        }

        return members;
    }

    private static byte[] Encode(short version, KafkaErrorCode error, List<Leaver> members)
    {
        var body = new KafkaPrimitiveWriter();
        if (version >= 1)
        {
            body.WriteInt32(0);
        }

        body.WriteInt16((short)error);
        if (version >= BatchVersion)
        {
            body.WriteArrayLength(members.Count);
            foreach (var member in members)
            {
                body.WriteString(member.MemberId);
                body.WriteNullableString(member.InstanceId);
                body.WriteInt16((short)member.Error);
            }
        }

        return body.ToArray();
    }

    // One member a LeaveGroup names, and its answer.
    private sealed record Leaver(string MemberId, string? InstanceId, KafkaErrorCode Error = KafkaErrorCode.None);
}
