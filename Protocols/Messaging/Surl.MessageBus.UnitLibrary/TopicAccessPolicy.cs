namespace Surl.MessageBus;

/// <summary>
/// The <c>--topic-access</c> checks (ADR-0102, decision 3.4). With no grant every caller may
/// do everything; with at least one, a caller may do only what a grant to its account, or to
/// <see cref="TopicAccessGrant.Everyone"/>, allows. An anonymous caller has no account. The
/// answer never depends on whether a topic or queue exists (ADR-0006, section 3). Immutable.
/// </summary>
internal sealed class TopicAccessPolicy(IReadOnlyList<TopicAccessGrant> grants)
{
    private const string QueueLevel = "$queue";
    private const string GroupLevel = "$group";

    private readonly (TopicAccessGrant Grant, string[] Levels)[] rows =
        [.. grants.Select(grant => (grant, CanonicalTopicName.SplitLevels(grant.Filter)))];

    /// <summary>Whether a publish on canonical topic <paramref name="topic"/> is allowed: some
    /// <c>pub</c> grant's filter matches it.</summary>
    public bool MayPublish(string? account, string topic) =>
        IsAllowed(account, TopicAccessRights.Publish, levels => CanonicalTopicName.FilterMatches(levels, CanonicalTopicName.SplitLevels(topic)));

    /// <summary>Whether a subscribe to, or a queue binding with, canonical filter
    /// <paramref name="filter"/> is allowed: some <c>sub</c> grant's filter covers it.</summary>
    public bool MaySubscribe(string? account, string filter) =>
        IsAllowed(account, TopicAccessRights.Subscribe, levels => CanonicalTopicName.FilterCovers(levels, CanonicalTopicName.SplitLevels(filter)));

    /// <summary>Whether <paramref name="rights"/> on queue <paramref name="queueName"/> are
    /// allowed: some grant's filter matches <c>$queue/&lt;name&gt;</c>, the name one level
    /// whatever it holds.</summary>
    public bool MayUseQueue(string? account, TopicAccessRights rights, string queueName) =>
        IsAllowed(account, rights, levels => CanonicalTopicName.FilterMatches(levels, [QueueLevel, queueName]));

    /// <summary>Whether consumer group <paramref name="group"/> may be used (ADR-0145, decision 6):
    /// some <c>sub</c> grant's filter matches <c>$group/&lt;id&gt;</c>, the id one level whatever
    /// it holds.</summary>
    public bool MayUseGroup(string? account, string group) =>
        IsAllowed(account, TopicAccessRights.Subscribe, levels => CanonicalTopicName.FilterMatches(levels, [GroupLevel, group]));

    private bool IsAllowed(string? account, TopicAccessRights rights, Func<string[], bool> grantApplies) =>
        rows.Length == 0
        || rows.Any(row => row.Grant.Rights.HasFlag(rights) && IsGrantedTo(row.Grant, account) && grantApplies(row.Levels));

    private static bool IsGrantedTo(TopicAccessGrant grant, string? account) =>
        grant.Member == TopicAccessGrant.Everyone || string.Equals(grant.Member, account, StringComparison.Ordinal);
}
