using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers Fetch v4 to v11 from the shared message bus's topic logs (ADR-0105 decisions 1, 3, 4
/// and 6; the Kafka protocol guide, Fetch).
/// </summary>
/// <remarks>
/// <para>
/// <b>Each partition</b>, in the order named, passes <see cref="KafkaFetchableTopic"/>'s checks
/// (17, 3, 29), then its <c>fetch_offset</c>: below the log start offset or above the log end
/// offset is <c>OFFSET_OUT_OF_RANGE</c> (1); equal to the log end offset is an empty answer. A
/// failed partition answers <c>high_watermark</c>, <c>last_stable_offset</c> and
/// <c>log_start_offset</c> (v5) -1 and empty <c>records</c>. Otherwise <c>high_watermark</c> and
/// <c>last_stable_offset</c> are the log end offset, <c>log_start_offset</c> (v5) the log's start,
/// <c>aborted_transactions</c> empty, <c>preferred_read_replica</c> (v11) -1, and <c>records</c>
/// the batches <see cref="KafkaRecordBatchWriter"/> builds, compressed with the served codec, from <c>fetch_offset</c> on, within the
/// partition's <c>partition_max_bytes</c> and what is left of the request's <c>max_bytes</c>
/// (itself capped at <see cref="ExchangeLimits.MaxMessageBytes"/>); the first record of the
/// response is returned whatever it costs, so a consumer always makes progress (KIP-74).
/// </para>
/// <para>
/// <b>Waiting.</b> When no partition failed and the records come to fewer than <c>min_bytes</c>,
/// the answer waits until they reach it or <c>max_wait_ms</c> (capped at
/// <see cref="MaxWaitMilliseconds"/>) has passed on <see cref="ExchangeContext.TimeProvider"/>,
/// whichever is first, waking on the bus's deliveries to the request's topics, then answers with
/// what is there.
/// </para>
/// <para>
/// <b>Fetch sessions</b> (v7 on) are not kept: <c>session_id</c> is answered 0, and a request
/// continuing a session (<c>session_id</c> above 0) is answered with top-level error
/// <c>FETCH_SESSION_ID_NOT_FOUND</c> (70) and no topics. <c>replica_id</c>,
/// <c>isolation_level</c>, <c>current_leader_epoch</c> (v9), the partitions'
/// <c>log_start_offset</c> (v5), <c>forgotten_topics_data</c> (v7) and <c>rack_id</c> (v11) change
/// nothing. <c>throttle_time_ms</c> is 0.
/// </para>
/// </remarks>
/// <param name="bus">The shared message bus whose topic logs are read.</param>
/// <param name="servedCompression">
/// The codec of every batch served (<c>--kafka-fetch-compression</c>, ADR-0105 decision 4);
/// <see cref="KafkaCompressionCodec.None"/> unless given.
/// </param>
public sealed class KafkaFetchAnswerer(MessageBus.MessageBus bus, KafkaCompressionCodec servedCompression = KafkaCompressionCodec.None) : IKafkaApiAnswerer
{
    /// <summary>The longest a Fetch waits for <c>min_bytes</c>: 60000 ms, half of the default
    /// <c>--idle-timeout</c> (ADR-0105 decision 6).</summary>
    public const int MaxWaitMilliseconds = 60_000;

    private const long NoOffset = -1;

    private readonly MessageBus.MessageBus bus = bus ?? throw new ArgumentNullException(nameof(bus));
    private readonly KafkaCompressionCodec servedCompression = KafkaRecordBatchWriter.RequireCodec(servedCompression);

    /// <inheritdoc/>
    public KafkaApiKey ApiKey => KafkaApiKey.Fetch;

    /// <inheritdoc/>
    public async ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var version = request.Header.ApiVersion;
        var fetch = FetchRequest.Read(request.Fields, version);
        if (fetch.SessionId > 0)
        {
            return KafkaAnswer.Respond(Encode([], version, KafkaErrorCode.FetchSessionIdNotFound));
        }

        var maxBytes = context.Limits.MaxMessageBytes > 0 ? Math.Min(fetch.MaxBytes, context.Limits.MaxMessageBytes) : fetch.MaxBytes;
        var chosen = await KafkaChosenErrorFault.FindAsync(ApiKey, fetch.Topics.Select(topic => topic.Name), request.Connection.AccountName, context);
        var read = new FetchRead(bus, fetch, maxBytes, servedCompression, request.Connection.AccountName, context.Scheme, chosen);
        var answered = read.Answer();
        var wait = Math.Min(fetch.MaxWaitMilliseconds, MaxWaitMilliseconds);
        if (wait > 0 && IsShort(answered, fetch.MinBytes))
        {
            answered = await WaitForMinBytesAsync(read, fetch.MinBytes, TimeSpan.FromMilliseconds(wait), request.Connection.AccountName, context);
        }

        return KafkaAnswer.Respond(Encode(answered, version, KafkaErrorCode.None));
    }

    // Whether an answer with no failed partition holds fewer than minBytes bytes of records.
    private static bool IsShort(List<TopicAnswer> answered, int minBytes)
    {
        var partitions = answered.SelectMany(topic => topic.Partitions).ToList();
        return partitions.All(partition => partition.Error == KafkaErrorCode.None)
            && partitions.Sum(partition => (long)partition.Records.Length) < minBytes;
    }

    // Reads again each time the bus delivers a message on one of the request's topics, until the
    // records reach minBytes or the wait is over. The topics are subscribed to before the read
    // that follows, so no message published in between is missed.
    private async Task<List<TopicAnswer>> WaitForMinBytesAsync(FetchRead read, int minBytes, TimeSpan wait, string? account, ExchangeContext context)
    {
        using var deadline = new CancellationTokenSource(wait, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, deadline.Token);
        using var listener = bus.Connect(account);
        var appended = NewSignal();
        foreach (var topic in read.Topics)
        {
            listener.Subscribe(topic, _ => appended.TrySetResult());
        }

        // The read after the wait ends takes in what a delivery that raced the deadline appended.
        var isWaitOver = false;
        while (true)
        {
            var signal = appended = NewSignal();
            var answered = read.Answer();
            if (isWaitOver || !IsShort(answered, minBytes))
            {
                return answered;
            }

            isWaitOver = !await WaitAsync(signal.Task, cancellation.Token, context.CancellationToken);
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    // True when signalled; false when the wait is over. The exchange's own cancellation throws.
    private static async Task<bool> WaitAsync(Task signal, CancellationToken cancellation, CancellationToken exchangeCancellation)
    {
        try
        {
            await signal.WaitAsync(cancellation);
            return true;
        }
        catch (OperationCanceledException) when (!exchangeCancellation.IsCancellationRequested)
        {
            return false;
        }
    }

    private static byte[] Encode(List<TopicAnswer> answered, short version, KafkaErrorCode error)
    {
        var body = new KafkaPrimitiveWriter();
        body.WriteInt32(0);
        if (version >= 7)
        {
            body.WriteInt16((short)error);
            body.WriteInt32(0);
        }

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

        return body.ToArray();
    }

    private static void WritePartition(KafkaPrimitiveWriter body, PartitionAnswer partition, short version)
    {
        body.WriteInt32(partition.Index);
        body.WriteInt16((short)partition.Error);
        body.WriteInt64(partition.HighWatermark);
        body.WriteInt64(partition.HighWatermark);
        if (version >= 5)
        {
            body.WriteInt64(partition.LogStartOffset);
        }

        body.WriteArrayLength(0);
        if (version >= 11)
        {
            body.WriteInt32(-1);
        }

        body.WriteBytes(partition.Records);
    }

    // One topic of the response, its partitions in the order the request named them.
    private sealed record TopicAnswer(string Name, IReadOnlyList<PartitionAnswer> Partitions);

    // One partition of the response.
    private sealed record PartitionAnswer(int Index, KafkaErrorCode Error, long HighWatermark, long LogStartOffset, byte[] Records)
    {
        public static PartitionAnswer Failed(int index, KafkaErrorCode error) => new(index, error, NoOffset, NoOffset, []);
    }

    // One partition of the request.
    private sealed record PartitionData(int Index, long FetchOffset, int PartitionMaxBytes);

    // One topic of the request.
    private sealed record TopicData(string Name, IReadOnlyList<PartitionData> Partitions);

    // The answer to one Fetch, read from the bus as often as the wait needs.
    private sealed class FetchRead(
        MessageBus.MessageBus bus, FetchRequest fetch, long maxBytes, KafkaCompressionCodec codec, string? account, string scheme, Dictionary<string, KafkaErrorCode> chosen)
    {
        // The canonical topics the request may read, which the wait listens on.
        public IEnumerable<string> Topics => fetch.Topics
            .SelectMany(topic => topic.Partitions.Select(partition => KafkaFetchableTopic.Find(bus, topic.Name, partition.Index, account, scheme).Topic))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal);

        public List<TopicAnswer> Answer()
        {
            var remaining = maxBytes;
            var isAnyRecordTaken = false;
            var answered = new List<TopicAnswer>(fetch.Topics.Count);
            foreach (var topic in fetch.Topics)
            {
                var partitions = new List<PartitionAnswer>(topic.Partitions.Count);
                foreach (var partition in topic.Partitions)
                {
                    var answer = AnswerPartition(topic.Name, partition, remaining, takeFirstWhole: !isAnyRecordTaken);
                    isAnyRecordTaken |= answer.Records.Length > 0;
                    remaining = Math.Max(0, remaining - answer.Records.Length);
                    partitions.Add(answer);
                }

                answered.Add(new(topic.Name, partitions));
            }

            return answered;
        }

        // takeFirstWhole holds until some partition of the response has returned records.
        private PartitionAnswer AnswerPartition(string name, PartitionData partition, long remaining, bool takeFirstWhole)
        {
            // A topic a chosen-error rule answers fails, so the answer does not wait (ADR-0124 decision 9).
            if (chosen.TryGetValue(name, out var chosenError))
            {
                return PartitionAnswer.Failed(partition.Index, chosenError);
            }

            var (topic, error) = KafkaFetchableTopic.Find(bus, name, partition.Index, account, scheme);
            if (topic is null)
            {
                return PartitionAnswer.Failed(partition.Index, error);
            }

            var limit = Math.Min(Math.Max(0, partition.PartitionMaxBytes), remaining);
            var log = bus.ReadLog(topic, partition.FetchOffset, limit);
            if (log.Outcome != MessageBusOutcome.Succeeded)
            {
                return PartitionAnswer.Failed(partition.Index, KafkaErrorCode.OffsetOutOfRange);
            }

            var records = KafkaRecordBatchWriter.Write(log.Records, limit, takeFirstWhole, codec);
            return new(partition.Index, KafkaErrorCode.None, log.HighWatermark, log.LogStartOffset, records);
        }
    }

    // A Fetch v4 to v11 request's fields, those that change the answer.
    private sealed record FetchRequest(int MaxWaitMilliseconds, int MinBytes, long MaxBytes, int SessionId, IReadOnlyList<TopicData> Topics)
    {
        public static FetchRequest Read(KafkaPrimitiveReader fields, short version)
        {
            fields.ReadInt32();
            var maxWait = fields.ReadInt32();
            var minBytes = fields.ReadInt32();
            var maxBytes = Math.Max(0, fields.ReadInt32());
            fields.ReadInt8();
            var sessionId = version >= 7 ? ReadSessionId(fields) : 0;
            var topics = ReadArray(fields, () => new TopicData(fields.ReadString(), ReadArray(fields, () => ReadPartition(fields, version))));
            return new(maxWait, minBytes, maxBytes, sessionId, topics);
        }

        // session_id, then session_epoch, which changes nothing.
        private static int ReadSessionId(KafkaPrimitiveReader fields)
        {
            var sessionId = fields.ReadInt32();
            fields.ReadInt32();
            return sessionId;
        }

        private static PartitionData ReadPartition(KafkaPrimitiveReader fields, short version)
        {
            var index = fields.ReadInt32();
            if (version >= 9)
            {
                fields.ReadInt32();
            }

            var fetchOffset = fields.ReadInt64();
            if (version >= 5)
            {
                fields.ReadInt64();
            }

            return new(index, fetchOffset, fields.ReadInt32());
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
