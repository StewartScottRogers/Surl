namespace Surl.Protocol.Abstractions;

/// <summary>
/// One fault rule: its identifier, its kind, the scope it matches and its parameters
/// (ADR-0124, decision 4; the <c>--fault</c> item's keys). A scope attribute left
/// <see langword="null"/> matches anything; a parameter left <see langword="null"/> is not given.
/// </summary>
/// <param name="Id">The rule's identifier: <c>f1</c>, <c>f2</c> ... per instance, never reused.</param>
/// <param name="Kind">What the rule makes the server do.</param>
public sealed record FaultRule(string Id, FaultKind Kind)
{
    /// <summary>
    /// The listen URL the rule is scoped to (<c>listener</c>).
    /// </summary>
    public string? Listener { get; init; }

    /// <summary>
    /// The scheme the rule is scoped to (<c>scheme</c>).
    /// </summary>
    public string? Scheme { get; init; }

    /// <summary>
    /// The account the rule is scoped to (<c>account</c>).
    /// </summary>
    public string? Account { get; init; }

    /// <summary>
    /// The path, mailbox, topic, queue, selector or DN the rule is scoped to (<c>path</c>).
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// The protocol verb the rule is scoped to (<c>command</c>).
    /// </summary>
    public string? Command { get; init; }

    /// <summary>
    /// The stage a <see cref="FaultKind.Delay"/> or <see cref="FaultKind.Drop"/> rule acts at
    /// (<c>stage</c>); <see langword="null"/> means its default, <see cref="FaultStage.Accept"/>.
    /// </summary>
    public FaultStage? Stage { get; init; }

    /// <summary>
    /// How long a <see cref="FaultKind.Delay"/> rule waits, 0 to 600000 milliseconds (<c>ms</c>).
    /// </summary>
    public int? DelayMilliseconds { get; init; }

    /// <summary>
    /// How many bytes a <see cref="FaultKind.Drop"/> rule lets the server send before it drops the
    /// connection (<c>after-bytes</c>).
    /// </summary>
    public long? AfterBytes { get; init; }

    /// <summary>
    /// How a <see cref="FaultKind.Drop"/> rule ends the connection (<c>close</c>); defaults to
    /// <see cref="FaultClose.Reset"/> (ADR-0188, decision 1). The serving engine reads it for an
    /// <see cref="FaultStage.Accept"/>-stage drop with <see cref="AfterBytes"/> on a connection, and on a datagram
    /// flow of an <see cref="IGracefulCutDatagramProtocolServer"/> on a listen URL it closes gracefully (HTTP/3,
    /// DNS over QUIC, WebTransport); every other datagram flow ignores it.
    /// </summary>
    public FaultClose Close { get; init; } = FaultClose.Reset;

    /// <summary>
    /// The chosen error an <see cref="FaultKind.Error"/> rule answers with, in its scheme's error shape
    /// (<c>reply</c>).
    /// </summary>
    public string? Reply { get; init; }

    /// <summary>
    /// The text an <see cref="FaultKind.Error"/> rule makes the HTTP server answer with (<c>body</c>);
    /// <see langword="null"/> keeps the status's fixed text (ADR-0186 decision 5).
    /// </summary>
    public string? Body { get; init; }

    /// <summary>
    /// How many times the rule applies before it is spent (<c>times</c>); <see langword="null"/> means always.
    /// </summary>
    public int? Times { get; init; }
}
