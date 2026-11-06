namespace Surl.Protocol.Abstractions;

/// <summary>
/// What is known at one point where a server, store or login check asks <see cref="IFaultPlan"/>
/// whether to misbehave (ADR-0124, decision 4). Every attribute but the stage is
/// <see langword="null"/> when it is not known at that point.
/// </summary>
/// <param name="Stage">The stage the point is at.</param>
public sealed record FaultPoint(FaultStage Stage)
{
    /// <summary>
    /// The exchange's <see cref="ExchangeContext.ExchangeId"/>, or <see langword="null"/> (a store seed).
    /// </summary>
    public long? ExchangeId { get; init; }

    /// <summary>
    /// The listen URL that accepted the exchange, as the instance reports it, bound port included.
    /// </summary>
    public string? ListenUrl { get; init; }

    /// <summary>
    /// The exchange's scheme.
    /// </summary>
    public string? Scheme { get; init; }

    /// <summary>
    /// The account name logged in or logging in.
    /// </summary>
    public string? Account { get; init; }

    /// <summary>
    /// The path, mailbox, topic, queue, selector or DN the point concerns (ADR-0124, decision 4's table).
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// The protocol's verb at a <see cref="FaultStage.Command"/> point (<c>GET</c>, <c>RETR</c>, <c>RCPT</c>).
    /// </summary>
    public string? Command { get; init; }
}
