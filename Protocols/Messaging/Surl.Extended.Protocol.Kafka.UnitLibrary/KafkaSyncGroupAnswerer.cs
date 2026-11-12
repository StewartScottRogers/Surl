using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers SyncGroup v0 to v3 through the broker's <see cref="KafkaGroupCoordinator"/> (ADR-0145
/// decisions 3, 6, 9 and 10; the Kafka protocol guide, SyncGroup).
/// </summary>
/// <remarks>
/// The request is first put to the exchange's fault plan (<see cref="KafkaCommandFault"/>, command
/// <c>SyncGroup</c>, path the group id): a drop closes with no bytes, an error rule's code is the
/// <c>error_code</c> and nothing changes. Then a caller who may not use the group is
/// <c>GROUP_AUTHORIZATION_FAILED</c> (30), and the coordinator makes the rest of the checks (25, 82,
/// 22, 27), relays the leader's assignments verbatim and holds a follower's answer until they
/// arrive, the exchange's idle clock paused meanwhile (<see cref="KafkaHeldAnswer"/>). v3 reads
/// <c>group_instance_id</c>; v1 to v3 start with <c>throttle_time_ms</c> 0. A refusal answers empty
/// assignment bytes.
/// </remarks>
/// <param name="coordinator">The broker's group coordinator.</param>
/// <param name="bus">The shared message bus, which answers the group access check.</param>
public sealed class KafkaSyncGroupAnswerer(KafkaGroupCoordinator coordinator, MessageBus.MessageBus bus) : IKafkaApiAnswerer
{
    /// <summary>The command a fault rule names to match SyncGroup: the guide's API name.</summary>
    public const string FaultCommand = "SyncGroup";

    private readonly KafkaGroupCoordinator coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    private readonly MessageBus.MessageBus bus = bus ?? throw new ArgumentNullException(nameof(bus));

    /// <inheritdoc/>
    public KafkaApiKey ApiKey => KafkaApiKey.SyncGroup;

    /// <inheritdoc/>
    public async ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var version = request.Header.ApiVersion;
        var fields = request.Fields;
        var group = fields.ReadString();
        var generationId = fields.ReadInt32();
        var memberId = fields.ReadString();
        var instanceId = version >= 3 ? fields.ReadNullableString() : null;
        var count = fields.ReadArrayLength().GetValueOrDefault();
        var assignments = new List<KeyValuePair<string, ReadOnlyMemory<byte>>>();
        for (var index = 0; index < count; index++)
        {
            assignments.Add(new(fields.ReadString(), fields.ReadBytes()));
        }

        var fault = await KafkaCommandFault.FindAsync(request, context, FaultCommand, group);
        if (fault.DropNote is { } note)
        {
            return KafkaAnswer.Close(note);
        }

        var refusal = fault.Error
            ?? (!bus.MayUseGroup(request.Connection.AccountName, group) ? KafkaErrorCode.GroupAuthorizationFailed : KafkaErrorCode.None);
        var result = refusal != KafkaErrorCode.None
            ? new KafkaSyncGroupResult(refusal, ReadOnlyMemory<byte>.Empty)
            : await KafkaHeldAnswer.WaitAsync(
                coordinator.SyncAsync(group, generationId, memberId, instanceId, assignments, KafkaGroupCaller.Of(request, context)),
                context);
        var body = new KafkaPrimitiveWriter();
        if (version >= 1)
        {
            body.WriteInt32(0);
        }

        body.WriteInt16((short)result.Error);
        body.WriteBytes(result.Assignment.Span);
        return KafkaAnswer.Respond(body.ToArray());
    }
}
