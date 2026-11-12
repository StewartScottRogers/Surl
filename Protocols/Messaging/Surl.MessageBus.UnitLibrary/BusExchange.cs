namespace Surl.MessageBus;

/// <summary>
/// The exchange a server is serving, as a <see cref="BusConnection"/> names it on every change and
/// fault its operations cause (ADR-0124, decisions 1 and 4).
/// </summary>
/// <param name="ExchangeId">The exchange's <c>ExchangeContext.ExchangeId</c>.</param>
/// <param name="ListenUrl">The listen URL that accepted the exchange, bound port included.</param>
/// <param name="Scheme">The exchange's scheme.</param>
public sealed record BusExchange(long ExchangeId, string ListenUrl, string Scheme);
