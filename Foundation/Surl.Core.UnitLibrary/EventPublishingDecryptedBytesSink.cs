using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// Publishes the plaintext a server's QUIC or DTLS code reports as <see cref="ExchangeBytesEvent"/>s
/// carrying their layer and stream, copied, only while the publisher wants bytes, and notes each chunk
/// in the exchange's log for <c>--trace</c> (ADR-0169, decisions 3 and 4). It never touches the wire
/// byte counts: those come from <see cref="EventPublishingExchangeLog"/> alone. The event carries the
/// bytes raw; the note carries them redacted when the server redacts (ADR-0182, decisions 4 and 6),
/// each reported chunk on its own, because a chunk the server reports is a whole unit (a decoded
/// header block, a datagram's plaintext).
/// </summary>
/// <param name="log">The exchange's log, which receives a note naming the layer and stream per chunk.</param>
/// <param name="events">The instance's event stream.</param>
/// <param name="exchangeId">The exchange the bytes belong to.</param>
/// <param name="createRedactor">Creates a redactor for one chunk, or <see langword="null"/> when the server does not redact.</param>
internal sealed class EventPublishingDecryptedBytesSink(
    IExchangeLog log, ISurlEventPublisher events, long exchangeId, Func<IReceivedBytesRedactor>? createRedactor = null) : IDecryptedBytesSink
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
        var logged = direction == ExchangeDirection.Received ? Loggable(bytes) : bytes;
        log.Note($"{layer} bytes {direction.ToString().ToLowerInvariant()}{stream}, {logged.Length} bytes: {Convert.ToHexString(logged)}");

        if (events.IsPublishingBytes)
        {
            events.Publish(new ExchangeBytesEvent(exchangeId, direction, bytes.ToArray())
            {
                Layer = layer,
                StreamId = streamId,
            });
        }
    }

    private ReadOnlySpan<byte> Loggable(ReadOnlySpan<byte> bytes)
    {
        if (createRedactor is null)
        {
            return bytes;
        }

        var redactor = createRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact(bytes, output);
        redactor.Flush(output);

        return output.WrittenSpan;
    }
}
