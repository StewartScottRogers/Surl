using System.Diagnostics.CodeAnalysis;

namespace Surl.MessageBus;

/// <summary>
/// The result of mapping an AMQP 1.0 link address or a STOMP destination onto the bus
/// (ADR-0100, decision 3.2).
/// </summary>
/// <param name="Outcome"><see cref="TopicNameMappingOutcome.Mapped"/>,
/// <see cref="TopicNameMappingOutcome.UnknownDestination"/> for an address with neither
/// prefix, or <see cref="TopicNameMappingOutcome.InvalidName"/> for a prefix followed by a
/// name the bus cannot hold.</param>
/// <param name="Destination">The destination; <see langword="null"/> unless mapped.</param>
public sealed record BusDestinationMapping(TopicNameMappingOutcome Outcome, BusDestination? Destination)
{
    /// <summary>Whether <see cref="Destination"/> holds the mapped destination.</summary>
    [MemberNotNullWhen(true, nameof(Destination))]
    public bool IsMapped => Outcome == TopicNameMappingOutcome.Mapped;
}
