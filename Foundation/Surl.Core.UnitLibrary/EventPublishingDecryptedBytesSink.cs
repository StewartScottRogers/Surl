using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// Publishes the plaintext a server's QUIC or DTLS code reports as <see cref="ExchangeBytesEvent"/>s
/// carrying their layer and stream, copied, only while the publisher wants bytes, and notes each chunk
/// in the exchange's log for <c>--trace</c> (ADR-0169, decisions 3 and 4). It never touches the wire
/// byte counts: those come from <see cref="EventPublishingExchangeLog"/> alone.
/// </summary>
/// <param name="log">The exchange's log, which receives a note naming the layer and stream per chunk.</param>
/// <param name="events">The instance's event stream.</param>
/// <param name="exchangeId">The exchange the bytes belong to.</param>
internal sealed class EventPublishingDecryptedBytesSink(IExchangeLog log, ISurlEventPublisher events, long exchangeId) : IDecryptedBytesSink
{
    /// <inheritdoc/>
    public void Received(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes) =>
        Report(ExchangeDirection.Received, layer, streamId, bytes);

    /// <inheritdoc/>
    public void Sent(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes) =>
        Report(ExchangeDirection.Sent, layer, streamId, bytes);

    private void Report(ExchangeDirection direction, ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes)
    {
        var stream = streamId is long id ? $", stream {id}" : string.Empty;
        log.Note($"{layer} bytes {direction.ToString().ToLowerInvariant()}{stream}, {bytes.Length} bytes: {Convert.ToHexString(bytes)}");

        if (events.IsPublishingBytes)
        {
            events.Publish(new ExchangeBytesEvent(exchangeId, direction, bytes.ToArray())
            {
                Layer = layer,
                StreamId = streamId,
            });
        }
    }
}
