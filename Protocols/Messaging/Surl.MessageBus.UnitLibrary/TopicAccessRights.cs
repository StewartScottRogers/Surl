namespace Surl.MessageBus;

/// <summary>
/// What a <see cref="TopicAccessGrant"/> lets its member do (ADR-0102, decision 3.4):
/// <c>pub</c>, <c>sub</c> or <c>pubsub</c>.
/// </summary>
[Flags]
public enum TopicAccessRights
{
    /// <summary>Nothing; no grant holds this alone.</summary>
    None = 0,

    /// <summary><c>pub</c>: publish on a topic the grant's filter matches, or send to the
    /// <c>$queue/&lt;name&gt;</c> queue it names.</summary>
    Publish = 1,

    /// <summary><c>sub</c>: subscribe to a filter the grant's filter covers, bind a queue to
    /// one, or consume from the <c>$queue/&lt;name&gt;</c> queue it names.</summary>
    Subscribe = 2,

    /// <summary><c>pubsub</c>: both.</summary>
    PublishAndSubscribe = Publish | Subscribe,
}
