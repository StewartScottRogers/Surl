namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// One subscription of a session as a snapshot keeps it (ADR-0159 decision 2): its Topic Filter and options, and the
/// Subscription Identifier it was made with.
/// </summary>
/// <param name="Options">The Topic Filter and its Subscription Options.</param>
/// <param name="Identifier">The <c>SUBSCRIBE</c>'s Subscription Identifier, or <see langword="null"/>.</param>
internal sealed record Mqtt5SubscriptionCopy(Mqtt5Subscription Options, uint? Identifier);
