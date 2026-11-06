namespace Surl.Protocol.Abstractions;

/// <summary>
/// One chunk of bytes that crossed an exchange's connection or flow, published only while
/// <see cref="ISurlEventPublisher.IsPublishingBytes"/> is <see langword="true"/>.
/// </summary>
/// <param name="ExchangeId">The exchange's <see cref="ExchangeContext.ExchangeId"/>.</param>
/// <param name="Direction">Whether the server received or sent the bytes.</param>
/// <param name="Bytes">A copy of the bytes, which the publisher does not reuse.</param>
public sealed record ExchangeBytesEvent(long ExchangeId, ExchangeDirection Direction, ReadOnlyMemory<byte> Bytes) : SurlEvent;
