namespace Surl.MessageBus;

/// <summary>
/// One filter of a <see cref="BusConnection.SubscribeTogether"/>.
/// </summary>
/// <param name="Filter">A canonical topic filter (<see cref="CanonicalTopicName.IsValidFilter"/>).
/// Not read when <paramref name="Held"/> is given.</param>
/// <param name="Held">The connection's live subscription to the filter when it already has one: it
/// is kept, no second one is made, and the retained messages it matches are read again.
/// <see langword="null"/> makes a new subscription.</param>
public sealed record BusSubscribeRequest(string Filter, BusSubscription? Held = null);
