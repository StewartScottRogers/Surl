namespace Surl.MessageBus;

/// <summary>
/// STOMP 1.2 destinations on the bus (ADR-0100, decision 3.2; STOMP 1.2 "SEND", "SUBSCRIBE"):
/// <c>/topic/&lt;t&gt;</c> is canonical topic <c>&lt;t&gt;</c> verbatim, and a SUBSCRIBE may
/// hold <c>+</c> and <c>#</c>; <c>/queue/&lt;q&gt;</c> is bus queue <c>&lt;q&gt;</c>. Any other
/// destination, or a <c>&lt;t&gt;</c> that is not canonical, is refused, and the server sends
/// an <c>ERROR</c> frame and closes.
/// </summary>
public static class StompDestinationNames
{
    /// <summary>
    /// Maps a SEND frame's destination onto the bus.
    /// </summary>
    /// <param name="destination">The destination header's value.</param>
    /// <returns>A topic or queue; <see cref="TopicNameMappingOutcome.UnknownDestination"/> for a
    /// destination with neither prefix; or <see cref="TopicNameMappingOutcome.InvalidName"/> when
    /// the rest is no canonical topic name or queue name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    public static BusDestinationMapping ToPublishDestination(string destination) =>
        SlashPrefixedDestinations.ToBusDestination(destination, topicIsFilter: false);

    /// <summary>
    /// Maps a SUBSCRIBE frame's destination onto the bus.
    /// </summary>
    /// <param name="destination">The destination header's value.</param>
    /// <returns>A topic filter or queue; <see cref="TopicNameMappingOutcome.UnknownDestination"/>
    /// for a destination with neither prefix; or <see cref="TopicNameMappingOutcome.InvalidName"/>
    /// when the rest is no canonical filter or queue name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    public static BusDestinationMapping ToSubscribeDestination(string destination) =>
        SlashPrefixedDestinations.ToBusDestination(destination, topicIsFilter: true);

    /// <summary>
    /// Maps a bus destination onto the destination header a MESSAGE frame carries.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <returns><c>/topic/&lt;name&gt;</c> or <c>/queue/&lt;name&gt;</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    public static string ToDestination(BusDestination destination) => SlashPrefixedDestinations.ToAddress(destination);
}
