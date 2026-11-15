namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// An opaque copy of the sessions one <see cref="Mqtt5Broker"/> keeps with no connection, with their subscriptions
/// and no queued delivery (ADR-0159 decision 2). Only the broker that took it restores it.
/// </summary>
public sealed class Mqtt5SessionSnapshot
{
    internal Mqtt5SessionSnapshot(object owner, IReadOnlyList<Mqtt5SessionCopy> sessions)
    {
        Owner = owner;
        Sessions = sessions;
        ByteCount = sessions.Sum(session => session.ByteCount);
    }

    /// <summary>The bytes the copy holds: identifiers, accounts and Topic Filters.</summary>
    public long ByteCount { get; }

    internal object Owner { get; }

    internal IReadOnlyList<Mqtt5SessionCopy> Sessions { get; }
}
