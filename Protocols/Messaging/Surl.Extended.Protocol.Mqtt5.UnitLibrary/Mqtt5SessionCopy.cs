namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// One session with no connection as a snapshot keeps it (ADR-0159 decision 2): who it belongs to and its
/// subscriptions, with no queued delivery and nothing in flight.
/// </summary>
/// <param name="ClientIdentifier">The client identifier.</param>
/// <param name="Account">The account the session subscribes as; <see langword="null"/> for an anonymous client.</param>
/// <param name="Scheme">The scheme of the connection that opened it.</param>
/// <param name="Expiry">The Session Expiry Interval in force.</param>
/// <param name="Clock">The clock its expiry is measured on.</param>
/// <param name="Subscriptions">Its subscriptions.</param>
internal sealed record Mqtt5SessionCopy(
    string ClientIdentifier,
    string? Account,
    string Scheme,
    TimeSpan Expiry,
    TimeProvider Clock,
    IReadOnlyList<Mqtt5SubscriptionCopy> Subscriptions)
{
    /// <summary>The bytes the copy holds: the identifier, the account, and each subscription's Topic Filter.</summary>
    public long ByteCount =>
        ClientIdentifier.Length + (Account?.Length ?? 0) + Subscriptions.Sum(subscription => subscription.Options.TopicFilter.Length);
}
