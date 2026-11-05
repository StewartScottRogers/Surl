namespace Surl.MessageBus;

/// <summary>
/// What an AMQP 1.0 link address or a STOMP destination names on the bus (ADR-0100,
/// decision 3.2): a topic of the shared topic space, or a named queue (decision 3.3).
/// </summary>
public enum BusDestinationKind
{
    /// <summary><c>/topic/&lt;t&gt;</c>: canonical topic name or filter <c>&lt;t&gt;</c>.</summary>
    Topic,

    /// <summary><c>/queue/&lt;q&gt;</c>: the bus queue named <c>&lt;q&gt;</c>.</summary>
    Queue,
}
