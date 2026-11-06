using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers ListOffsets v1 to v5 from the shared message bus's topic logs (ADR-0105 decision 3;
/// the Kafka protocol guide, ListOffsets).
/// </summary>
/// <remarks>
/// <para>
/// <b>Each partition</b>, in the order named: a topic name outside Kafka's rule is
/// <c>INVALID_TOPIC_EXCEPTION</c> (17); a partition other than 0 <c>UNKNOWN_TOPIC_OR_PARTITION</c>
/// (3); a topic the caller may not subscribe to <c>TOPIC_AUTHORIZATION_FAILED</c> (29); a topic
/// with no log 3 (<see cref="KafkaFetchableTopic"/>). A failed partition answers <c>timestamp</c> and
/// <c>offset</c> -1, and <c>leader_epoch</c> (v4) -1.
/// </para>
/// <para>
/// <b>The timestamp asked</b>: -2 (earliest, and any value below it) is the log start offset,
/// -1 (latest) the log end offset, both answered with <c>timestamp</c> -1; any other is the first
/// record published at or after it, answered with that record's publish time, or offset -1 and
/// timestamp -1 when there is none. <c>leader_epoch</c> (v4) is 0, <c>throttle_time_ms</c> (v2)
/// is 0, and <c>isolation_level</c> (v2) and <c>current_leader_epoch</c> (v4) change nothing.
/// </para>
/// </remarks>
/// <param name="bus">The shared message bus whose topic logs are read.</param>
public sealed class KafkaListOffsetsAnswerer(MessageBus.MessageBus bus) : IKafkaApiAnswerer
{
    /// <summary>The timestamp that asks for the log start offset (earliest): -2.</summary>
    public const long EarliestTimestamp = -2;

    /// <summary>The timestamp that asks for the log end offset (latest): -1.</summary>
    public const long LatestTimestamp = -1;

    private const long None = -1;

    private readonly MessageBus.MessageBus bus = bus ?? throw new ArgumentNullException(nameof(bus));

    /// <inheritdoc/>
    public KafkaApiKey ApiKey => KafkaApiKey.ListOffsets;

    /// <inheritdoc/>
    public ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var version = request.Header.ApiVersion;
        var fields = request.Fields;
        fields.ReadInt32();
        if (version >= 2)
        {
            fields.ReadInt8();
        }

        var account = request.Connection.AccountName;
        var body = new KafkaPrimitiveWriter();
        if (version >= 2)
        {
            body.WriteInt32(0);
        }

        var topicCount = fields.ReadArrayLength().GetValueOrDefault();
        body.WriteArrayLength(topicCount);
        for (var topicIndex = 0; topicIndex < topicCount; topicIndex++)
        {
            AnswerTopic(fields, body, version, account, context.Scheme);
        }

        return ValueTask.FromResult(KafkaAnswer.Respond(body.ToArray()));
    }

    private void AnswerTopic(KafkaPrimitiveReader fields, KafkaPrimitiveWriter body, short version, string? account, string scheme)
    {
        var name = fields.ReadString();
        body.WriteString(name);
        var partitionCount = fields.ReadArrayLength().GetValueOrDefault();
        body.WriteArrayLength(partitionCount);
        for (var partitionIndex = 0; partitionIndex < partitionCount; partitionIndex++)
        {
            var index = fields.ReadInt32();
            if (version >= 4)
            {
                fields.ReadInt32();
            }

            var timestamp = fields.ReadInt64();
            var (topic, error) = KafkaFetchableTopic.Find(bus, name, index, account, scheme);
            var (offset, foundAt) = topic is null ? (None, None) : Lookup(topic, timestamp);
            body.WriteInt32(index);
            body.WriteInt16((short)error);
            body.WriteInt64(foundAt);
            body.WriteInt64(offset);
            if (version >= 4)
            {
                body.WriteInt32(topic is null ? -1 : 0);
            }
        }
    }

    // The offset a timestamp names, and the timestamp answered with it.
    private (long Offset, long Timestamp) Lookup(string topic, long timestamp)
    {
        var offsets = bus.GetLogOffsets(topic);
        if (timestamp <= EarliestTimestamp)
        {
            return (offsets.LogStartOffset, None);
        }

        if (timestamp == LatestTimestamp)
        {
            return (offsets.HighWatermark, None);
        }

        var found = bus.ReadLog(topic, offsets.LogStartOffset, long.MaxValue).Records
            .FirstOrDefault(record => record.Message.PublishedAt.ToUnixTimeMilliseconds() >= timestamp);
        return found is null ? (None, None) : (found.Offset, found.Message.PublishedAt.ToUnixTimeMilliseconds());
    }
}
