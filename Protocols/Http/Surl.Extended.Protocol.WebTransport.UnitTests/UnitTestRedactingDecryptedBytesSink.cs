using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// A decrypted-bytes sink that logs each stream's bytes as <c>Surl.Core</c>'s
/// <c>EventPublishingDecryptedBytesSink</c> does: every <see cref="ExchangeBytesLayer.Decrypted"/> chunk with
/// a stream ID goes through one <see cref="IStreamReceivedBytesRedactor"/> kept for the exchange, and what it
/// writes is kept per stream. Frame payloads, decoded text, datagrams and sent bytes are not that log, and
/// are left out.
/// </summary>
/// <param name="redactor">The redactor the exchange's stream bytes go through.</param>
internal sealed class UnitTestRedactingDecryptedBytesSink(IStreamReceivedBytesRedactor redactor) : IDecryptedBytesSink
{
    private readonly Lock redaction = new();
    private readonly List<(long StreamId, byte[] Logged)> chunks = [];

    /// <summary>What was logged of <paramref name="streamId"/>, one entry per chunk received, in order.</summary>
    public byte[][] LoggedChunks(long streamId)
    {
        lock (redaction)
        {
            return [.. chunks.Where(chunk => chunk.StreamId == streamId).Select(chunk => chunk.Logged)];
        }
    }

    public void Received(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes)
    {
        if (layer != ExchangeBytesLayer.Decrypted || streamId is not long id)
        {
            return;
        }

        lock (redaction)
        {
            var output = new ArrayBufferWriter<byte>();
            redactor.RedactStream(id, bytes, output);
            chunks.Add((id, output.WrittenSpan.ToArray()));
        }
    }

    // Left out on purpose: the interface's default would feed a frame payload to Received as the stream's next bytes.
    public void ReceivedFramePayload(ExchangeBytesLayer layer, long streamId, ReadOnlySpan<byte> bytes)
    {
    }

    public void Sent(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes)
    {
    }
}
