using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// One broker's group APIs for the consumer group tests (BL-948): a coordinator and bus shared by
/// every request, each request sent on a connection of its own, so a held JoinGroup or SyncGroup
/// is a task that has not finished. Requests and answers are written by hand from the Kafka
/// protocol guide's FindCoordinator, JoinGroup, SyncGroup, Heartbeat and LeaveGroup schemas.
/// </summary>
internal sealed class KafkaGroupHarness
{
    /// <summary>The protocol type every member offers unless a test says otherwise.</summary>
    public const string Consumer = "consumer";

    private readonly CancellationToken cancellationToken;

    public KafkaGroupHarness(
        CancellationToken cancellationToken,
        Func<TimeProvider, KafkaGroupCoordinator>? coordinator = null,
        MessageBusConfiguration? busConfiguration = null,
        TimeProvider? clock = null,
        int maxCommittedOffsets = Surl.MessageBus.MessageBus.DefaultMaxCommittedOffsets)
    {
        this.cancellationToken = cancellationToken;
        Clock = clock ?? new ManualTimeProvider();
        Coordinator = coordinator?.Invoke(Clock) ?? new KafkaGroupCoordinator(Clock);
        Bus = new Surl.MessageBus.MessageBus(
            new ManualTimeProvider(),
            configuration: busConfiguration,
            events: Events,
            maxCommittedOffsets: maxCommittedOffsets);
    }

    public TimeProvider Clock { get; }

    public KafkaGroupCoordinator Coordinator { get; }

    public Surl.MessageBus.MessageBus Bus { get; }

    public RecordingSurlEventPublisher Events { get; } = new();

    public IFaultPlan Faults { get; set; } = EmptyFaultPlan.Instance;

    public SequenceIdentifierRandom Identifiers { get; } = new();

    public RecordingExchangeLog Log { get; } = new();

    /// <summary>The consumer group events published so far.</summary>
    public IReadOnlyList<ConsumerGroupChangedEvent> GroupEvents => [.. Events.Events.OfType<ConsumerGroupChangedEvent>()];

    /// <summary>The <paramref name="call"/>th member id the identifiers give, with <paramref name="prefix"/>.</summary>
    public static string MemberId(string prefix, int call)
    {
        var hex = Convert.ToHexStringLower(Enumerable.Repeat((byte)call, 16).ToArray());
        return $"{prefix}-{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }

    /// <summary>Sends <paramref name="request"/> on a connection of its own and returns what the broker wrote.</summary>
    public Task<byte[]> SendAsync(byte[] request, IIdleClock? idleClock = null) => SendAsync(request, idleClock, null);

    /// <summary>Sends <paramref name="request"/> and waits until the broker holds it, the idle clock paused.</summary>
    public async Task<(Task<byte[]> Answer, RecordingIdleClock IdleClock)> SendHeldAsync(byte[] request)
    {
        var idleClock = new RecordingIdleClock();
        var answer = SendAsync(request, idleClock);
        await Task.WhenAny(idleClock.FirstPause, answer).WaitAsync(cancellationToken);
        Assert.IsFalse(answer.IsCompleted, "The request was answered at once, not held.");
        return (answer, idleClock);
    }

    public async Task<byte[]> SendAsync(byte[] request, IIdleClock? idleClock, ListenUrl? listenUrl)
    {
        var connection = new InMemoryConnection([request]);
        var context = Context(cancellationToken, Log, listenUrl, timeProvider: Clock) with
        {
            Events = Events,
            Faults = Faults,
            Identifiers = Identifiers,
            IdleClock = idleClock ?? NoIdleClock.Instance,
        };
        await Broker().ServeAsync(connection, context);
        return connection.WrittenBytes;
    }

    public KafkaBroker Broker() => new(
        new UnitTestKafkaAuthenticationPolicy(true, "PLAIN"),
        [
            new KafkaFindCoordinatorAnswerer(Bus),
            new KafkaJoinGroupAnswerer(Coordinator, Bus),
            new KafkaSyncGroupAnswerer(Coordinator, Bus),
            new KafkaHeartbeatAnswerer(Coordinator, Bus),
            new KafkaLeaveGroupAnswerer(Coordinator, Bus),
            new KafkaOffsetCommitAnswerer(Coordinator, Bus),
            new KafkaOffsetFetchAnswerer(Bus),
            new KafkaDescribeGroupsAnswerer(Coordinator, Bus),
            new KafkaListGroupsAnswerer(Coordinator, Bus),
        ]);

    /// <summary>Joins a dynamic member with v3 (no MEMBER_ID_REQUIRED), answered at once or held.</summary>
    public Task<byte[]> JoinV3Async(string memberId, params string[] protocols) =>
        SendAsync(JoinGroup(3, "g", 10000, 10000, memberId, null, Consumer, Protocols(protocols)));

    // ---- Requests -------------------------------------------------------------------------

    public static (string Name, string Metadata)[] Protocols(params string[] names) =>
        [.. (names.Length == 0 ? ["range"] : names).Select(name => (name, "m-" + name))];

    /// <summary>JoinGroup: group_id, session_timeout_ms, rebalance_timeout_ms (v1+), member_id,
    /// group_instance_id (v5), protocol_type, protocols [name, metadata].</summary>
    public static byte[] JoinGroup(
        short version,
        string group,
        int sessionTimeoutMs,
        int rebalanceTimeoutMs,
        string memberId,
        string? instanceId,
        string protocolType,
        (string Name, string Metadata)[] protocols,
        bool hasClientId = true)
    {
        byte[] fields =
        [
            .. String(group),
            .. Int32(sessionTimeoutMs),
            .. version >= 1 ? Int32(rebalanceTimeoutMs) : [],
            .. String(memberId),
            .. version >= 5 ? NullableString(instanceId) : [],
            .. String(protocolType),
            .. Int32(protocols.Length),
            .. protocols.SelectMany(protocol => (byte[])[.. String(protocol.Name), .. Bytes(protocol.Metadata)]),
        ];
        return hasClientId ? Request(KafkaApiKey.JoinGroup, version, fields) : RequestWithNullClientId(KafkaApiKey.JoinGroup, version, fields);
    }

    /// <summary>SyncGroup: group_id, generation_id, member_id, group_instance_id (v3), assignments [member_id, assignment].</summary>
    public static byte[] SyncGroup(short version, string group, int generationId, string memberId, string? instanceId, params (string MemberId, string Assignment)[] assignments) =>
        Request(
            KafkaApiKey.SyncGroup,
            version,
            [
                .. String(group),
                .. Int32(generationId),
                .. String(memberId),
                .. version >= 3 ? NullableString(instanceId) : [],
                .. Int32(assignments.Length),
                .. assignments.SelectMany(assignment => (byte[])[.. String(assignment.MemberId), .. Bytes(assignment.Assignment)]),
            ]);

    /// <summary>Heartbeat: group_id, generation_id, member_id, group_instance_id (v3).</summary>
    public static byte[] Heartbeat(short version, string group, int generationId, string memberId, string? instanceId = null) =>
        Request(KafkaApiKey.Heartbeat, version, [.. String(group), .. Int32(generationId), .. String(memberId), .. version >= 3 ? NullableString(instanceId) : []]);

    /// <summary>LeaveGroup v0 to v2: group_id, member_id.</summary>
    public static byte[] LeaveGroup(short version, string group, string memberId) =>
        Request(KafkaApiKey.LeaveGroup, version, [.. String(group), .. String(memberId)]);

    /// <summary>LeaveGroup v3: group_id, members [member_id, group_instance_id].</summary>
    public static byte[] LeaveGroupV3(string group, params (string MemberId, string? InstanceId)[] members) =>
        Request(
            KafkaApiKey.LeaveGroup,
            3,
            [.. String(group), .. Int32(members.Length), .. members.SelectMany(member => (byte[])[.. String(member.MemberId), .. NullableString(member.InstanceId)])]);

    /// <summary>FindCoordinator: key, key_type (v1+).</summary>
    public static byte[] FindCoordinator(short version, string key, sbyte keyType = 0) =>
        Request(KafkaApiKey.FindCoordinator, version, [.. String(key), .. version >= 1 ? [(byte)keyType] : Array.Empty<byte>()]);

    // ---- Answers --------------------------------------------------------------------------

    /// <summary>A JoinGroup answer: throttle_time_ms (v2+), error_code, generation_id, protocol_name,
    /// leader, member_id, members [member_id, group_instance_id (v5), metadata].</summary>
    public static byte[] JoinAnswer(
        short version,
        short error,
        int generationId,
        string protocol,
        string leader,
        string memberId,
        params (string MemberId, string? InstanceId, string Metadata)[] members) =>
        Response(
        [
            .. version >= 2 ? Int32(0) : [],
            .. Int16(error),
            .. Int32(generationId),
            .. String(protocol),
            .. String(leader),
            .. String(memberId),
            .. Int32(members.Length),
            .. members.SelectMany(member => (byte[])[.. String(member.MemberId), .. version >= 5 ? NullableString(member.InstanceId) : [], .. Bytes(member.Metadata)]),
        ]);

    /// <summary>A refused JoinGroup: generation -1, empty protocol and leader, no members.</summary>
    public static byte[] JoinRefused(short version, short error, string memberId) => JoinAnswer(version, error, -1, "", "", memberId);

    /// <summary>A SyncGroup answer: throttle_time_ms (v1+), error_code, assignment.</summary>
    public static byte[] SyncAnswer(short version, short error, string assignment = "") =>
        Response([.. version >= 1 ? Int32(0) : [], .. Int16(error), .. Bytes(assignment)]);

    /// <summary>A Heartbeat or LeaveGroup v0 to v2 answer: throttle_time_ms (v1+), error_code.</summary>
    public static byte[] ErrorAnswer(short version, short error) => Response([.. version >= 1 ? Int32(0) : [], .. Int16(error)]);

    /// <summary>A LeaveGroup v3 answer: throttle_time_ms, error_code, members [member_id, group_instance_id, error_code].</summary>
    public static byte[] LeaveV3Answer(short error, params (string MemberId, string? InstanceId, short Error)[] members) =>
        Response(
        [
            .. Int32(0),
            .. Int16(error),
            .. Int32(members.Length),
            .. members.SelectMany(member => (byte[])[.. String(member.MemberId), .. NullableString(member.InstanceId), .. Int16(member.Error)]),
        ]);

    // ---- Committed offsets, DescribeGroups and ListGroups ---------------------------------

    /// <summary>One partition of an OffsetCommit: index, offset, leader epoch (v6+), metadata.</summary>
    public sealed record Commit(int Index, long Offset, int Epoch = -1, string? Metadata = null);

    /// <summary>OffsetCommit v2 to v7: group_id, generation_id, member_id, group_instance_id (v7),
    /// retention_time_ms (v2 to v4), topics [name, partitions [index, offset, leader_epoch (v6+), metadata]].</summary>
    public static byte[] OffsetCommit(
        short version,
        string group,
        int generationId,
        string memberId,
        string? instanceId,
        params (string Name, Commit[] Partitions)[] topics) =>
        Request(
            KafkaApiKey.OffsetCommit,
            version,
            [
                .. String(group),
                .. Int32(generationId),
                .. String(memberId),
                .. version >= 7 ? NullableString(instanceId) : [],
                .. version <= 4 ? Long(86400000) : [],
                .. Int32(topics.Length),
                .. topics.SelectMany(topic => (byte[])
                [
                    .. String(topic.Name),
                    .. Int32(topic.Partitions.Length),
                    .. topic.Partitions.SelectMany(partition => (byte[])
                    [
                        .. Int32(partition.Index),
                        .. Long(partition.Offset),
                        .. version >= 6 ? Int32(partition.Epoch) : [],
                        .. NullableString(partition.Metadata),
                    ]),
                ]),
            ]);

    /// <summary>A simple commit of offset <paramref name="offset"/> on partition 0 of topic <paramref name="topic"/> (v2).</summary>
    public static byte[] SimpleCommit(string group, string topic, long offset, string? metadata = null) =>
        OffsetCommit(2, group, -1, "", null, (topic, [new Commit(0, offset, -1, metadata)]));

    /// <summary>An OffsetCommit answer: throttle_time_ms (v3+), topics [name, partitions [index, error_code]].</summary>
    public static byte[] OffsetCommitAnswer(short version, params (string Name, (int Index, short Error)[] Partitions)[] topics) =>
        Response(
        [
            .. version >= 3 ? Int32(0) : [],
            .. Int32(topics.Length),
            .. topics.SelectMany(topic => (byte[])
            [
                .. String(topic.Name),
                .. Int32(topic.Partitions.Length),
                .. topic.Partitions.SelectMany(partition => (byte[])[.. Int32(partition.Index), .. Int16(partition.Error)]),
            ]),
        ]);

    /// <summary>An OffsetCommit answer for partition 0 of one topic.</summary>
    public static byte[] OffsetCommitAnswer(short version, string topic, short error) => OffsetCommitAnswer(version, (topic, [(0, error)]));

    /// <summary>OffsetFetch v1 to v5: group_id, topics [name, partition_indexes]; null topics is length -1.</summary>
    public static byte[] OffsetFetch(short version, string group, params (string Name, int[] Partitions)[]? topics) =>
        Request(
            KafkaApiKey.OffsetFetch,
            version,
            [
                .. String(group),
                .. topics is null ? Int32(-1) : Int32(topics.Length),
                .. (topics ?? []).SelectMany(topic => (byte[])
                [
                    .. String(topic.Name),
                    .. Int32(topic.Partitions.Length),
                    .. topic.Partitions.SelectMany(partition => Int32(partition)),
                ]),
            ]);

    /// <summary>One partition of an OffsetFetch answer.</summary>
    public sealed record Fetched(int Index, long Offset, int Epoch, string Metadata, short Error);

    /// <summary>An OffsetFetch answer: throttle_time_ms (v3+), topics [name, partitions [index, offset,
    /// leader_epoch (v5), metadata, error_code]], error_code (v2+).</summary>
    public static byte[] OffsetFetchAnswer(short version, short error, params (string Name, Fetched[] Partitions)[] topics) =>
        Response(
        [
            .. version >= 3 ? Int32(0) : [],
            .. Int32(topics.Length),
            .. topics.SelectMany(topic => (byte[])
            [
                .. String(topic.Name),
                .. Int32(topic.Partitions.Length),
                .. topic.Partitions.SelectMany(partition => (byte[])
                [
                    .. Int32(partition.Index),
                    .. Long(partition.Offset),
                    .. version >= 5 ? Int32(partition.Epoch) : [],
                    .. String(partition.Metadata),
                    .. Int16(partition.Error),
                ]),
            ]),
            .. version >= 2 ? Int16(error) : [],
        ]);

    /// <summary>OffsetFetch's answer for partition 0 of one topic that is not committed.</summary>
    public static Fetched NotCommitted(short error = 0) => new(0, -1, -1, "", error);

    /// <summary>DescribeGroups: groups, include_authorized_operations (v3+).</summary>
    public static byte[] DescribeGroups(short version, bool includeOperations, params string[] groups) =>
        Request(
            KafkaApiKey.DescribeGroups,
            version,
            [.. Int32(groups.Length), .. groups.SelectMany(group => String(group)), .. version >= 3 ? new[] { (byte)(includeOperations ? 1 : 0) } : Array.Empty<byte>()]);

    /// <summary>One member of a DescribeGroups answer.</summary>
    public sealed record Described(string MemberId, string? InstanceId, string ClientId, string ClientHost, string Metadata, string Assignment);

    /// <summary>One group of a DescribeGroups answer.</summary>
    public sealed record DescribedGroup(short Error, string Group, string State, string ProtocolType, string ProtocolData, Described[] Members);

    /// <summary>A DescribeGroups answer: throttle_time_ms (v1+), groups [error_code, group_id, group_state,
    /// protocol_type, protocol_data, members [member_id, group_instance_id (v4), client_id, client_host,
    /// member_metadata, member_assignment], authorized_operations (v3+)].</summary>
    public static byte[] DescribeGroupsAnswer(short version, int operations, params DescribedGroup[] groups) =>
        Response(
        [
            .. version >= 1 ? Int32(0) : [],
            .. Int32(groups.Length),
            .. groups.SelectMany(group => (byte[])
            [
                .. Int16(group.Error),
                .. String(group.Group),
                .. String(group.State),
                .. String(group.ProtocolType),
                .. String(group.ProtocolData),
                .. Int32(group.Members.Length),
                .. group.Members.SelectMany(member => (byte[])
                [
                    .. String(member.MemberId),
                    .. version >= 4 ? NullableString(member.InstanceId) : [],
                    .. String(member.ClientId),
                    .. String(member.ClientHost),
                    .. Bytes(member.Metadata),
                    .. Bytes(member.Assignment),
                ]),
                .. version >= 3 ? Int32(operations) : [],
            ]),
        ]);

    /// <summary>ListGroups: an empty request body.</summary>
    public static byte[] ListGroups(short version) => Request(KafkaApiKey.ListGroups, version);

    /// <summary>A ListGroups answer: throttle_time_ms (v1+), error_code, groups [group_id, protocol_type].</summary>
    public static byte[] ListGroupsAnswer(short version, short error, params (string Group, string ProtocolType)[] groups) =>
        Response(
        [
            .. version >= 1 ? Int32(0) : [],
            .. Int16(error),
            .. Int32(groups.Length),
            .. groups.SelectMany(group => (byte[])[.. String(group.Group), .. String(group.ProtocolType)]),
        ]);

    private static byte[] Long(long value) => [.. Int32((int)(value >> 32)), .. Int32((int)value)];

    public static byte[] NullableString(string? value) => value is null ? [0xFF, 0xFF] : String(value);

    // Request header v1 with a null client_id.
    private static byte[] RequestWithNullClientId(KafkaApiKey apiKey, short version, byte[] fields)
    {
        byte[] header = [.. Int16((short)apiKey), .. Int16(version), 0, 0, 0, CorrelationId, 0xFF, 0xFF];
        return [.. Int32(header.Length + fields.Length), .. header, .. fields];
    }
}

/// <summary>An identifier source whose nth request is filled with the byte n, so member ids follow one another.</summary>
internal sealed class SequenceIdentifierRandom : IIdentifierRandom
{
    public int Calls { get; private set; }

    public void NextBytes(Span<byte> destination)
    {
        Calls++;
        destination.Fill((byte)Calls);
    }
}

/// <summary>A clock that records every timer's callback, so a test can fire one late, as a real
/// timer racing a change may; otherwise it is the <see cref="ManualTimeProvider"/> it wraps.</summary>
internal sealed class CallbackRecordingTimeProvider : TimeProvider
{
    private readonly ManualTimeProvider inner = new();

    public List<(TimerCallback Callback, object? State)> Timers { get; } = [];

    public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        Timers.Add((callback, state));
        return inner.CreateTimer(callback, state, dueTime, period);
    }

    public void Advance(TimeSpan duration) => inner.Advance(duration);
}
