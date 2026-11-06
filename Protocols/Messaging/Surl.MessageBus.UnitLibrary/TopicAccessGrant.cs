namespace Surl.MessageBus;

/// <summary>
/// One member's rights in a <c>--topic-access &lt;filter&gt;=&lt;member&gt;:&lt;pub|sub|pubsub&gt;</c>
/// row (ADR-0102, decision 3.4), its groups already expanded to account names (decision 4.1).
/// Once any grant is configured, every publish and subscribe needs one.
/// </summary>
public sealed record TopicAccessGrant
{
    /// <summary>The reserved member meaning every caller, logged in or anonymous (ADR-0102,
    /// decision 4.2).</summary>
    public const string Everyone = "@everyone";

    /// <summary>
    /// Grants <paramref name="rights"/> on <paramref name="filter"/> to <paramref name="member"/>.
    /// </summary>
    /// <param name="filter">A canonical filter; a queue is named <c>$queue/&lt;name&gt;</c>.</param>
    /// <param name="member">An account name, or <see cref="Everyone"/>.</param>
    /// <param name="rights">What the member may do: <see cref="TopicAccessRights.Publish"/>,
    /// <see cref="TopicAccessRights.Subscribe"/> or <see cref="TopicAccessRights.PublishAndSubscribe"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="filter"/> is not a canonical filter,
    /// <paramref name="member"/> is empty, or <paramref name="rights"/> is none of the three.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="member"/> is <see langword="null"/>.</exception>
    public TopicAccessGrant(string filter, string member, TopicAccessRights rights)
    {
        if (!CanonicalTopicName.IsValidFilter(filter))
        {
            throw new ArgumentException($"'{filter}' is not a canonical topic filter.", nameof(filter));
        }

        ArgumentException.ThrowIfNullOrEmpty(member);
        if (rights is not (TopicAccessRights.Publish or TopicAccessRights.Subscribe or TopicAccessRights.PublishAndSubscribe))
        {
            throw new ArgumentException("A grant grants pub, sub or pubsub.", nameof(rights));
        }

        Filter = filter;
        Member = member;
        Rights = rights;
    }

    /// <summary>The canonical filter the grant covers.</summary>
    public string Filter { get; }

    /// <summary>The account name, or <see cref="Everyone"/>.</summary>
    public string Member { get; }

    /// <summary>What the member may do.</summary>
    public TopicAccessRights Rights { get; }
}
