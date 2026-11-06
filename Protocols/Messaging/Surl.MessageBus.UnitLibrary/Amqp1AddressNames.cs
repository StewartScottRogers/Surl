namespace Surl.MessageBus;

/// <summary>
/// AMQP 1.0 link addresses on the bus (ADR-0100, decision 3.2; AMQP 1.0 part 3 sections 3.5.3
/// and 3.5.4): <c>/topic/&lt;t&gt;</c> is canonical topic <c>&lt;t&gt;</c> verbatim, and a
/// source (a receiver's link) may hold <c>+</c> and <c>#</c>; <c>/queue/&lt;q&gt;</c> is bus
/// queue <c>&lt;q&gt;</c>. Any other address, or a <c>&lt;t&gt;</c> that is not canonical, is
/// refused, and the server refuses the <c>attach</c> with <c>amqp:not-found</c>.
/// </summary>
public static class Amqp1AddressNames
{
    /// <summary>
    /// Maps a target address (a sender's link, which publishes) onto the bus.
    /// </summary>
    /// <param name="address">The target address.</param>
    /// <returns>A topic or queue; <see cref="TopicNameMappingOutcome.UnknownDestination"/> for an
    /// address with neither prefix; or <see cref="TopicNameMappingOutcome.InvalidName"/> when the
    /// rest is no canonical topic name or queue name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is <see langword="null"/>.</exception>
    public static BusDestinationMapping ToPublishDestination(string address) =>
        SlashPrefixedDestinations.ToBusDestination(address, topicIsFilter: false);

    /// <summary>
    /// Maps a source address (a receiver's link, which subscribes or consumes) onto the bus.
    /// </summary>
    /// <param name="address">The source address.</param>
    /// <returns>A topic filter or queue; <see cref="TopicNameMappingOutcome.UnknownDestination"/>
    /// for an address with neither prefix; or <see cref="TopicNameMappingOutcome.InvalidName"/>
    /// when the rest is no canonical filter or queue name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is <see langword="null"/>.</exception>
    public static BusDestinationMapping ToSubscribeDestination(string address) =>
        SlashPrefixedDestinations.ToBusDestination(address, topicIsFilter: true);

    /// <summary>
    /// Maps a bus destination onto its AMQP 1.0 address, as a message's <c>to</c> property and a
    /// link's address carry it.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <returns><c>/topic/&lt;name&gt;</c> or <c>/queue/&lt;name&gt;</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    public static string ToAddress(BusDestination destination) => SlashPrefixedDestinations.ToAddress(destination);
}
