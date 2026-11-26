using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// Publishes the bytes the code serving an exchange reports through <see cref="IDecryptedBytesSink"/> as
/// <see cref="ExchangeBytesEvent"/>s carrying their layer and stream, copied, only while the publisher
/// wants bytes, and notes each chunk in the exchange's log (ADR-0169, decisions 3 and 4). It never touches
/// the wire byte counts: those come from <see cref="EventPublishingExchangeLog"/> alone. The event carries
/// the bytes raw, in both directions. When the server redacts, the log note carries received bytes alone
/// redacted, each chunk by a fresh redactor flushed at the chunk's end; the note carries sent bytes as they
/// are (ADR-0182, decisions 4 and 6, and amendment 1).
/// </summary>
/// <param name="log">The exchange's log, which receives a note naming the layer and stream per chunk.</param>
/// <param name="events">The instance's event stream.</param>
/// <param name="exchangeId">The exchange the bytes belong to.</param>
/// <param name="createRedactor">Creates a redactor for one received chunk, or <see langword="null"/> when the server does not redact.</param>
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
