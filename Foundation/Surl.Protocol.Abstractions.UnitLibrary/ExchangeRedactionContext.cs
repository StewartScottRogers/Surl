namespace Surl.Protocol.Abstractions;

/// <summary>
/// What a server is told when the engine asks it for the redactor of one exchange
/// (ADR-0182, decision 6).
/// </summary>
/// <param name="ExchangeId">The exchange's <see cref="ExchangeContext.ExchangeId"/>.</param>
/// <param name="Scheme">The lower-case scheme the exchange is answered on, such as <c>ftps</c>.</param>
public sealed record ExchangeRedactionContext(long ExchangeId, string Scheme);
