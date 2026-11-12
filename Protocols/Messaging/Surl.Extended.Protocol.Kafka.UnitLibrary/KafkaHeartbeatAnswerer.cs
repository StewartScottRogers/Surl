using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers Heartbeat v0 to v3 through the broker's <see cref="KafkaGroupCoordinator"/> (ADR-0145
/// decisions 3, 6 and 10; the Kafka protocol guide, Heartbeat).
/// </summary>
/// <remarks>
/// The request is first put to the exchange's fault plan (<see cref="KafkaCommandFault"/>, command
/// <c>Heartbeat</c>, path the group id): a drop closes with no bytes, an error rule's code is the
/// <c>error_code</c> and nothing changes, the member's session not refreshed. Then a caller who may
/// not use the group is <c>GROUP_AUTHORIZATION_FAILED</c> (30), and the coordinator answers the rest
/// (25, 82, 22, 27 or 0). v3 reads <c>group_instance_id</c>; v1 to v3 start with
/// <c>throttle_time_ms</c> 0.
/// </remarks>
/// <param name="coordinator">The broker's group coordinator.</param>
/// <param name="bus">The shared message bus, which answers the group access check.</param>
public sealed class KafkaHeartbeatAnswerer(KafkaGroupCoordinator coordinator, MessageBus.MessageBus bus) : IKafkaApiAnswerer
{
    /// <summary>The command a fault rule names to match Heartbeat: the guide's API name.</summary>
    public const string FaultCommand = "Heartbeat";

    private readonly KafkaGroupCoordinator coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    private readonly MessageBus.MessageBus bus = bus ?? throw new ArgumentNullException(nameof(bus));

    /// <inheritdoc/>
    public KafkaApiKey ApiKey => KafkaApiKey.Heartbeat;

    /// <inheritdoc/>
    public async ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var version = request.Header.ApiVersion;
        var group = request.Fields.ReadString();
        var generationId = request.Fields.ReadInt32();
        var memberId = request.Fields.ReadString();
        var instanceId = version >= 3 ? request.Fields.ReadNullableString() : null;
        var fault = await KafkaCommandFault.FindAsync(request, context, FaultCommand, group);
        if (fault.DropNote is { } note)
        {
            return KafkaAnswer.Close(note);
        }

        var error = fault.Error
            ?? (!bus.MayUseGroup(request.Connection.AccountName, group) ? KafkaErrorCode.GroupAuthorizationFailed
            : coordinator.Heartbeat(group, generationId, memberId, instanceId));
        var body = new KafkaPrimitiveWriter();
        if (version >= 1)
        {
            body.WriteInt32(0);
        }

        body.WriteInt16((short)error);
        return KafkaAnswer.Respond(body.ToArray());
    }
}
