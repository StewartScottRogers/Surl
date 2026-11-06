namespace Surl.MessageBus;

/// <summary>
/// Whether a protocol's name was mapped onto the bus's canonical topic space, or onto the
/// protocol's names from it, and when not, why (ADR-0100, decision 3.2's "Not mappable"
/// column). Each server words a refusal in its own protocol.
/// </summary>
public enum TopicNameMappingOutcome
{
    /// <summary>The name was mapped.</summary>
    Mapped,

    /// <summary>The name is not one the mapping accepts: invalid in its own protocol, or holding
    /// a character the canonical space reserves (a NATS token or AMQP 0-9-1 word holding
    /// <c>/</c>, <c>+</c> or <c>#</c>, a Kafka topic outside <c>[A-Za-z0-9._-]{1,249}</c>, a
    /// <c>/topic/</c> address whose rest is not canonical).</summary>
    InvalidName,

    /// <summary>A canonical name or filter the protocol cannot name (an empty level or a
    /// <c>.</c> in a level toward NATS, AMQP 0-9-1 or Kafka, and the like): nobody on that
    /// protocol can have subscribed to it, so nothing is delivered there.</summary>
    Unreachable,

    /// <summary>An AMQP 1.0 link address or STOMP destination that begins with neither
    /// <c>/topic/</c> nor <c>/queue/</c>.</summary>
    UnknownDestination,

    /// <summary>A Kafka partition other than 0: every bus topic has exactly one.</summary>
    UnknownPartition,
}
