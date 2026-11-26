using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// Publishes the bytes the code serving an exchange reports through <see cref="IDecryptedBytesSink"/> as
/// <see cref="ExchangeBytesEvent"/>s carrying their layer and stream, copied, only while the publisher
/// wants bytes, and notes each chunk in the exchange's log (ADR-0169, decisions 3 and 4). It never touches
/// the wire byte counts: those come from <see cref="EventPublishingExchangeLog"/> alone. The event carries
/// the bytes raw, in both directions. When the server redacts, the log note carries received bytes alone
/// redacted; the note carries sent bytes as they are (ADR-0182, decisions 4 and 6, and amendments 1, 21
/// and 22). A stream's <see cref="ExchangeBytesLayer.Decrypted"/> bytes go through one
/// <see cref="IStreamReceivedBytesRedactor"/> kept for the exchange, when the server's redactor is one.
/// <see cref="ExchangeBytesLayer.Decoded"/> text goes to a fresh redactor's
/// <see cref="IDecodedTextReceivedBytesRedactor.RedactDecodedText"/>, when the redactor is one. Every
/// other received chunk, and every frame payload, goes through a fresh redactor flushed at the chunk's end.
/// </summary>
/// <param name="log">The exchange's log, which receives a note naming the layer and stream per chunk.</param>
/// <param name="events">The instance's event stream.</param>
/// <param name="exchangeId">The exchange the bytes belong to.</param>
/// <param name="createRedactor">Creates a redactor for the exchange's decrypted bytes, or <see langword="null"/> when the server does not redact.</param>
internal sealed class EventPublishingDecryptedBytesSink(
    IExchangeLog log, ISurlEventPublisher events, long exchangeId, Func<IReceivedBytesRedactor>? createRedactor = null) : IDecryptedBytesSink
{
    private readonly Lock streamRedaction = new();
    private IStreamReceivedBytesRedactor? streamRedactor;

    /// <inheritdoc/>
    public void Received(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes)
    {
        var logged = layer switch
        {
            ExchangeBytesLayer.Decrypted when streamId is long id => RedactedAsTheStreamsNextBytes(id, bytes),
            ExchangeBytesLayer.Decoded => RedactedAsDecodedText(bytes),
            _ => RedactedAlone(bytes),
        };
        Report(ExchangeDirection.Received, layer, streamId, bytes, logged);
    }

    /// <inheritdoc/>
    public void ReceivedFramePayload(ExchangeBytesLayer layer, long streamId, ReadOnlySpan<byte> bytes) =>
        Report(ExchangeDirection.Received, layer, streamId, bytes, RedactedAlone(bytes));

    /// <inheritdoc/>
    public void Sent(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes) =>
        Report(ExchangeDirection.Sent, layer, streamId, bytes, bytes);

    private void Report(
        ExchangeDirection direction, ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> logged)
    {
        var stream = streamId is long id ? $", stream {id}" : string.Empty;
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

    private ReadOnlySpan<byte> RedactedAsTheStreamsNextBytes(long streamId, ReadOnlySpan<byte> bytes)
    {
        if (createRedactor is null)
        {
            return bytes;
        }

        lock (streamRedaction)
        {
            var redactor = streamRedactor ?? createRedactor();
            if (redactor is not IStreamReceivedBytesRedactor followingStreams)
            {
                return RedactWhole(redactor, bytes);
            }

            streamRedactor = followingStreams;
            var output = new ArrayBufferWriter<byte>();
            followingStreams.RedactStream(streamId, bytes, output);

            return output.WrittenSpan;
        }
    }

    private ReadOnlySpan<byte> RedactedAsDecodedText(ReadOnlySpan<byte> decodedText)
    {
        if (createRedactor is null)
        {
            return decodedText;
        }

        var redactor = createRedactor();
        if (redactor is not IDecodedTextReceivedBytesRedactor readingDecodedText)
        {
            return RedactWhole(redactor, decodedText);
        }

        var output = new ArrayBufferWriter<byte>();
        readingDecodedText.RedactDecodedText(decodedText, output);

        return output.WrittenSpan;
    }

    private ReadOnlySpan<byte> RedactedAlone(ReadOnlySpan<byte> bytes) =>
        createRedactor is null ? bytes : RedactWhole(createRedactor(), bytes);

    private static ReadOnlySpan<byte> RedactWhole(IReceivedBytesRedactor redactor, ReadOnlySpan<byte> bytes)
    {
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact(bytes, output);
        redactor.Flush(output);

        return output.WrittenSpan;
    }
}
