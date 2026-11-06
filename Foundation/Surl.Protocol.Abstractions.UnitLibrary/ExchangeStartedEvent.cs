using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// An exchange began: a connection was accepted or a datagram flow's first datagram arrived.
/// </summary>
/// <param name="ExchangeId">The exchange's <see cref="ExchangeContext.ExchangeId"/>.</param>
/// <param name="ListenUrl">The listen URL that accepted it, as the instance reports it, bound port included.</param>
/// <param name="Scheme">The listen URL's scheme.</param>
/// <param name="Client">The client's endpoint.</param>
public sealed record ExchangeStartedEvent(long ExchangeId, string ListenUrl, string Scheme, EndPoint Client) : SurlEvent;
