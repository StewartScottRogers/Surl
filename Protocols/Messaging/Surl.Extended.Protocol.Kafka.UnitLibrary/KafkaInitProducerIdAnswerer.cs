using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers InitProducerId v0 and v1 by handing an idempotent producer a new producer id and epoch 0
/// (ADR-0145 decisions 8 and 10; the Kafka protocol guide, InitProducerId).
/// </summary>
/// <remarks>
/// <para>
/// The request is first put to the exchange's fault plan at <see cref="FaultStage.Command"/>,
/// command <c>InitProducerId</c>, no path, account the caller's (<see cref="CommandStageFaults"/>): a
/// delay holds the answer, a drop aborts the connection with no answer, and an
/// <see cref="FaultKind.Error"/> rule whose reply is a Kafka error code 1 to 127 is the top-level
/// <c>error_code</c>, with <c>producer_id</c> and <c>producer_epoch</c> -1 and no id drawn; a reply
/// outside that is noted and not applied.
/// </para>
/// <para>
/// A null <c>transactional_id</c> is answered error 0, the next of
/// <see cref="KafkaProducerStates.NextProducerId"/> and epoch 0. A non-null one, empty included, is
/// <c>INVALID_REQUEST</c> (42) with <c>producer_id</c> and <c>producer_epoch</c> -1: there are no
/// transactions. <c>transaction_timeout_ms</c> is read and ignored; <c>throttle_time_ms</c> is 0.
/// Both versions read and answer alike.
/// </para>
/// </remarks>
/// <param name="producerStates">The broker's producers, shared with its <see cref="KafkaProduceAnswerer"/>.</param>
public sealed class KafkaInitProducerIdAnswerer(KafkaProducerStates producerStates) : IKafkaApiAnswerer
{
    /// <summary>The command a fault rule names to match InitProducerId: the guide's API name.</summary>
    public const string FaultCommand = "InitProducerId";

    private const long NoProducerId = -1;
    private const short NoEpoch = -1;

    private readonly KafkaProducerStates producerStates = producerStates ?? throw new ArgumentNullException(nameof(producerStates));

    /// <inheritdoc/>
    public KafkaApiKey ApiKey => KafkaApiKey.InitProducerId;

    /// <inheritdoc/>
    public async ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var transactionalId = request.Fields.ReadNullableString();
        request.Fields.ReadInt32();

        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = request.Connection.AccountName,
            Command = FaultCommand,
        };
        var rule = await CommandStageFaults.FindAndApplyAsync(context, request.Transport, point, FaultCommand);
        if (rule is { Kind: FaultKind.Drop })
        {
            return KafkaAnswer.Close($"InitProducerId was dropped by fault rule {rule.Id}");
        }

        var chosen = ChosenError(rule, context);
        return KafkaAnswer.Respond(
            chosen is { } error ? Encode(error, NoProducerId, NoEpoch)
            : transactionalId is not null ? Encode(KafkaErrorCode.InvalidRequest, NoProducerId, NoEpoch)
            : Encode(KafkaErrorCode.None, producerStates.NextProducerId(context.Identifiers), 0));
    }

    // An Error rule's code, when its reply is a Kafka error code 1 to 127 (ADR-0124 decision 4's
    // kafka row); null to answer as usual.
    private static KafkaErrorCode? ChosenError(FaultRule? rule, ExchangeContext context)
    {
        if (rule is not { Kind: FaultKind.Error })
        {
            return null;
        }

        if (!int.TryParse(rule.Reply, NumberStyles.None, CultureInfo.InvariantCulture, out var code) || code is < 1 or > 127)
        {
            context.Log.Note($"Fault rule {rule.Id} not applied to InitProducerId: its reply is not a Kafka error code 1 to 127.");
            return null;
        }

        context.Log.Note($"InitProducerId answered error {code} by fault rule {rule.Id}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, FaultCommand));
        return (KafkaErrorCode)code;
    }

    // throttle_time_ms 0, error_code, producer_id, producer_epoch.
    private static byte[] Encode(KafkaErrorCode error, long producerId, short epoch)
    {
        var body = new KafkaPrimitiveWriter();
        body.WriteInt32(0);
        body.WriteInt16((short)error);
        body.WriteInt64(producerId);
        body.WriteInt16(epoch);
        return body.ToArray();
    }
}
