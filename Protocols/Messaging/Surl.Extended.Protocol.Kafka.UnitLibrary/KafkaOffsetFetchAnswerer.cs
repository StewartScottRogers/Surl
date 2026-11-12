using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers OffsetFetch v1 to v5 from the offsets <c>Surl.MessageBus</c> keeps (ADR-0145 decisions 4,
/// 6 and 10; the Kafka protocol guide, OffsetFetch).
/// </summary>
/// <remarks>
/// <para>
/// The request is first put to the exchange's fault plan (<see cref="KafkaCommandFault"/>, command
/// <c>OffsetFetch</c>, path the group id): a drop closes with no bytes, an error rule's code is the
/// top-level <c>error_code</c> (v2 to v5) with no topics, or every asked partition's error (v1). A
/// caller who may not use the group is answered 30 the same way.
/// </para>
/// <para>
/// Each partition asked is answered, in order: 17 for a name outside Kafka's rule, 29 without
/// subscribe access, 3 for a partition other than 0; otherwise its committed offset, leader epoch
/// (v5; -1 before) and metadata, or offset -1, leader epoch -1 and empty metadata when none is
/// committed or the topic does not exist, with error 0. A null <c>topics</c> array lists every topic
/// the group committed that the caller may subscribe to and Kafka can name, in ordinal order of
/// Kafka name. v3 on start with <c>throttle_time_ms</c> 0.
/// </para>
/// </remarks>
/// <param name="bus">The shared message bus, which keeps the offsets and answers the access checks.</param>
public sealed class KafkaOffsetFetchAnswerer(MessageBus.MessageBus bus) : IKafkaApiAnswerer
{
    /// <summary>The command a fault rule names to match OffsetFetch: the guide's API name.</summary>
    public const string FaultCommand = "OffsetFetch";

    private const int NoOffset = -1;
    private const int NoLeaderEpoch = -1;

    private readonly MessageBus.MessageBus bus = bus ?? throw new ArgumentNullException(nameof(bus));

    /// <inheritdoc/>
    public KafkaApiKey ApiKey => KafkaApiKey.OffsetFetch;

    /// <inheritdoc/>
    public async ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var version = request.Header.ApiVersion;
        var group = request.Fields.ReadString();
        var asked = ReadTopics(request.Fields, version);
        var fault = await KafkaCommandFault.FindAsync(request, context, FaultCommand, group);
        if (fault.DropNote is { } note)
        {
            return KafkaAnswer.Close(note);
        }

        var account = request.Connection.AccountName;
        var groupError = fault.Error
            ?? (!bus.MayUseGroup(account, group) ? KafkaErrorCode.GroupAuthorizationFailed : KafkaErrorCode.None);
        List<FetchTopic> topics = groupError != KafkaErrorCode.None && version >= 2 ? [] : asked ?? Committed(group, account, context.Scheme);
        var body = new KafkaPrimitiveWriter();
        if (version >= 3)
        {
            body.WriteInt32(0);
        }

        WriteTopics(body, version, (group, groupError, account, context.Scheme), topics);
        if (version >= 2)
        {
            body.WriteInt16((short)groupError);
        }

        return KafkaAnswer.Respond(body.ToArray());
    }

    private void WriteTopics(
        KafkaPrimitiveWriter body,
        short version,
        (string Group, KafkaErrorCode GroupError, string? Account, string Scheme) caller,
        List<FetchTopic> topics)
    {
        body.WriteArrayLength(topics.Count);
        foreach (var topic in topics)
        {
            body.WriteString(topic.Name);
            body.WriteArrayLength(topic.Partitions.Count);
            foreach (var partition in topic.Partitions)
            {
                var fetched = caller.GroupError != KafkaErrorCode.None
                    ? Failed(caller.GroupError)
                    : Find(caller.Group, topic.Name, partition, caller.Account, caller.Scheme);
                WritePartition(body, version, partition, fetched);
            }
        }
    }

    // A null array asks for every committed topic from v2; v1 has no null array, so it asks for none.
    private static List<FetchTopic>? ReadTopics(KafkaPrimitiveReader fields, short version)
    {
        var count = fields.ReadArrayLength();
        if (count is null)
        {
            return version >= 2 ? null : [];
        }

        var topics = new List<FetchTopic>();
        for (var topicIndex = 0; topicIndex < count; topicIndex++)
        {
            var name = fields.ReadString();
            var partitionCount = fields.ReadArrayLength().GetValueOrDefault();
            var partitions = new List<int>();
            for (var partitionIndex = 0; partitionIndex < partitionCount; partitionIndex++)
            {
                partitions.Add(fields.ReadInt32());
            }

            topics.Add(new FetchTopic(name, partitions));
        }

        return topics;
    }

    // Every topic the group committed that the caller may subscribe to and Kafka can name.
    private List<FetchTopic> Committed(string group, string? account, string scheme) =>
    [
        .. bus.GetCommittedOffsets(group)
            .Where(entry => bus.GetTopicAccess(account, entry.Topic).HasFlag(TopicAccessRights.Subscribe))
            .Select(entry => bus.Names.FromCanonicalTopic(scheme, entry.Topic))
            .Where(mapping => mapping.IsMapped)
            .Select(mapping => mapping.Name!)
            .Order(StringComparer.Ordinal)
            .Select(name => new FetchTopic(name, [KafkaTopicNames.OnlyPartition])),
    ];

    private Fetched Find(string group, string name, int partition, string? account, string scheme)
    {
        var mapping = bus.Names.ToCanonicalTopic(scheme, name);
        if (!mapping.IsMapped)
        {
            return Failed(KafkaErrorCode.InvalidTopicException);
        }

        if (!bus.GetTopicAccess(account, mapping.Name).HasFlag(TopicAccessRights.Subscribe))
        {
            return Failed(KafkaErrorCode.TopicAuthorizationFailed);
        }

        if (partition != KafkaTopicNames.OnlyPartition)
        {
            return Failed(KafkaErrorCode.UnknownTopicOrPartition);
        }

        return bus.GetCommittedOffset(group, mapping.Name) is { } entry
            ? new Fetched(entry.Offset, entry.LeaderEpoch, entry.Metadata ?? "", KafkaErrorCode.None)
            : Failed(KafkaErrorCode.None);
    }

    // Offset -1, leader epoch -1 and empty metadata, with the error given.
    private static Fetched Failed(KafkaErrorCode error) => new(NoOffset, NoLeaderEpoch, "", error);

    private static void WritePartition(KafkaPrimitiveWriter body, short version, int partition, Fetched fetched)
    {
        body.WriteInt32(partition);
        body.WriteInt64(fetched.Offset);
        if (version >= 5)
        {
            body.WriteInt32(fetched.LeaderEpoch);
        }

        body.WriteString(fetched.Metadata);
        body.WriteInt16((short)fetched.Error);
    }

    private sealed record FetchTopic(string Name, List<int> Partitions);

    private sealed record Fetched(long Offset, int LeaderEpoch, string Metadata, KafkaErrorCode Error);
}
