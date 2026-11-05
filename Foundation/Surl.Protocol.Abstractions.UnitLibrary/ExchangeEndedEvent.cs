namespace Surl.Protocol.Abstractions;

/// <summary>
/// An exchange ended.
/// </summary>
/// <param name="ExchangeId">The exchange's <see cref="ExchangeContext.ExchangeId"/>.</param>
/// <param name="ReceivedByteCount">How many bytes the server received in the exchange.</param>
/// <param name="SentByteCount">How many bytes the server sent in the exchange.</param>
public sealed record ExchangeEndedEvent(long ExchangeId, long ReceivedByteCount, long SentByteCount) : SurlEvent;
