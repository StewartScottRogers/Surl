using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers Produce v3 to v8 by appending each partition's records to the shared message bus's
/// topic log (ADR-0105 decisions 3, 4, 6 and 7; the Kafka protocol guide, Produce).
/// </summary>
/// <remarks>
/// <para>
/// <b>The request.</b> <c>acks</c> other than 0, 1 and -1 is <c>INVALID_REQUIRED_ACKS</c> (21)
/// for every partition, and a non-null <c>transactional_id</c>
/// <c>UNSUPPORTED_FOR_MESSAGE_FORMAT</c> (43); either way nothing is appended. <c>timeout_ms</c>
/// is read and ignored: there is no replica to wait for.
/// </para>
/// <para>
/// <b>Each partition</b>, in the order named: a topic name outside Kafka's rule is
/// <c>INVALID_TOPIC_EXCEPTION</c> (17); a partition other than 0 <c>UNKNOWN_TOPIC_OR_PARTITION</c>
/// (3); a topic the caller may not publish to <c>TOPIC_AUTHORIZATION_FAILED</c> (29); a batch
/// failing <see cref="KafkaRecordBatchReader"/>'s checks its error - a compressed batch is
/// decompressed first, within <see cref="KafkaRecordsCompression.DecompressionBound"/> of
/// <see cref="ExchangeLimits.MaxMessageBytes"/> (<c>MESSAGE_TOO_LARGE</c> (10) past it) - and
/// each of its records becomes one bus message; a record whose key and value
/// pass the bus's <c>MaxMessageBytes</c> (<c>--max-filesize</c>) <c>MESSAGE_TOO_LARGE</c> (10);
/// an idempotent producer's sequence or epoch refused by <see cref="KafkaProducerStates"/>
/// <c>OUT_OF_ORDER_SEQUENCE_NUMBER</c> (45) or <c>INVALID_PRODUCER_EPOCH</c> (47), and a retry of
/// batches already taken answered 0 with the first one's original <c>base_offset</c>,
/// <c>log_append_time_ms</c> -1 and nothing appended (ADR-0145 decision 8); a bus store bound <c>POLICY_VIOLATION</c> (44). A topic with no log is created. Otherwise each
/// record is published on the bus as one message - the value as payload (null as empty), the
/// key, the UTF-8 headers, quality of service 1 - so live subscribers of the mapped topic
/// receive it, and the partition is answered with the first record's offset as
/// <c>base_offset</c>, the bus's publish time as <c>log_append_time_ms</c> and the log's start
/// offset as <c>log_start_offset</c> (v5). A failed partition carries -1 in all three.
/// <c>record_errors</c> (v8) is empty and <c>error_message</c> (v8) null; <c>throttle_time_ms</c> is 0.
/// </para>
/// <para>
/// <b><c>acks</c> 0</b> writes no response; a partition's error is then only noted in the
/// exchange log.
/// </para>
/// </remarks>
/// <param name="bus">The shared message bus whose topic logs the records are appended to.</param>
/// <param name="producerStates">The broker's idempotent producers, shared with its
/// <see cref="KafkaInitProducerIdAnswerer"/>; a new, empty set unless given.</param>
public sealed class KafkaProduceAnswerer(MessageBus.MessageBus bus, KafkaProducerStates? producerStates = null) : IKafkaApiAnswerer
{
    private const long NoOffset = -1;

    private readonly MessageBus.MessageBus bus = bus ?? throw new ArgumentNullException(nameof(bus));
    private readonly KafkaProducerStates producerStates = producerStates ?? new KafkaProducerStates();

    /// <inheritdoc/>
    public KafkaApiKey ApiKey => KafkaApiKey.Produce;

    /// <inheritdoc/>
    public async ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var produce = ProduceRequest.Read(request.Fields);
        var requestError = produce.Acks is not (0 or 1 or -1) ? KafkaErrorCode.InvalidRequiredAcks
            : produce.TransactionalId is not null ? KafkaErrorCode.UnsupportedForMessageFormat
            : KafkaErrorCode.None;
        var account = request.Connection.AccountName;

        // A request refused as a whole asks the fault plan nothing (ADR-0124 decision 9).
        var chosen = requestError == KafkaErrorCode.None
            ? await KafkaChosenErrorFault.FindAsync(ApiKey, produce.Topics.Select(topic => topic.Name), account, context)
            : [];
        var answered = produce.Topics.Select(topic => new TopicAnswer(
            topic.Name,
            [.. topic.Partitions.Select(partition => AnswerPartition(topic.Name, partition, requestError, chosen, account, context))])).ToList();

        if (produce.Acks == 0)
        {
            NoteErrors(answered, context);
            return KafkaAnswer.NoResponse;
        }

        return KafkaAnswer.Respond(Encode(answered, request.Header.ApiVersion));
    }

    // The request's own error first, then a chosen-error rule's code, else the append.
    private PartitionAnswer AnswerPartition(
        string name, PartitionData partition, KafkaErrorCode requestError, Dictionary<string, KafkaErrorCode> chosen, string? account, ExchangeContext context) =>
        requestError != KafkaErrorCode.None ? PartitionAnswer.Failed(partition.Index, requestError)
        : chosen.TryGetValue(name, out var chosenError) ? PartitionAnswer.Failed(partition.Index, chosenError)
        : Append(name, partition, account, context);

    private PartitionAnswer Append(string name, PartitionData partition, string? account, ExchangeContext context)
    {
        var (topic, topicError) = PublishableTopic(name, partition.Index, account, context.Scheme);
        if (topic is null)
        {
            return PartitionAnswer.Failed(partition.Index, topicError);
        }

        var batches = KafkaRecordBatchReader.Read(
            partition.Records ?? ReadOnlyMemory<byte>.Empty,
            KafkaRecordsCompression.DecompressionBound(context.Limits.MaxMessageBytes));
        var error = RecordsError(batches);
        if (error != KafkaErrorCode.None)
        {
            return PartitionAnswer.Failed(partition.Index, error);
        }

        var sequence = producerStates.Check(topic, batches.Batches);
        return sequence.Error != KafkaErrorCode.None ? PartitionAnswer.Failed(partition.Index, sequence.Error)
            : sequence.DuplicateBaseOffset is { } original ? Duplicate(topic, partition.Index, original)
            : bus.CreateLog(topic) != MessageBusOutcome.Succeeded ? PartitionAnswer.Failed(partition.Index, KafkaErrorCode.PolicyViolation)
            : Publish(topic, partition.Index, batches, account, context);
    }

    // The canonical topic a partition's records go to, or why they may not: an invalid name,
    // a partition other than 0, or a topic the caller may not publish to.
    private (string? Topic, KafkaErrorCode Error) PublishableTopic(string name, int index, string? account, string scheme)
    {
        var mapping = bus.Names.ToCanonicalTopic(scheme, name);
        return !mapping.IsMapped ? (null, KafkaErrorCode.InvalidTopicException)
            : index != KafkaTopicNames.OnlyPartition ? (null, KafkaErrorCode.UnknownTopicOrPartition)
            : !bus.GetTopicAccess(account, mapping.Name!).HasFlag(TopicAccessRights.Publish) ? (null, KafkaErrorCode.TopicAuthorizationFailed)
            : (mapping.Name, KafkaErrorCode.None);
    }

    // A batch that failed its checks, or a record past the bus's message limit.
    private KafkaErrorCode RecordsError(KafkaRecordBatchReadResult batches) =>
        batches.Error != KafkaErrorCode.None ? batches.Error
        : batches.Records.Any(record => record.KeyAndValueBytes > bus.MaxMessageBytes) ? KafkaErrorCode.MessageTooLarge
        : KafkaErrorCode.None;

    // A retried batch already appended: its original base offset, no append time (Kafka answers
    // a duplicate with -1 there), and the log's start offset now.
    private PartitionAnswer Duplicate(string topic, int index, long baseOffset) =>
        new(index, KafkaErrorCode.None, baseOffset, NoOffset, bus.GetLogOffsets(topic).LogStartOffset);

    // Publishes the partition's records as one all-or-nothing bus batch (ADR-0105 decision 4); a
    // store bound met by any of them is POLICY_VIOLATION and none is appended.
    private PartitionAnswer Publish(string topic, int index, KafkaRecordBatchReadResult batches, string? account, ExchangeContext context)
    {
        using var connection = bus.Connect(account);
        var entries = batches.Records.Select(record => BusBatchEntry.ToTopic(ToBusMessage(topic, record))).ToList();
        var published = connection.PublishBatch(entries);
        if (published.Outcome != MessageBusOutcome.Succeeded)
        {
            return PartitionAnswer.Failed(index, KafkaErrorCode.PolicyViolation);
        }

        var offsets = published.Results.Select(result => result.LogOffset.GetValueOrDefault()).ToList();

        producerStates.Record(topic, batches.Batches, BatchBaseOffsets(batches.Batches, offsets));

        // Retention always keeps the newest record, so its publish time is read back from the
        // log; the exchange's clock stands in should a later publish have dropped it already.
        var appendedAt = bus.ReadLog(topic, offsets[^1], 0).Records
            .Select(record => record.Message.PublishedAt)
            .DefaultIfEmpty(context.TimeProvider.GetUtcNow())
            .First();
        return new(index, KafkaErrorCode.None, offsets[0], appendedAt.ToUnixTimeMilliseconds(), bus.GetLogOffsets(topic).LogStartOffset);
    }

    // The offset of each batch's first record: the batches' records were published in order.
    private static List<long> BatchBaseOffsets(IReadOnlyList<KafkaProducedBatch> batches, List<long> offsets)
    {
        var baseOffsets = new List<long>(batches.Count);
        var first = 0;
        foreach (var batch in batches)
        {
            baseOffsets.Add(offsets[first]);
            first += batch.RecordCount;
        }

        return baseOffsets;
    }

    private static BusMessage ToBusMessage(string topic, KafkaProducedRecord record) =>
        new(topic, record.Value ?? ReadOnlyMemory<byte>.Empty)
        {
            Key = record.Key,
            Headers = record.Headers,
            QualityOfService = BusQualityOfService.AtLeastOnce,
        };

    private static void NoteErrors(List<TopicAnswer> answered, ExchangeContext context)
    {
        foreach (var topic in answered)
        {
            foreach (var partition in topic.Partitions.Where(partition => partition.Error != KafkaErrorCode.None))
            {
                context.Log.Note($"Produce with acks 0: topic {topic.Name} partition {partition.Index} was refused with error {(short)partition.Error} ({partition.Error}); no response is written.");
            }
        }
    }

    private static byte[] Encode(List<TopicAnswer> answered, short version)
    {
        var body = new KafkaPrimitiveWriter();
        body.WriteArrayLength(answered.Count);
        foreach (var topic in answered)
        {
            body.WriteString(topic.Name);
            body.WriteArrayLength(topic.Partitions.Count);
            foreach (var partition in topic.Partitions)
            {
                WritePartition(body, partition, version);
            }
        }

        body.WriteInt32(0);
        return body.ToArray();
    }

    private static void WritePartition(KafkaPrimitiveWriter body, PartitionAnswer partition, short version)
    {
        body.WriteInt32(partition.Index);
        body.WriteInt16((short)partition.Error);
        body.WriteInt64(partition.BaseOffset);
        body.WriteInt64(partition.LogAppendTimeMilliseconds);
        if (version >= 5)
        {
            body.WriteInt64(partition.LogStartOffset);
        }

        if (version >= 8)
        {
            body.WriteArrayLength(0);
            body.WriteNullableString(null);
        }
    }

    // One topic of the response, its partitions in the order the request named them.
    private sealed record TopicAnswer(string Name, IReadOnlyList<PartitionAnswer> Partitions);

    // One partition of the response.
    private sealed record PartitionAnswer(int Index, KafkaErrorCode Error, long BaseOffset, long LogAppendTimeMilliseconds, long LogStartOffset)
    {
        public static PartitionAnswer Failed(int index, KafkaErrorCode error) => new(index, error, NoOffset, NoOffset, NoOffset);
    }

    // One partition of the request: its index and its records, which may be null.
    private sealed record PartitionData(int Index, ReadOnlyMemory<byte>? Records);

    // One topic of the request.
    private sealed record TopicData(string Name, IReadOnlyList<PartitionData> Partitions);

    // A Produce v3 to v8 request's fields; the four versions read alike.
    private sealed record ProduceRequest(string? TransactionalId, short Acks, IReadOnlyList<TopicData> Topics)
    {
        public static ProduceRequest Read(KafkaPrimitiveReader fields)
        {
            var transactionalId = fields.ReadNullableString();
            var acks = fields.ReadInt16();
            fields.ReadInt32();
            var topics = ReadArray(fields, () => new TopicData(fields.ReadString(), ReadArray(fields, () => new PartitionData(fields.ReadInt32(), fields.ReadNullableBytes()))));
            return new(transactionalId, acks, topics);
        }

        // An ARRAY, null read as empty.
        private static List<T> ReadArray<T>(KafkaPrimitiveReader fields, Func<T> readElement)
        {
            var items = new List<T>();
            var count = fields.ReadArrayLength().GetValueOrDefault();
            for (var index = 0; index < count; index++)
            {
                items.Add(readElement());
            }

            return items;
        }
    }
}
