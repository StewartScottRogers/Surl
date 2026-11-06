using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// The points a <see cref="FaultPlan"/> override is asked at (ADR-0124, decision 5): every attribute given
/// must match the point as a <see cref="FaultRule"/>'s does, and an attribute left <see langword="null"/>
/// matches anything, <see cref="Stage"/> included.
/// </summary>
public sealed record FaultOverrideScope
{
    /// <summary>The listen URL, matched as <see cref="FaultRule.Listener"/> is: a port of 0 matches any bound port.</summary>
    public string? Listener { get; init; }

    /// <summary>The scheme, case-insensitive.</summary>
    public string? Scheme { get; init; }

    /// <summary>The account, ordinally.</summary>
    public string? Account { get; init; }

    /// <summary>The path, equal or as a segment prefix.</summary>
    public string? Path { get; init; }

    /// <summary>The protocol's verb, case-insensitive.</summary>
    public string? Command { get; init; }

    /// <summary>The stage; <see langword="null"/> asks the override at every stage.</summary>
    public FaultStage? Stage { get; init; }
}
