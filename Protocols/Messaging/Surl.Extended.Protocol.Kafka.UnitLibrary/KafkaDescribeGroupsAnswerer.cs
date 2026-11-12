using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers DescribeGroups v0 to v4 from the broker's <see cref="KafkaGroupCoordinator"/> and the
/// offsets <c>Surl.MessageBus</c> keeps (ADR-0145 decisions 5, 6 and 10; the Kafka protocol guide,
/// DescribeGroups).
/// </summary>
/// <remarks>
/// <para>
/// One row is answered for each group asked, in request order. Each group is first put to the
/// exchange's fault plan (<see cref="KafkaCommandFault"/>, command <c>DescribeGroups</c>, path the group
/// id): a drop closes with no bytes, an error rule's code is that row's <c>error_code</c> with every
/// other field empty. Then a caller who may not use the group is 30 the same way, whether or not the
/// group exists. A group with no member and no committed offset is error 0, state <c>Dead</c> and
/// empty strings; one known only by its committed offsets is state <c>Empty</c> with an empty
/// protocol type. Otherwise the row is the coordinator's description: state name, protocol type,
/// the chosen protocol name in <c>CompletingRebalance</c> and <c>Stable</c>, and each member in join
/// order with its client id, <c>client_host</c> <c>/&lt;address&gt;</c>, metadata for the chosen
/// protocol and, in <c>Stable</c>, its assignment.
/// </para>
/// <para>
/// v3 and v4 read <c>include_authorized_operations</c> and write <c>authorized_operations</c>:
/// -2147483648 unless asked, then READ (3) and DESCRIBE (8) set, 264. v4 writes
/// <c>group_instance_id</c>. v1 on start with <c>throttle_time_ms</c> 0.
/// </para>
/// </remarks>
/// <param name="coordinator">The broker's group coordinator.</param>
/// <param name="bus">The shared message bus, which answers the group access check and keeps the offsets.</param>
public sealed class KafkaDescribeGroupsAnswerer(KafkaGroupCoordinator coordinator, MessageBus.MessageBus bus) : IKafkaApiAnswerer
{
    /// <summary>The command a fault rule names to match DescribeGroups: the guide's API name.</summary>
    public const string FaultCommand = "DescribeGroups";

    /// <summary>The <c>authorized_operations</c> bit set a caller who asked for it is told: READ (3) and DESCRIBE (8).</summary>
    public const int AuthorizedOperations = (1 << 3) | (1 << 8);

    /// <summary>The <c>authorized_operations</c> value written when the request did not ask: <see cref="int.MinValue"/>.</summary>
    public const int AuthorizedOperationsNotAsked = int.MinValue;

    private static readonly KafkaGroupDescription Blank = new("", "", "", []);
    private static readonly KafkaGroupDescription Dead = new("Dead", "", "", []);
    private static readonly KafkaGroupDescription OffsetsOnly = new("Empty", "", "", []);

    private readonly KafkaGroupCoordinator coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    private readonly MessageBus.MessageBus bus = bus ?? throw new ArgumentNullException(nameof(bus));

    /// <inheritdoc/>
    public KafkaApiKey ApiKey => KafkaApiKey.DescribeGroups;

    /// <inheritdoc/>
    public async ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var version = request.Header.ApiVersion;
        var fields = request.Fields;
        var count = fields.ReadArrayLength().GetValueOrDefault();
        var asked = new List<string>();
        for (var index = 0; index < count; index++)
        {
            asked.Add(fields.ReadString());
        }

        var wantsOperations = version >= 3 && fields.ReadBoolean();
        var rows = new List<Row>();
        foreach (var group in asked)
        {
            var fault = await KafkaCommandFault.FindAsync(request, context, FaultCommand, group);
            if (fault.DropNote is { } note)
            {
                return KafkaAnswer.Close(note);
            }

            var (error, description) = Describe(group, fault.Error, request.Connection.AccountName);
            rows.Add(new Row(group, error, description));
        }

        return KafkaAnswer.Respond(Encode(version, wantsOperations, rows));
    }

    private (KafkaErrorCode Error, KafkaGroupDescription Description) Describe(string group, KafkaErrorCode? faultError, string? account)
    {
        var error = faultError ?? (!bus.MayUseGroup(account, group) ? KafkaErrorCode.GroupAuthorizationFailed : KafkaErrorCode.None);
        return error != KafkaErrorCode.None ? (error, Blank)
            : (KafkaErrorCode.None, coordinator.Describe(group) ?? (bus.GetCommittedOffsets(group).Count > 0 ? OffsetsOnly : Dead));
    }

    private static byte[] Encode(short version, bool wantsOperations, List<Row> rows)
    {
        var body = new KafkaPrimitiveWriter();
        if (version >= 1)
        {
            body.WriteInt32(0);
        }

        body.WriteArrayLength(rows.Count);
        foreach (var row in rows)
        {
            body.WriteInt16((short)row.Error);
            body.WriteString(row.Group);
            body.WriteString(row.Description.State);
            body.WriteString(row.Description.ProtocolType);
            body.WriteString(row.Description.ProtocolName);
            WriteMembers(body, version, row.Description.Members);
            if (version >= 3)
            {
                body.WriteInt32(wantsOperations ? AuthorizedOperations : AuthorizedOperationsNotAsked);
            }
        }

        return body.ToArray();
    }

    private static void WriteMembers(KafkaPrimitiveWriter body, short version, IReadOnlyList<KafkaGroupMemberDescription> members)
    {
        body.WriteArrayLength(members.Count);
        foreach (var member in members)
        {
            body.WriteString(member.MemberId);
            if (version >= 4)
            {
                body.WriteNullableString(member.InstanceId);
            }

            body.WriteString(member.ClientId);
            body.WriteString(member.ClientHost);
            body.WriteBytes(member.Metadata.Span);
            body.WriteBytes(member.Assignment.Span);
        }
    }

    // One group asked, and how it is answered.
    private sealed record Row(string Group, KafkaErrorCode Error, KafkaGroupDescription Description);
}
