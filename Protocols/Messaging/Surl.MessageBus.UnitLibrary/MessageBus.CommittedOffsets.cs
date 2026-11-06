using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.MessageBus;

/// <summary>
/// The committed offsets the bus keeps for Kafka consumer groups (ADR-0145, decisions 0, 4, 6, 9
/// and 10): a bounded table keyed by group id and canonical topic, which only the Kafka broker
/// reads or writes. It is not a message: no subscription, queue or log sees it. It is reset,
/// snapshotted, restored and persisted with the rest of the bus, so an offset never outlives the
/// log it points into.
/// </summary>
public sealed partial class MessageBus
{
    /// <summary>The default for <see cref="MaxCommittedOffsets"/>: 10000 (ADR-0145, decision 9).</summary>
    public const int DefaultMaxCommittedOffsets = 10_000;

    /// <summary>
    /// What every committed offset's change name starts with: <c>$group/</c>, the form the access
    /// grants name a group by (ADR-0145, decision 6).
    /// </summary>
    public const string GroupChangeNamePrefix = "$group/";

    /// <summary>The bytes every committed offset counts against a snapshot beyond its strings:
    /// its offset (8), leader epoch (4) and commit time (8).</summary>
    internal const int CommittedOffsetOverheadBytes = 20;

    private readonly Dictionary<(string Group, string Topic), BusCommittedOffset> committedOffsets = [];
    private long committedOffsetsVersion;
    private long savedCommittedOffsetsVersion;

    /// <summary>The most committed-offset entries, all groups together (ADR-0145, decision 9); a
    /// commit making one more is <see cref="MessageBusOutcome.StoreFull"/>, a replacement is always taken.</summary>
    public int MaxCommittedOffsets { get; }

    /// <summary>
    /// The name a commit's <see cref="StoreChange.Committed"/> change is published under, and the
    /// path an out-of-space rule names it by: <c>$group/&lt;group&gt;/&lt;topic&gt;</c>, with
    /// <c>%</c> and <c>/</c> in the group id written <c>%25</c> and <c>%2F</c>, so the name splits
    /// one way (ADR-0145, decision 10).
    /// </summary>
    /// <param name="group">The consumer group's id.</param>
    /// <param name="topic">The canonical topic name.</param>
    /// <returns>The change name.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static string CommittedChangeName(string group, string topic)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(topic);
        var encodedGroup = group.Replace("%", "%25", StringComparison.Ordinal).Replace("/", "%2F", StringComparison.Ordinal);
        return string.Concat(GroupChangeNamePrefix, encodedGroup, "/", topic);
    }

    /// <summary>
    /// Whether <paramref name="account"/> may use consumer group <paramref name="group"/> under the
    /// configured access grants (ADR-0145, decision 6): some <c>sub</c> grant's filter matches
    /// <c>$group/&lt;group&gt;</c>, the group id one level whatever it holds, and a wildcard first
    /// level never matching it. With no grants every caller may use every group. The answer never
    /// depends on whether the group exists.
    /// </summary>
    /// <param name="account">The caller's account, or <see langword="null"/> for an anonymous one.</param>
    /// <param name="group">The consumer group's id.</param>
    /// <returns>Whether the caller may use the group.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="group"/> is <see langword="null"/>.</exception>
    public bool MayUseGroup(string? account, string group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return accessPolicy.MayUseGroup(account, group);
    }

    /// <summary>
    /// Commits <paramref name="offset"/> for <paramref name="group"/> on <paramref name="topic"/>,
    /// replacing what the group committed there before, stamped with the bus's clock, and publishes
    /// one <see cref="StoreChange.Committed"/> named <see cref="CommittedChangeName"/>.
    /// </summary>
    /// <param name="account">The committer's account, or <see langword="null"/> for an anonymous one.</param>
    /// <param name="group">The consumer group's id.</param>
    /// <param name="topic">The canonical topic name.</param>
    /// <param name="offset">The offset, kept as given.</param>
    /// <param name="leaderEpoch">The leader epoch, -1 when none was given.</param>
    /// <param name="metadata">The metadata, <see langword="null"/> kept null.</param>
    /// <returns><see cref="MessageBusOutcome.Succeeded"/>; <see cref="MessageBusOutcome.InvalidTopic"/>
    /// for a topic that is not canonical; <see cref="MessageBusOutcome.AccessDenied"/> without
    /// <c>sub</c> on the topic or on <c>$group/&lt;group&gt;</c>; or <see cref="MessageBusOutcome.StoreFull"/>
    /// when an out-of-space rule matches the change name, or a new entry would pass
    /// <see cref="MaxCommittedOffsets"/>. Nothing changes on a refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="group"/> or <paramref name="topic"/>
    /// is <see langword="null"/>.</exception>
    public MessageBusOutcome CommitOffset(string? account, string group, string topic, long offset, int leaderEpoch, string? metadata)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(topic);
        var changeName = CommittedChangeName(group, topic);
        var refusal = CommitRefusal(account, group, topic, changeName);
        if (refusal is not MessageBusOutcome.Succeeded)
        {
            return refusal;
        }

        var entry = new BusCommittedOffset(group, topic, offset, leaderEpoch, metadata, timeProvider.GetUtcNow());
        lock (stateLock)
        {
            if (!committedOffsets.ContainsKey((group, topic)) && committedOffsets.Count >= MaxCommittedOffsets)
            {
                return MessageBusOutcome.StoreFull;
            }

            committedOffsets[(group, topic)] = entry;
            committedOffsetsVersion++;
            PublishChange(StoreChange.Committed, changeName, byteCount: null);
            return MessageBusOutcome.Succeeded;
        }
    }

    /// <summary>What refuses a commit before the table is looked at: the topic, the access grants
    /// and an out-of-space rule, in that order; <see cref="MessageBusOutcome.Succeeded"/> for none.
    /// Called outside the bus's lock, since the fault plan may run a test's code.</summary>
    private MessageBusOutcome CommitRefusal(string? account, string group, string topic, string changeName) =>
        !CanonicalTopicName.IsValidTopic(topic) ? MessageBusOutcome.InvalidTopic
        : !accessPolicy.MaySubscribe(account, topic) || !accessPolicy.MayUseGroup(account, group) ? MessageBusOutcome.AccessDenied
        : IsOutOfSpace(account, changeName) ? MessageBusOutcome.StoreFull
        : MessageBusOutcome.Succeeded;

    /// <summary>
    /// What <paramref name="group"/> last committed on <paramref name="topic"/>. Checks no access:
    /// the caller asks <see cref="MayUseGroup"/> and <see cref="GetTopicAccess"/> first.
    /// </summary>
    /// <param name="group">The consumer group's id.</param>
    /// <param name="topic">The canonical topic name.</param>
    /// <returns>The entry, or <see langword="null"/> when none is committed.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public BusCommittedOffset? GetCommittedOffset(string group, string topic)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(topic);
        lock (stateLock)
        {
            return committedOffsets.GetValueOrDefault((group, topic));
        }
    }

    /// <summary>
    /// Every entry <paramref name="group"/> holds, in ordinal order of topic, as OffsetFetch with
    /// null topics answers them.
    /// </summary>
    /// <param name="group">The consumer group's id.</param>
    /// <returns>The entries; empty when the group has committed nothing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="group"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<BusCommittedOffset> GetCommittedOffsets(string group)
    {
        ArgumentNullException.ThrowIfNull(group);
        lock (stateLock)
        {
            return [.. committedOffsets.Values
                .Where(entry => string.Equals(entry.Group, group, StringComparison.Ordinal))
                .OrderBy(entry => entry.Topic, StringComparer.Ordinal)];
        }
    }

    /// <summary>
    /// Every group that holds a committed offset, in ordinal order, as ListGroups lists the groups
    /// known only by their offsets.
    /// </summary>
    /// <returns>The group ids.</returns>
    public IReadOnlyList<string> GetCommittedGroups()
    {
        lock (stateLock)
        {
            return [.. committedOffsets.Keys.Select(key => key.Group).Distinct().Order(StringComparer.Ordinal)];
        }
    }

    /// <summary>The bytes one entry counts against a snapshot: the UTF-8 of its group, topic and
    /// metadata, and <see cref="CommittedOffsetOverheadBytes"/>.</summary>
    internal static long CommittedOffsetBytes(BusCommittedOffset entry) =>
        CommittedOffsetOverheadBytes
        + Encoding.UTF8.GetByteCount(entry.Group)
        + Encoding.UTF8.GetByteCount(entry.Topic)
        + Encoding.UTF8.GetByteCount(entry.Metadata ?? string.Empty);

    /// <summary>Makes the table hold exactly <paramref name="entries"/>. Called with the lock held.</summary>
    private void ReplaceCommittedOffsets(IEnumerable<BusCommittedOffset> entries)
    {
        committedOffsets.Clear();
        foreach (var entry in entries)
        {
            committedOffsets[(entry.Group, entry.Topic)] = entry;
        }

        committedOffsetsVersion++;
    }
}
