using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers Metadata v0 to v8 from the shared message bus (ADR-0105, decision 2; the Kafka
/// protocol guide, Metadata).
/// </summary>
/// <remarks>
/// <para>
/// <b>The cluster.</b> One broker, node 0, which is also the controller (v1), advertised at the
/// host and port the client reached: the listener's bound address and port, or the connection's
/// local address when the listener is bound to a wildcard address. <c>rack</c> (v1) is null.
/// <c>cluster_id</c> (v2) is 16 random bytes in base64url, 22 characters, fixed for the
/// answerer's life. <c>throttle_time_ms</c> (v3) is 0.
/// </para>
/// <para>
/// <b>Topics.</b> A request naming no topics - null from v1, an empty array in v0 - lists every
/// bus topic Kafka can name and the caller may publish or subscribe to, in ordinal order of the
/// Kafka name. A named topic is answered in the order named: <c>INVALID_TOPIC_EXCEPTION</c> (17)
/// for a name outside Kafka's rule; <c>TOPIC_AUTHORIZATION_FAILED</c> (29) when the caller may
/// neither publish nor subscribe, whether or not it exists; otherwise, when it has no log, it is
/// created when the request allows auto-creation (always v0 to v3, from v4 by
/// <c>allow_auto_topic_creation</c>), <c>POLICY_VIOLATION</c> (44) when the bus holds its most
/// topics, and <c>UNKNOWN_TOPIC_OR_PARTITION</c> (3) when it does not allow it. A topic answered
/// error 0 has one partition, 0, whose leader, only replica and only in-sync replica is node 0,
/// with no offline replicas (v5) and leader epoch 0 (v7); <c>is_internal</c> (v1) is false. A
/// topic with an error has no partitions.
/// </para>
/// <para>
/// <b>Authorized operations</b> (v8) are <c>-2147483648</c> unless asked for; asked for, a topic's
/// are DESCRIBE always, READ when the caller may subscribe and WRITE when it may publish, and the
/// cluster's DESCRIBE alone.
/// </para>
/// </remarks>
public sealed class KafkaMetadataAnswerer : IKafkaApiAnswerer
{
    /// <summary>The authorized operations of a request that did not ask for them.</summary>
    public const int OperationsNotRequested = int.MinValue;

    private const int NodeId = 0;
    private const int Describe = 1 << 8;
    private const int Read = 1 << 3;
    private const int Write = 1 << 4;

    private readonly MessageBus.MessageBus bus;

    /// <summary>Creates an answerer over <paramref name="bus"/>, with a new cluster id.</summary>
    /// <param name="bus">The shared message bus whose topics are listed and created.</param>
    public KafkaMetadataAnswerer(MessageBus.MessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);

        this.bus = bus;
        ClusterId = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
    }

    /// <inheritdoc/>
    public KafkaApiKey ApiKey => KafkaApiKey.Metadata;

    /// <summary>The cluster id every response carries from v2.</summary>
    public string ClusterId { get; }

    /// <inheritdoc/>
    public ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var version = request.Header.ApiVersion;
        var asked = MetadataRequest.Read(request.Fields, version);
        var account = request.Connection.AccountName;
        var topics = asked.TopicNames is { } names
            ? names.Select(name => Named(name, asked.AllowsAutoCreation, account, context.Scheme)).ToList()
            : Listed(account, context.Scheme);

        var body = new KafkaPrimitiveWriter();
        if (version >= 3)
        {
            body.WriteInt32(0);
        }

        WriteCluster(body, version, context);
        body.WriteArrayLength(topics.Count);
        foreach (var topic in topics)
        {
            WriteTopic(body, version, topic, asked.IncludesTopicOperations);
        }

        if (version >= 8)
        {
            body.WriteInt32(asked.IncludesClusterOperations ? Describe : OperationsNotRequested);
        }

        return ValueTask.FromResult(KafkaAnswer.Respond(body.ToArray()));
    }

    /// <summary>The host the client reached: the listener's address, or the connection's local
    /// address when the listener is bound to a wildcard address.</summary>
    internal static string AdvertisedHost(ExchangeContext context)
    {
        var host = context.ListenUrl.Host;
        var isWildcard = IPAddress.TryParse(host, out var address)
            && (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any));
        if (!isWildcard || context.LocalEndPoint is not IPEndPoint local)
        {
            return host;
        }

        return (local.Address.IsIPv4MappedToIPv6 ? local.Address.MapToIPv4() : local.Address).ToString();
    }

    private TopicRow Named(string name, bool allowsAutoCreation, string? account, string scheme)
    {
        var mapping = bus.Names.ToCanonicalTopic(scheme, name);
        if (!mapping.IsMapped)
        {
            return new(name, KafkaErrorCode.InvalidTopicException, TopicAccessRights.None);
        }

        var rights = bus.GetTopicAccess(account, mapping.Name);
        if (rights == TopicAccessRights.None)
        {
            return new(name, KafkaErrorCode.TopicAuthorizationFailed, rights);
        }

        var error = allowsAutoCreation
            ? (bus.CreateLog(mapping.Name) == MessageBusOutcome.Succeeded ? KafkaErrorCode.None : KafkaErrorCode.PolicyViolation)
            : (bus.GetLogTopics().Contains(mapping.Name, StringComparer.Ordinal) ? KafkaErrorCode.None : KafkaErrorCode.UnknownTopicOrPartition);
        return new(name, error, rights);
    }

    private List<TopicRow> Listed(string? account, string scheme) =>
    [
        .. bus.GetLogTopics()
            .Select(topic => (Topic: topic, Mapping: bus.Names.FromCanonicalTopic(scheme, topic)))
            .Where(listed => listed.Mapping.IsMapped)
            .Select(listed => new TopicRow(listed.Mapping.Name!, KafkaErrorCode.None, bus.GetTopicAccess(account, listed.Topic)))
            .Where(row => row.Rights != TopicAccessRights.None)
            .OrderBy(row => row.Name, StringComparer.Ordinal),
    ];

    private void WriteCluster(KafkaPrimitiveWriter body, short version, ExchangeContext context)
    {
        body.WriteArrayLength(1);
        body.WriteInt32(NodeId);
        body.WriteString(AdvertisedHost(context));
        body.WriteInt32(context.ListenUrl.BoundPort ?? context.ListenUrl.Port);
        if (version >= 1)
        {
            body.WriteNullableString(null);
        }

        if (version >= 2)
        {
            body.WriteNullableString(ClusterId);
        }

        if (version >= 1)
        {
            body.WriteInt32(NodeId);
        }
    }

    private static void WriteTopic(KafkaPrimitiveWriter body, short version, TopicRow topic, bool includesOperations)
    {
        body.WriteInt16((short)topic.Error);
        body.WriteString(topic.Name);
        if (version >= 1)
        {
            body.WriteBoolean(false);
        }

        var hasPartition = topic.Error == KafkaErrorCode.None;
        body.WriteArrayLength(hasPartition ? 1 : 0);
        if (hasPartition)
        {
            WritePartitionZero(body, version);
        }

        if (version >= 8)
        {
            body.WriteInt32(includesOperations ? Operations(topic.Rights) : OperationsNotRequested);
        }
    }

    private static void WritePartitionZero(KafkaPrimitiveWriter body, short version)
    {
        body.WriteInt16((short)KafkaErrorCode.None);
        body.WriteInt32(KafkaTopicNames.OnlyPartition);
        body.WriteInt32(NodeId);
        if (version >= 7)
        {
            body.WriteInt32(0);
        }

        WriteNodeZero(body);
        WriteNodeZero(body);
        if (version >= 5)
        {
            body.WriteArrayLength(0);
        }
    }

    private static void WriteNodeZero(KafkaPrimitiveWriter body)
    {
        body.WriteArrayLength(1);
        body.WriteInt32(NodeId);
    }

    private static int Operations(TopicAccessRights rights) =>
        Describe
        | (rights.HasFlag(TopicAccessRights.Subscribe) ? Read : 0)
        | (rights.HasFlag(TopicAccessRights.Publish) ? Write : 0);

    // One topic row: the Kafka name, its error and what the caller may do with it.
    private sealed record TopicRow(string Name, KafkaErrorCode Error, TopicAccessRights Rights);

    // A Metadata request's fields: the topics named (null for every topic), whether a missing
    // one may be created, and whether v8's authorized operations are asked for.
    private sealed record MetadataRequest(
        IReadOnlyList<string>? TopicNames,
        bool AllowsAutoCreation,
        bool IncludesClusterOperations,
        bool IncludesTopicOperations)
    {
        public static MetadataRequest Read(KafkaPrimitiveReader fields, short version)
        {
            var names = ReadTopicNames(fields);
            var namesEveryTopic = names is null || (version == 0 && names.Count == 0);
            return ReadFlags(fields, version, namesEveryTopic ? null : names);
        }

        private static MetadataRequest ReadFlags(KafkaPrimitiveReader fields, short version, IReadOnlyList<string>? names)
        {
            var allowsAutoCreation = version < 4 || fields.ReadBoolean();
            var includesCluster = version >= 8 && fields.ReadBoolean();
            var includesTopic = version >= 8 && fields.ReadBoolean();
            return new(names, allowsAutoCreation, includesCluster, includesTopic);
        }

        private static List<string>? ReadTopicNames(KafkaPrimitiveReader fields)
        {
            if (fields.ReadArrayLength() is not { } count)
            {
                return null;
            }

            var names = new List<string>(Math.Min(count, fields.Remaining / 2));
            for (var index = 0; index < count; index++)
            {
                names.Add(fields.ReadString());
            }

            return names;
        }
    }
}
