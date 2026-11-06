using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// The points a response override (<see cref="SurlServerFaults.AddOverride"/>) is asked at (ADR-0124, decision 5):
/// every property given must match the point as a <see cref="SurlFault"/>'s scope does, and a property left
/// <see langword="null"/> matches anything, <see cref="Stage"/> included.
/// </summary>
/// <example>
/// <code>
/// // Ask at every HTTP request for a path under /reports.
/// var scope = new SurlFaultScope { Scheme = "http", Path = "/reports", Stage = FaultStage.Command };
/// </code>
/// </example>
public sealed record SurlFaultScope
{
    /// <summary>The listen URL, as <c>surl</c> takes one; a port of 0 matches any bound port. Not given together with <see cref="Scheme"/>.</summary>
    public string? Listener { get; init; }

    /// <summary>The scheme, case-insensitive. Not given together with <see cref="Listener"/>.</summary>
    public string? Scheme { get; init; }

    /// <summary>The account logged in or logging in, case-sensitive.</summary>
    public string? Account { get; init; }

    /// <summary>The path, mailbox, topic, queue, selector or DN: equal, or as a segment prefix.</summary>
    public string? Path { get; init; }

    /// <summary>The protocol's verb (<c>GET</c>, <c>RETR</c>), case-insensitive.</summary>
    public string? Command { get; init; }

    /// <summary>The stage; <see langword="null"/> asks the override at every stage.</summary>
    public FaultStage? Stage { get; init; }
}
