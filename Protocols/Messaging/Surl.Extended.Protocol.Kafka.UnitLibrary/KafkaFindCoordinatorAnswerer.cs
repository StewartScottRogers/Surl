using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers FindCoordinator v0 to v2 with node 0, the one broker, as every group's coordinator
/// (ADR-0145 decisions 2, 6 and 10; the Kafka protocol guide, FindCoordinator).
/// </summary>
/// <remarks>
/// The request is first put to the exchange's fault plan (<see cref="KafkaCommandFault"/>, command
/// <c>FindCoordinator</c>, path the key): a drop closes with no bytes, an error rule's code is the
/// <c>error_code</c>. Then a <c>key_type</c> other than 0 (v1 and v2; v0 has none and means a group)
/// is <c>INVALID_REQUEST</c> (42), and a caller who may not use the group
/// <c>GROUP_AUTHORIZATION_FAILED</c> (30); a refusal names node -1, host empty and port -1. Otherwise
/// node 0 is answered at the host and port Metadata advertises. v1 and v2 add
/// <c>throttle_time_ms</c> 0 and a null <c>error_message</c>.
/// </remarks>
/// <param name="bus">The shared message bus, which answers the group access check.</param>
public sealed class KafkaFindCoordinatorAnswerer(MessageBus.MessageBus bus) : IKafkaApiAnswerer
{
    /// <summary>The command a fault rule names to match FindCoordinator: the guide's API name.</summary>
    public const string FaultCommand = "FindCoordinator";

    private const sbyte GroupKeyType = 0;

    private readonly MessageBus.MessageBus bus = bus ?? throw new ArgumentNullException(nameof(bus));

    /// <inheritdoc/>
    public KafkaApiKey ApiKey => KafkaApiKey.FindCoordinator;

    /// <inheritdoc/>
    public async ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var version = request.Header.ApiVersion;
        var key = request.Fields.ReadString();
        var keyType = version >= 1 ? request.Fields.ReadInt8() : GroupKeyType;
        var fault = await KafkaCommandFault.FindAsync(request, context, FaultCommand, key);
        if (fault.DropNote is { } note)
        {
            return KafkaAnswer.Close(note);
        }

        var error = fault.Error
            ?? (keyType != GroupKeyType ? KafkaErrorCode.InvalidRequest
            : !bus.MayUseGroup(request.Connection.AccountName, key) ? KafkaErrorCode.GroupAuthorizationFailed
            : KafkaErrorCode.None);
        return KafkaAnswer.Respond(Encode(version, error, context));
    }

    private static byte[] Encode(short version, KafkaErrorCode error, ExchangeContext context)
    {
        var body = new KafkaPrimitiveWriter();
        if (version >= 1)
        {
            body.WriteInt32(0);
        }

        body.WriteInt16((short)error);
        if (version >= 1)
        {
            body.WriteNullableString(null);
        }

        var isFound = error == KafkaErrorCode.None;
        body.WriteInt32(isFound ? 0 : -1);
        body.WriteString(isFound ? KafkaMetadataAnswerer.AdvertisedHost(context) : "");
        body.WriteInt32(isFound ? context.ListenUrl.BoundPort ?? context.ListenUrl.Port : -1);
        return body.ToArray();
    }
}
