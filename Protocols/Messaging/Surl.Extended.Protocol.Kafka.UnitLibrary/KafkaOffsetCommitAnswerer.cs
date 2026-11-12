using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers OffsetCommit v2 to v7 from <c>Surl.MessageBus</c> and the broker's
/// <see cref="KafkaGroupCoordinator"/> (ADR-0145 decisions 4, 6 and 10; the Kafka protocol guide,
/// OffsetCommit).
/// </summary>
/// <remarks>
/// <para>
/// The request is first put to the exchange's fault plan (<see cref="KafkaCommandFault"/>, command
/// <c>OffsetCommit</c>, path the group id): a drop closes with no bytes, an error rule's code answers
/// every partition and nothing is stored. Then an empty group id is <c>INVALID_GROUP_ID</c> (24), a
/// caller who may not use the group 30, and the coordinator judges who may commit
/// (<see cref="KafkaGroupCoordinator.CheckCommit"/>: 25, 82, 22, 27); each of those answers every
/// partition.
/// </para>
/// <para>
/// Each partition then stands alone and is answered, in order: 17 for a name outside Kafka's rule,
/// 3 for a partition other than 0, 29 without subscribe access (whether or not the topic exists, as
/// Metadata and Fetch answer it), 3 for a topic with no log, 12 for metadata over 4096 bytes in
/// UTF-8, 44 when the bus refuses the commit (its bound or an out-of-space rule), otherwise 0 with
/// the offset stored in the bus. <c>retention_time_ms</c> (v2 to v4) is read and ignored;
/// <c>committed_leader_epoch</c> is read from v6 and is -1 before. v3 on start with
/// <c>throttle_time_ms</c> 0.
/// </para>
/// </remarks>
/// <param name="coordinator">The broker's group coordinator.</param>
/// <param name="bus">The shared message bus, which keeps the offsets and answers the access checks.</param>
public sealed class KafkaOffsetCommitAnswerer(KafkaGroupCoordinator coordinator, MessageBus.MessageBus bus) : IKafkaApiAnswerer
{
    /// <summary>The command a fault rule names to match OffsetCommit: the guide's API name.</summary>
    public const string FaultCommand = "OffsetCommit";

    /// <summary>The most UTF-8 bytes of <c>committed_metadata</c> taken: Kafka's <c>offset.metadata.max.bytes</c> default.</summary>
    public const int MaxMetadataBytes = 4096;

    private readonly KafkaGroupCoordinator coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    private readonly MessageBus.MessageBus bus = bus ?? throw new ArgumentNullException(nameof(bus));

    /// <inheritdoc/>
    public KafkaApiKey ApiKey => KafkaApiKey.OffsetCommit;

    /// <inheritdoc/>
    public async ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var version = request.Header.ApiVersion;
        var asked = Read(request.Fields, version);
        var fault = await KafkaCommandFault.FindAsync(request, context, FaultCommand, asked.Group);
        if (fault.DropNote is { } note)
        {
            return KafkaAnswer.Close(note);
        }

        var account = request.Connection.AccountName;
        var groupError = fault.Error ?? GroupError(asked, account);
        var body = new KafkaPrimitiveWriter();
        if (version >= 3)
        {
            body.WriteInt32(0);
        }

        WriteTopics(body, asked, groupError, account, context.Scheme);
        return KafkaAnswer.Respond(body.ToArray());
    }

    private void WriteTopics(KafkaPrimitiveWriter body, CommitRequest asked, KafkaErrorCode groupError, string? account, string scheme)
    {
        body.WriteArrayLength(asked.Topics.Count);
        foreach (var topic in asked.Topics)
        {
            body.WriteString(topic.Name);
            body.WriteArrayLength(topic.Partitions.Count);
            foreach (var partition in topic.Partitions)
            {
                body.WriteInt32(partition.Index);
                body.WriteInt16((short)(groupError != KafkaErrorCode.None ? groupError : Commit(asked.Group, topic.Name, partition, account, scheme)));
            }
        }
    }

    private KafkaErrorCode GroupError(CommitRequest asked, string? account) =>
        asked.Group.Length == 0 ? KafkaErrorCode.InvalidGroupId
        : !bus.MayUseGroup(account, asked.Group) ? KafkaErrorCode.GroupAuthorizationFailed
        : coordinator.CheckCommit(asked.Group, asked.GenerationId, asked.MemberId, asked.InstanceId);

    private KafkaErrorCode Commit(string group, string name, CommitPartition partition, string? account, string scheme)
    {
        var (topic, error) = KafkaFetchableTopic.Find(bus, name, partition.Index, account, scheme);
        if (error != KafkaErrorCode.None)
        {
            return error;
        }

        if (partition.Metadata is not null && Encoding.UTF8.GetByteCount(partition.Metadata) > MaxMetadataBytes)
        {
            return KafkaErrorCode.OffsetMetadataTooLarge;
        }

        var outcome = bus.CommitOffset(account, group, topic!, partition.Offset, partition.LeaderEpoch, partition.Metadata);
        return outcome == MessageBusOutcome.Succeeded ? KafkaErrorCode.None : KafkaErrorCode.PolicyViolation;
    }

    private static CommitRequest Read(KafkaPrimitiveReader fields, short version)
    {
        var group = fields.ReadString();
        var generationId = fields.ReadInt32();
        var memberId = fields.ReadString();
        var instanceId = version >= 7 ? fields.ReadNullableString() : null;
        if (version <= 4)
        {
            fields.ReadInt64();
        }

        var topics = new List<CommitTopic>();
        var topicCount = fields.ReadArrayLength().GetValueOrDefault();
        for (var topicIndex = 0; topicIndex < topicCount; topicIndex++)
        {
            var name = fields.ReadString();
            var partitions = new List<CommitPartition>();
            var partitionCount = fields.ReadArrayLength().GetValueOrDefault();
            for (var partitionIndex = 0; partitionIndex < partitionCount; partitionIndex++)
            {
                var index = fields.ReadInt32();
                var offset = fields.ReadInt64();
                var leaderEpoch = version >= 6 ? fields.ReadInt32() : -1;
                partitions.Add(new CommitPartition(index, offset, leaderEpoch, fields.ReadNullableString()));
            }

            topics.Add(new CommitTopic(name, partitions));
        }

        return new CommitRequest(group, generationId, memberId, instanceId, topics);
    }

    private sealed record CommitRequest(string Group, int GenerationId, string MemberId, string? InstanceId, List<CommitTopic> Topics);

    private sealed record CommitTopic(string Name, List<CommitPartition> Partitions);

    private sealed record CommitPartition(int Index, long Offset, int LeaderEpoch, string? Metadata);
}
