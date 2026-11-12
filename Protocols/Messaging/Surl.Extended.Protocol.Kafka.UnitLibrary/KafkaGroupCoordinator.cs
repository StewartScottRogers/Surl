using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Runs every consumer group of one broker by the classic group protocol (ADR-0145 decisions 0, 3,
/// 9 and 10): members, generations, the join phase, the leader, the protocol chosen, assignments
/// and sessions, in memory, never persisted, snapshotted or reset. One coordinator is shared by an
/// instance's <c>kafka</c> and <c>kafkas</c> listeners, through the JoinGroup, SyncGroup, Heartbeat
/// and LeaveGroup answerers.
/// </summary>
/// <remarks>
/// <para>
/// <b>The join phase.</b> A join to a group not preparing a rebalance moves it to
/// <see cref="KafkaGroupState.PreparingRebalance"/>, with no initial delay; each JoinGroup is held
/// until the phase ends, when every member has one waiting or when the largest rebalance timeout
/// among the members has passed since it began. Members with none waiting are then removed; the
/// generation grows by one; the leader is the previous leader when it rejoined, otherwise the
/// earliest member; the protocol is the leader's first that every member lists; and the group
/// moves to <see cref="KafkaGroupState.CompletingRebalance"/>. The leader's SyncGroup hands each
/// member exactly the bytes it names for it, empty bytes to a member it does not name, and moves
/// the group to <see cref="KafkaGroupState.Stable"/>; a follower's SyncGroup is held until then.
/// </para>
/// <para>
/// <b>Sessions.</b> A member not waiting in the join phase is removed once its session timeout
/// passes with no JoinGroup, SyncGroup or Heartbeat, and the group rebalances. A
/// <c>MEMBER_ID_REQUIRED</c> answer's id is held pending for the session timeout. A group with no
/// members and none pending is forgotten. Every deadline is a timer of the injected
/// <see cref="TimeProvider"/>.
/// </para>
/// <para>
/// <b>Events.</b> Each change is one <see cref="ConsumerGroupChangedEvent"/>, published on the
/// <see cref="ISurlEventPublisher"/> of the group's latest request: a timer's removals and
/// generations carry no exchange id.
/// </para>
/// </remarks>
public sealed class KafkaGroupCoordinator
{
    /// <summary>The default for <see cref="MaxGroups"/>: 1000 (ADR-0145 decision 9).</summary>
    public const int DefaultMaxGroups = 1000;

    /// <summary>The default for <see cref="MaxMembersPerGroup"/>: 1000 (ADR-0145 decision 9).</summary>
    public const int DefaultMaxMembersPerGroup = 1000;

    /// <summary>The default for <see cref="MaxGroupStateBytes"/>: 268435456, 256 MiB (ADR-0145 decision 9).</summary>
    public const long DefaultMaxGroupStateBytes = 268_435_456;

    /// <summary>The shortest session timeout a JoinGroup may ask for: Kafka's default <c>group.min.session.timeout.ms</c>.</summary>
    public const int MinSessionTimeoutMs = 6000;

    /// <summary>The longest session timeout a JoinGroup may ask for, and the longest rebalance timeout: Kafka's default <c>group.max.session.timeout.ms</c>.</summary>
    public const int MaxSessionTimeoutMs = 1_800_000;

    private readonly Lock gate = new();
    private readonly Dictionary<string, Group> groups = new(StringComparer.Ordinal);
    private readonly TimeProvider timeProvider;
    private long stateBytes;

    /// <summary>Creates a coordinator holding no groups.</summary>
    /// <param name="timeProvider">The instance's clock, which times every session and join phase.</param>
    /// <param name="maxGroups">The most groups held, with members or pending ones.</param>
    /// <param name="maxMembersPerGroup">The most members of one group, pending ones included.</param>
    /// <param name="maxGroupStateBytes">The most member metadata and assignment bytes held, all groups together.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A bound is zero or less.</exception>
    public KafkaGroupCoordinator(
        TimeProvider timeProvider,
        int maxGroups = DefaultMaxGroups,
        int maxMembersPerGroup = DefaultMaxMembersPerGroup,
        long maxGroupStateBytes = DefaultMaxGroupStateBytes)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxGroups);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxMembersPerGroup);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxGroupStateBytes);
        this.timeProvider = timeProvider;
        MaxGroups = maxGroups;
        MaxMembersPerGroup = maxMembersPerGroup;
        MaxGroupStateBytes = maxGroupStateBytes;
    }

    /// <summary>The most groups held; a JoinGroup that would make one more is <c>POLICY_VIOLATION</c> (44).</summary>
    public int MaxGroups { get; }

    /// <summary>The most members of one group, pending ones included; one more is <c>GROUP_MAX_SIZE_REACHED</c> (81).</summary>
    public int MaxMembersPerGroup { get; }

    /// <summary>The most member metadata and assignment bytes held; a JoinGroup or a leader's SyncGroup passing it is 44.</summary>
    public long MaxGroupStateBytes { get; }

    /// <summary>
    /// Joins a member to a group, answered once the join phase ends; refusals and
    /// <c>MEMBER_ID_REQUIRED</c> are answered at once. Checks, in order: the session timeout (26),
    /// an empty protocol type or list (23), a fenced instance (82), an unknown member id (25), the
    /// bounds (81, 44), a protocol type or list the other members do not share (23).
    /// </summary>
    /// <param name="request">The JoinGroup's fields; the group id is not empty and access is already checked.</param>
    /// <param name="caller">Who asked.</param>
    /// <returns>The answer, completed when the join phase ends.</returns>
    internal Task<KafkaJoinGroupResult> JoinAsync(KafkaJoinGroupRequest request, KafkaGroupCaller caller)
    {
        Task<JoinPhase> phase;
        string memberId;
        lock (gate)
        {
            var refusal = JoinRefusal(request, out var group);
            if (refusal != KafkaErrorCode.None)
            {
                return Task.FromResult(KafkaJoinGroupResult.Failed(refusal, request.MemberId));
            }

            if (request is { MemberId.Length: 0, InstanceId: null, IsMemberIdRequired: true })
            {
                return Task.FromResult(KafkaJoinGroupResult.Failed(KafkaErrorCode.MemberIdRequired, AddPending(group, request, caller)));
            }

            (phase, memberId) = Join(group ?? AddGroup(request.Group), request, caller);
        }

        return AnswerWhenPhaseEndsAsync(phase, memberId, request.InstanceId);
    }

    /// <summary>
    /// Takes a SyncGroup. Checks, in order: an unknown group (25), a fenced instance (82), an unknown
    /// member (25), another generation (22), a rebalance being prepared (27). The leader's
    /// assignments are handed out; a follower's SyncGroup waits for them.
    /// </summary>
    /// <param name="group">The group id.</param>
    /// <param name="generationId">The generation the member names.</param>
    /// <param name="memberId">The member id.</param>
    /// <param name="instanceId">The group instance id (v3); null for a dynamic member.</param>
    /// <param name="assignments">The leader's assignments, member id and bytes; a follower's are ignored.</param>
    /// <param name="caller">Who asked.</param>
    /// <returns>The answer, completed when the leader's assignments arrive.</returns>
    internal Task<KafkaSyncGroupResult> SyncAsync(
        string group,
        int generationId,
        string memberId,
        string? instanceId,
        IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>> assignments,
        KafkaGroupCaller caller)
    {
        Task<IReadOnlyDictionary<string, ReadOnlyMemory<byte>>?> ready;
        lock (gate)
        {
            var refusal = GenerationRefusal(group, generationId, memberId, instanceId, out var held, out var member);
            if (refusal != KafkaErrorCode.None)
            {
                return Task.FromResult(new KafkaSyncGroupResult(refusal, ReadOnlyMemory<byte>.Empty));
            }

            Refresh(member!);
            switch (held!.State)
            {
                case KafkaGroupState.PreparingRebalance:
                    return Task.FromResult(new KafkaSyncGroupResult(KafkaErrorCode.RebalanceInProgress, ReadOnlyMemory<byte>.Empty));
                case KafkaGroupState.Stable:
                    return Task.FromResult(new KafkaSyncGroupResult(KafkaErrorCode.None, member!.Assignment));
            }

            if (member!.Id == held.LeaderId)
            {
                return Task.FromResult(Distribute(held, member, assignments, caller));
            }

            ready = held.Assignments.Task;
        }

        return AnswerWhenAssignedAsync(ready, memberId);
    }

    /// <summary>
    /// Takes a Heartbeat: an unknown group (25), a fenced instance (82), an unknown member (25),
    /// another generation (22), then <c>REBALANCE_IN_PROGRESS</c> (27) while a rebalance is being
    /// prepared, otherwise 0. Every heartbeat that names the member refreshes its session.
    /// </summary>
    /// <param name="group">The group id.</param>
    /// <param name="generationId">The generation the member names.</param>
    /// <param name="memberId">The member id.</param>
    /// <param name="instanceId">The group instance id (v3); null for a dynamic member.</param>
    /// <returns>The error code answered.</returns>
    internal KafkaErrorCode Heartbeat(string group, int generationId, string memberId, string? instanceId)
    {
        lock (gate)
        {
            var refusal = GenerationRefusal(group, generationId, memberId, instanceId, out var held, out var member);
            if (refusal != KafkaErrorCode.None)
            {
                return refusal;
            }

            Refresh(member!);
            return held!.State == KafkaGroupState.PreparingRebalance ? KafkaErrorCode.RebalanceInProgress : KafkaErrorCode.None;
        }
    }

    /// <summary>
    /// Takes one member's LeaveGroup, by member id, or by group instance id with an empty member id:
    /// an unknown group or member is 25, a member id other than its instance's 82. The group
    /// rebalances, or empties when it was the last member.
    /// </summary>
    /// <param name="group">The group id.</param>
    /// <param name="memberId">The member id; empty to leave by <paramref name="instanceId"/>.</param>
    /// <param name="instanceId">The group instance id (v3); null for a dynamic member.</param>
    /// <param name="caller">Who asked.</param>
    /// <returns>The member's error code.</returns>
    internal KafkaErrorCode Leave(string group, string memberId, string? instanceId, KafkaGroupCaller caller)
    {
        lock (gate)
        {
            groups.TryGetValue(group, out var held);
            var id = memberId.Length > 0 || held is null ? memberId : HolderOf(held, instanceId);
            var refusal = IdentityRefusal(held, id, instanceId, out var member);
            if (refusal != KafkaErrorCode.None)
            {
                return refusal;
            }

            Remove(held!, member!, ConsumerGroupChange.MemberLeft, caller.ExchangeId, caller.Account);
            AfterRemoval(held!, caller);
            return KafkaErrorCode.None;
        }
    }

    private static string HolderOf(Group group, string? instanceId) =>
        instanceId is null ? "" : group.Instances.GetValueOrDefault(instanceId, "");

    private static async Task<KafkaJoinGroupResult> AnswerWhenPhaseEndsAsync(Task<JoinPhase> phase, string memberId, string? instanceId) =>
        (await phase).AnswerFor(memberId, instanceId);

    private static async Task<KafkaSyncGroupResult> AnswerWhenAssignedAsync(Task<IReadOnlyDictionary<string, ReadOnlyMemory<byte>>?> ready, string memberId) =>
        await ready is { } given
            ? new(KafkaErrorCode.None, given.GetValueOrDefault(memberId))
            : new(KafkaErrorCode.RebalanceInProgress, ReadOnlyMemory<byte>.Empty);

    private KafkaErrorCode JoinRefusal(KafkaJoinGroupRequest request, out Group? group)
    {
        groups.TryGetValue(request.Group, out group);
        if (request.SessionTimeoutMs is < MinSessionTimeoutMs or > MaxSessionTimeoutMs)
        {
            return KafkaErrorCode.InvalidSessionTimeout;
        }

        if (request.ProtocolType.Length == 0 || request.Protocols.Count == 0)
        {
            return KafkaErrorCode.InconsistentGroupProtocol;
        }

        var identity = request.MemberId.Length == 0 ? KafkaErrorCode.None : JoinerIdentityRefusal(group, request);
        if (identity != KafkaErrorCode.None)
        {
            return identity;
        }

        var bound = BoundRefusal(group, request);
        return bound != KafkaErrorCode.None ? bound : ProtocolRefusal(group, request);
    }

    // A known joiner names a member or a pending member id, and is not fenced.
    private static KafkaErrorCode JoinerIdentityRefusal(Group? group, KafkaJoinGroupRequest request)
    {
        var refusal = IdentityRefusal(group, request.MemberId, request.InstanceId, out _);
        return refusal == KafkaErrorCode.UnknownMemberId && group?.Pending.ContainsKey(request.MemberId) == true ? KafkaErrorCode.None : refusal;
    }

    // A new member past the group's size 81, a new group past the most groups 44, metadata past the
    // state bound 44.
    private KafkaErrorCode BoundRefusal(Group? group, KafkaJoinGroupRequest request) =>
        group is null ? (groups.Count >= MaxGroups ? KafkaErrorCode.PolicyViolation : BytesRefusal(0, request))
        : IsNewMember(group, request) && group.Members.Count + group.Pending.Count >= MaxMembersPerGroup ? KafkaErrorCode.GroupMaxSizeReached
        : BytesRefusal(group.Find(request.MemberId)?.MetadataBytes ?? 0, request);

    private static bool IsNewMember(Group group, KafkaJoinGroupRequest request) =>
        request.MemberId.Length == 0 && HolderOfOrNull(group, request.InstanceId) is null;

    private KafkaErrorCode BytesRefusal(long heldBytes, KafkaJoinGroupRequest request) =>
        stateBytes - heldBytes + ProtocolBytes(request.Protocols) > MaxGroupStateBytes ? KafkaErrorCode.PolicyViolation : KafkaErrorCode.None;

    private static string? HolderOfOrNull(Group group, string? instanceId) =>
        instanceId is null ? null : group.Instances.GetValueOrDefault(instanceId);

    // The other members must share the protocol type and at least one protocol name.
    private static KafkaErrorCode ProtocolRefusal(Group? group, KafkaJoinGroupRequest request)
    {
        var others = group?.Members.Where(member => !IsSameJoiner(member, request)).ToList() ?? [];
        if (others.Count == 0)
        {
            return KafkaErrorCode.None;
        }

        var common = request.Protocols.Select(protocol => protocol.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var other in others)
        {
            common.IntersectWith(other.Protocols.Select(protocol => protocol.Name));
        }

        return group!.ProtocolType != request.ProtocolType || common.Count == 0 ? KafkaErrorCode.InconsistentGroupProtocol : KafkaErrorCode.None;
    }

    private static bool IsSameJoiner(Member member, KafkaJoinGroupRequest request) =>
        member.Id == request.MemberId || (request.InstanceId is not null && member.InstanceId == request.InstanceId);

    private static long ProtocolBytes(IEnumerable<KafkaGroupProtocol> protocols) => protocols.Sum(protocol => (long)protocol.Metadata.Length);

    // Unknown group 25, fenced instance 82, unknown member 25.
    private static KafkaErrorCode IdentityRefusal(Group? group, string memberId, string? instanceId, out Member? member)
    {
        member = null;
        if (group is null)
        {
            return KafkaErrorCode.UnknownMemberId;
        }

        if (instanceId is not null && group.Instances.TryGetValue(instanceId, out var holder) && holder != memberId)
        {
            return KafkaErrorCode.FencedInstanceId;
        }

        member = group.Find(memberId);
        return member is null ? KafkaErrorCode.UnknownMemberId : KafkaErrorCode.None;
    }

    private KafkaErrorCode GenerationRefusal(string group, int generationId, string memberId, string? instanceId, out Group? held, out Member? member)
    {
        groups.TryGetValue(group, out held);
        var refusal = IdentityRefusal(held, memberId, instanceId, out member);
        return refusal != KafkaErrorCode.None ? refusal
            : generationId != held!.Generation ? KafkaErrorCode.IllegalGeneration
            : KafkaErrorCode.None;
    }

    private Group AddGroup(string id)
    {
        var group = new Group(id);
        group.PhaseTimer = timeProvider.CreateTimer(OnPhaseTimeout, group, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        groups.Add(id, group);
        return group;
    }

    private string AddPending(Group? group, KafkaJoinGroupRequest request, KafkaGroupCaller caller)
    {
        var held = group ?? AddGroup(request.Group);
        var pending = new PendingMember(held, NewMemberId(request.ClientId ?? "", caller.Identifiers));
        pending.Timer = timeProvider.CreateTimer(OnPendingExpired, pending, TimeSpan.FromMilliseconds(request.SessionTimeoutMs), Timeout.InfiniteTimeSpan);
        held.Pending.Add(pending.Id, pending);
        return pending.Id;
    }

    private (Task<JoinPhase> Phase, string MemberId) Join(Group group, KafkaJoinGroupRequest request, KafkaGroupCaller caller)
    {
        group.Events = caller.Events;
        group.Scheme = caller.Scheme;
        var member = request.MemberId.Length == 0 ? AddMember(group, request, caller) : Rejoin(group, request, caller);
        stateBytes += ProtocolBytes(request.Protocols) - member.MetadataBytes;
        member.Update(request, caller);
        if (request.InstanceId is not null)
        {
            group.Instances[request.InstanceId] = member.Id;
        }

        member.SessionTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        group.ProtocolType = request.ProtocolType;
        if (group.State != KafkaGroupState.PreparingRebalance)
        {
            BeginRebalance(group);
        }

        SchedulePhaseEnd(group);
        var phase = group.Phase.Task;
        if (group.Members.TrueForAll(each => each.IsJoinWaiting))
        {
            EndPhase(group, caller);
        }

        return (phase, member.Id);
    }

    // A first join: a static member whose instance another member id holds fences that one.
    private Member AddMember(Group group, KafkaJoinGroupRequest request, KafkaGroupCaller caller)
    {
        if (HolderOfOrNull(group, request.InstanceId) is { } holder)
        {
            Remove(group, group.Find(holder)!, ConsumerGroupChange.MemberFenced, caller.ExchangeId, caller.Account);
        }

        return NewMember(group, NewMemberId(request.InstanceId ?? request.ClientId ?? "", caller.Identifiers), caller);
    }

    // A known member, or a pending one sending back its MEMBER_ID_REQUIRED id.
    private Member Rejoin(Group group, KafkaJoinGroupRequest request, KafkaGroupCaller caller)
    {
        if (group.Pending.Remove(request.MemberId, out var pending))
        {
            pending.Timer.Dispose();
            return NewMember(group, pending.Id, caller);
        }

        return group.Find(request.MemberId)!;
    }

    private Member NewMember(Group group, string id, KafkaGroupCaller caller)
    {
        var member = new Member(id);
        member.SessionTimer = timeProvider.CreateTimer(OnSessionExpired, (group, member), Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        group.Members.Add(member);
        Publish(group, ConsumerGroupChange.MemberJoined, id, caller.ExchangeId, caller.Account);
        return member;
    }

    private static string NewMemberId(string prefix, IIdentifierRandom identifiers)
    {
        Span<byte> bytes = stackalloc byte[16];
        identifiers.NextBytes(bytes);
        var hex = Convert.ToHexStringLower(bytes);
        return $"{prefix}-{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }

    private void Refresh(Member member) =>
        member.SessionTimer.Change(member.IsJoinWaiting ? Timeout.InfiniteTimeSpan : member.SessionTimeout, Timeout.InfiniteTimeSpan);

    private void BeginRebalance(Group group)
    {
        group.State = KafkaGroupState.PreparingRebalance;
        group.Assignments.TrySetResult(null);
        group.Phase = new(TaskCreationOptions.RunContinuationsAsynchronously);
        group.PhaseStartedAt = timeProvider.GetUtcNow();
    }

    private void SchedulePhaseEnd(Group group)
    {
        var deadline = group.PhaseStartedAt + TimeSpan.FromMilliseconds(group.Members.Max(member => member.RebalanceTimeoutMs));
        var due = TimeSpan.FromTicks(Math.Max(0, (deadline - timeProvider.GetUtcNow()).Ticks));
        group.PhaseTimer.Change(due, Timeout.InfiniteTimeSpan);
    }

    private void EndPhase(Group group, KafkaGroupCaller? caller)
    {
        foreach (var absent in group.Members.Where(member => !member.IsJoinWaiting).ToList())
        {
            Remove(group, absent, ConsumerGroupChange.MemberRemoved, null, absent.Account);
        }

        group.PhaseTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        if (group.Members.Count == 0)
        {
            group.Phase.TrySetResult(new JoinPhase(-1, "", "", []));
            Empty(group);
            return;
        }

        group.Generation++;
        var leader = group.Members.Find(member => member.Id == group.LeaderId) ?? group.Members[0];
        group.LeaderId = leader.Id;
        var protocol = leader.Protocols.First(offered => group.Members.TrueForAll(member => member.Lists(offered.Name))).Name;
        foreach (var member in group.Members)
        {
            member.IsJoinWaiting = false;
            member.SessionTimer.Change(member.SessionTimeout, Timeout.InfiniteTimeSpan);
        }

        group.State = KafkaGroupState.CompletingRebalance;
        group.Assignments = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Publish(group, ConsumerGroupChange.GenerationStarted, null, caller?.ExchangeId, caller?.Account);
        group.Phase.TrySetResult(new JoinPhase(
            group.Generation,
            protocol,
            leader.Id,
            [.. group.Members.Select(member => new KafkaJoinedMember(member.Id, member.InstanceId, member.MetadataFor(protocol)))]));
    }

    private KafkaSyncGroupResult Distribute(
        Group group,
        Member leader,
        IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>> assignments,
        KafkaGroupCaller caller)
    {
        var given = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
        foreach (var (memberId, assignment) in assignments.Where(named => group.Find(named.Key) is not null))
        {
            given[memberId] = assignment;
        }

        var added = given.Values.Sum(assignment => (long)assignment.Length) - group.Members.Sum(member => (long)member.Assignment.Length);
        if (stateBytes + added > MaxGroupStateBytes)
        {
            BeginRebalance(group);
            SchedulePhaseEnd(group);
            return new(KafkaErrorCode.PolicyViolation, ReadOnlyMemory<byte>.Empty);
        }

        stateBytes += added;
        foreach (var member in group.Members)
        {
            member.Assignment = given.GetValueOrDefault(member.Id);
        }

        group.State = KafkaGroupState.Stable;
        Publish(group, ConsumerGroupChange.AssignmentDistributed, null, caller.ExchangeId, caller.Account);
        group.Assignments.TrySetResult(given);
        return new(KafkaErrorCode.None, leader.Assignment);
    }

    private void Remove(Group group, Member member, ConsumerGroupChange change, long? exchangeId, string? account)
    {
        member.IsRemoved = true;
        member.SessionTimer.Dispose();
        group.Members.Remove(member);
        group.Instances.Remove(member.InstanceId ?? "");
        stateBytes -= member.MetadataBytes + member.Assignment.Length;
        Publish(group, change, member.Id, exchangeId, account);
    }

    private void AfterRemoval(Group group, KafkaGroupCaller? caller)
    {
        if (group.State == KafkaGroupState.PreparingRebalance)
        {
            if (group.Members.TrueForAll(member => member.IsJoinWaiting))
            {
                EndPhase(group, caller);
            }

            return;
        }

        if (group.Members.Count == 0)
        {
            Empty(group);
            return;
        }

        BeginRebalance(group);
        SchedulePhaseEnd(group);
    }

    private void Empty(Group group)
    {
        group.State = KafkaGroupState.Empty;
        group.ProtocolType = null;
        group.Assignments.TrySetResult(null);
        ForgetIfIdle(group);
    }

    private void ForgetIfIdle(Group group)
    {
        if (group.Members.Count + group.Pending.Count == 0)
        {
            group.PhaseTimer.Dispose();
            groups.Remove(group.Id);
        }
    }

    private void OnSessionExpired(object? state)
    {
        var (group, member) = ((Group, Member))state!;
        lock (gate)
        {
            if (member.IsRemoved)
            {
                return;
            }

            Remove(group, member, ConsumerGroupChange.MemberRemoved, null, member.Account);
            AfterRemoval(group, null);
        }
    }

    private void OnPhaseTimeout(object? state)
    {
        var group = (Group)state!;
        lock (gate)
        {
            if (group.State == KafkaGroupState.PreparingRebalance)
            {
                EndPhase(group, null);
            }
        }
    }

    private void OnPendingExpired(object? state)
    {
        var pending = (PendingMember)state!;
        lock (gate)
        {
            pending.Timer.Dispose();
            pending.Group.Pending.Remove(pending.Id);
            ForgetIfIdle(pending.Group);
        }
    }

    private static void Publish(Group group, ConsumerGroupChange change, string? memberId, long? exchangeId, string? account) =>
        group.Events.Publish(new ConsumerGroupChangedEvent(exchangeId, group.Scheme, group.Id, change, memberId, group.Generation, account));

    // One group's membership, in join order.
    private sealed class Group(string id)
    {
        public string Id { get; } = id;

        public List<Member> Members { get; } = [];

        public Dictionary<string, PendingMember> Pending { get; } = new(StringComparer.Ordinal);

        // Which member id holds each group instance id.
        public Dictionary<string, string> Instances { get; } = new(StringComparer.Ordinal);

        public KafkaGroupState State { get; set; } = KafkaGroupState.Empty;

        public int Generation { get; set; }

        public string? LeaderId { get; set; }

        public string? ProtocolType { get; set; }

        public ISurlEventPublisher Events { get; set; } = DiscardingSurlEventPublisher.Instance;

        public string Scheme { get; set; } = "";

        public ITimer PhaseTimer { get; set; } = null!;

        public DateTimeOffset PhaseStartedAt { get; set; }

        public TaskCompletionSource<JoinPhase> Phase { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Completed with the leader's assignments, or with null when a rebalance begins first.
        public TaskCompletionSource<IReadOnlyDictionary<string, ReadOnlyMemory<byte>>?> Assignments { get; set; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Member? Find(string memberId) => Members.Find(member => member.Id == memberId);
    }

    // One member of a group.
    private sealed class Member(string id)
    {
        public string Id { get; } = id;

        public string? InstanceId { get; private set; }

        public string? Account { get; private set; }

        public IReadOnlyList<KafkaGroupProtocol> Protocols { get; private set; } = [];

        public long MetadataBytes { get; private set; }

        public TimeSpan SessionTimeout { get; private set; }

        public int RebalanceTimeoutMs { get; private set; }

        public ReadOnlyMemory<byte> Assignment { get; set; }

        public bool IsJoinWaiting { get; set; }

        public bool IsRemoved { get; set; }

        public ITimer SessionTimer { get; set; } = null!;

        public void Update(KafkaJoinGroupRequest request, KafkaGroupCaller caller)
        {
            InstanceId = request.InstanceId;
            Account = caller.Account;
            Protocols = request.Protocols;
            MetadataBytes = ProtocolBytes(request.Protocols);
            SessionTimeout = TimeSpan.FromMilliseconds(request.SessionTimeoutMs);
            RebalanceTimeoutMs = request.RebalanceTimeoutMs;
            IsJoinWaiting = true;
        }

        public bool Lists(string protocol) => Protocols.Any(offered => offered.Name == protocol);

        public ReadOnlyMemory<byte> MetadataFor(string protocol) => Protocols.First(offered => offered.Name == protocol).Metadata;
    }

    // A MEMBER_ID_REQUIRED id waiting to be sent back within the session timeout.
    private sealed class PendingMember(Group group, string id)
    {
        public Group Group { get; } = group;

        public string Id { get; } = id;

        public ITimer Timer { get; set; } = null!;
    }

    // What every JoinGroup held in one join phase is answered from.
    private sealed record JoinPhase(int Generation, string Protocol, string Leader, IReadOnlyList<KafkaJoinedMember> Members)
    {
        public KafkaJoinGroupResult AnswerFor(string memberId, string? instanceId) =>
            Members.Any(member => member.MemberId == memberId)
                ? new(KafkaErrorCode.None, Generation, Protocol, Leader, memberId, memberId == Leader ? Members : [])
                : KafkaJoinGroupResult.Failed(instanceId is null ? KafkaErrorCode.UnknownMemberId : KafkaErrorCode.FencedInstanceId, memberId);
    }
}
