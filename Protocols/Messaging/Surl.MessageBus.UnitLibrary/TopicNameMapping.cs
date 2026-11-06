using System.Diagnostics.CodeAnalysis;

namespace Surl.MessageBus;

/// <summary>
/// The result of mapping one topic name or filter between a protocol and the bus's canonical
/// topic space (ADR-0100, decision 3.2).
/// </summary>
/// <param name="Outcome">Whether it was mapped, and when not, why.</param>
/// <param name="Name">The mapped name or filter; <see langword="null"/> unless
/// <see cref="Outcome"/> is <see cref="TopicNameMappingOutcome.Mapped"/>.</param>
/// <param name="Group">The subscription's fan-out group (<see cref="BusSubscription.Group"/>):
/// an MQTT 5 shared subscription's share name; otherwise <see langword="null"/>.</param>
public sealed record TopicNameMapping(TopicNameMappingOutcome Outcome, string? Name, string? Group = null)
{
    /// <summary>Whether <see cref="Name"/> holds the mapped name.</summary>
    [MemberNotNullWhen(true, nameof(Name))]
    public bool IsMapped => Outcome == TopicNameMappingOutcome.Mapped;

    internal static TopicNameMapping Of(string name) => new(TopicNameMappingOutcome.Mapped, name);

    internal static TopicNameMapping Refused(TopicNameMappingOutcome outcome) => new(outcome, null);
}
