namespace Surl.MessageBus;

/// <summary>
/// A place on the bus an AMQP 1.0 link address or a STOMP destination names (ADR-0100,
/// decision 3.2).
/// </summary>
/// <param name="Kind">A topic or a queue.</param>
/// <param name="Name">The canonical topic name or filter, or the queue's name.</param>
public sealed record BusDestination(BusDestinationKind Kind, string Name);
