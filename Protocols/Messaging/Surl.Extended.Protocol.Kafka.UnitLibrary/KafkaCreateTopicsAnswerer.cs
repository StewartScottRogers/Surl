using System.Globalization;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers CreateTopics v2 to v4 by giving each valid, new topic its empty one-partition log on the
/// shared message bus (ADR-0145 decisions 7 and 10; the Kafka protocol guide, CreateTopics).
/// </summary>
/// <remarks>
/// <para>
/// <b>Each topic</b>, in request order, is first put to the exchange's fault plan at
/// <see cref="FaultStage.Command"/>, command <c>CreateTopics</c>, path its Kafka name, account the
/// caller's (<see cref="CommandStageFaults"/>): a delay holds the answer, a drop aborts the connection
/// with no answer, and an <see cref="FaultKind.Error"/> rule whose reply is a Kafka error code 1 to 127
/// is that topic's <c>error_code</c>, the topic not created; a reply outside that is noted and not
/// applied. Otherwise the checks run in order: a name given twice in the request is
/// <c>INVALID_REQUEST</c> (42) for each occurrence; a name outside Kafka's rule
/// <c>INVALID_TOPIC_EXCEPTION</c> (17); no publish access to its canonical topic
/// <c>TOPIC_AUTHORIZATION_FAILED</c> (29); a topic with a log <c>TOPIC_ALREADY_EXISTS</c> (36);
/// <c>num_partitions</c> other than 1 or -1 <c>INVALID_PARTITIONS</c> (37); <c>replication_factor</c>
/// other than 1 or -1 <c>INVALID_REPLICATION_FACTOR</c> (38); <c>assignments</c> other than none or
/// exactly partition 0 on node <c>[0]</c> <c>INVALID_REPLICA_ASSIGNMENT</c> (39); then
/// <see cref="MessageBus.MessageBus.CreateLog"/> makes the empty log, and the bus's most topics is
/// <c>POLICY_VIOLATION</c> (44).
/// </para>
/// <para>
/// <c>validate_only</c> makes every check but <c>CreateLog</c>'s bound and creates nothing.
/// <c>timeout_ms</c> is read and ignored, creation being immediate. <c>configs</c> are accepted and
/// ignored, and the exchange log notes each topic's ignored names. Each topic is answered with its
/// name, its <c>error_code</c> and a null <c>error_message</c>; <c>throttle_time_ms</c> is 0.
/// </para>
/// </remarks>
/// <param name="bus">The shared message bus the topics are created on.</param>
public sealed class KafkaCreateTopicsAnswerer(MessageBus.MessageBus bus) : IKafkaApiAnswerer
{
    /// <summary>The command a fault rule names to match CreateTopics: the guide's API name.</summary>
    public const string FaultCommand = "CreateTopics";

    private const int OnlyNode = 0;

    private readonly MessageBus.MessageBus bus = bus ?? throw new ArgumentNullException(nameof(bus));

    /// <inheritdoc/>
    public KafkaApiKey ApiKey => KafkaApiKey.CreateTopics;

    /// <inheritdoc/>
    public async ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var asked = CreateTopicsRequest.Read(request.Fields);
        var duplicates = asked.Topics
            .GroupBy(topic => topic.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var answers = new List<(string Name, KafkaErrorCode Error)>(asked.Topics.Count);
        foreach (var topic in asked.Topics)
        {
            var rule = await FindFaultAsync(topic.Name, request, context);
            if (rule is { Kind: FaultKind.Drop })
            {
                return KafkaAnswer.Close($"CreateTopics was dropped by fault rule {rule.Id}");
            }

            NoteIgnoredConfigs(topic, context);
            var chosen = ChosenError(rule, topic.Name, context);
            answers.Add((topic.Name, chosen ?? Answer(topic, duplicates.Contains(topic.Name), asked.ValidateOnly, request.Connection.AccountName, context.Scheme)));
        }

        return KafkaAnswer.Respond(Encode(answers));
    }

    private static async ValueTask<FaultRule?> FindFaultAsync(string name, KafkaRequest request, ExchangeContext context)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = request.Connection.AccountName,
            Path = name,
            Command = FaultCommand,
        };
        return await CommandStageFaults.FindAndApplyAsync(context, request.Transport, point, name);
    }

    // An Error rule's code, when its reply is a Kafka error code 1 to 127 (ADR-0124 decision 4's
    // kafka row); null to answer the topic as usual.
    private static KafkaErrorCode? ChosenError(FaultRule? rule, string name, ExchangeContext context)
    {
        if (rule is not { Kind: FaultKind.Error })
        {
            return null;
        }

        if (!int.TryParse(rule.Reply, NumberStyles.None, CultureInfo.InvariantCulture, out var code) || code is < 1 or > 127)
        {
            context.Log.Note($"Fault rule {rule.Id} not applied to CreateTopics of {name}: its reply is not a Kafka error code 1 to 127.");
            return null;
        }

        context.Log.Note($"CreateTopics of {name} answered error {code} by fault rule {rule.Id}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, name));
        return (KafkaErrorCode)code;
    }

    private static void NoteIgnoredConfigs(TopicRequest topic, ExchangeContext context)
    {
        if (topic.ConfigNames.Count > 0)
        {
            context.Log.Note($"CreateTopics ignored the configs of {topic.Name}: {string.Join(", ", topic.ConfigNames)}.");
        }
    }

    private KafkaErrorCode Answer(TopicRequest topic, bool isDuplicate, bool validateOnly, string? account, string scheme)
    {
        if (isDuplicate)
        {
            return KafkaErrorCode.InvalidRequest;
        }

        var mapping = bus.Names.ToCanonicalTopic(scheme, topic.Name);
        if (!mapping.IsMapped)
        {
            return KafkaErrorCode.InvalidTopicException;
        }

        var refusal = Refusal(topic, mapping.Name, account);
        if (refusal != KafkaErrorCode.None || validateOnly)
        {
            return refusal;
        }

        return bus.CreateLog(mapping.Name) == MessageBusOutcome.Succeeded ? KafkaErrorCode.None : KafkaErrorCode.PolicyViolation;
    }

    private KafkaErrorCode Refusal(TopicRequest topic, string canonical, string? account) =>
        !bus.GetTopicAccess(account, canonical).HasFlag(TopicAccessRights.Publish) ? KafkaErrorCode.TopicAuthorizationFailed
        : bus.GetLogTopics().Contains(canonical, StringComparer.Ordinal) ? KafkaErrorCode.TopicAlreadyExists
        : topic.PartitionCount is not (1 or -1) ? KafkaErrorCode.InvalidPartitions
        : topic.ReplicationFactor is not (1 or -1) ? KafkaErrorCode.InvalidReplicationFactor
        : !topic.IsAssignedToPartitionZeroOnNodeZeroOrUnassigned ? KafkaErrorCode.InvalidReplicaAssignment
        : KafkaErrorCode.None;

    private static byte[] Encode(List<(string Name, KafkaErrorCode Error)> answers)
    {
        var body = new KafkaPrimitiveWriter();
        body.WriteInt32(0);
        body.WriteArrayLength(answers.Count);
        foreach (var (name, error) in answers)
        {
            body.WriteString(name);
            body.WriteInt16((short)error);
            body.WriteNullableString(null);
        }

        return body.ToArray();
    }

    // One partition's assignment: its index and the nodes it is placed on.
    private sealed record Assignment(int PartitionIndex, IReadOnlyList<int> NodeIds);

    // One topic of the request: its name, partition count, replication factor, assignments and
    // the names of its configs.
    private sealed record TopicRequest(
        string Name,
        int PartitionCount,
        short ReplicationFactor,
        IReadOnlyList<Assignment> Assignments,
        IReadOnlyList<string> ConfigNames)
    {
        public bool IsAssignedToPartitionZeroOnNodeZeroOrUnassigned =>
            Assignments.Count == 0
            || (Assignments.Count == 1
                && Assignments[0].PartitionIndex == KafkaTopicNames.OnlyPartition
                && Assignments[0].NodeIds.SequenceEqual([OnlyNode]));
    }

    // A CreateTopics v2 to v4 request: its topics, then timeout_ms (ignored) and validate_only.
    private sealed record CreateTopicsRequest(IReadOnlyList<TopicRequest> Topics, bool ValidateOnly)
    {
        public static CreateTopicsRequest Read(KafkaPrimitiveReader fields)
        {
            var topics = ReadArray(fields, ReadTopic);
            _ = fields.ReadInt32();
            return new(topics, fields.ReadBoolean());
        }

        private static TopicRequest ReadTopic(KafkaPrimitiveReader fields) => new(
            fields.ReadString(),
            fields.ReadInt32(),
            fields.ReadInt16(),
            ReadArray(fields, ReadAssignment),
            ReadArray(fields, ReadConfigName));

        private static Assignment ReadAssignment(KafkaPrimitiveReader fields) =>
            new(fields.ReadInt32(), ReadArray(fields, nodes => nodes.ReadInt32()));

        private static string ReadConfigName(KafkaPrimitiveReader fields)
        {
            var name = fields.ReadString();
            _ = fields.ReadNullableString();
            return name;
        }

        // A null array is read as an empty one.
        private static List<T> ReadArray<T>(KafkaPrimitiveReader fields, Func<KafkaPrimitiveReader, T> readItem)
        {
            var count = fields.ReadArrayLength().GetValueOrDefault();
            var items = new List<T>();
            for (var index = 0; index < count; index++)
            {
                items.Add(readItem(fields));
            }

            return items;
        }
    }
}
