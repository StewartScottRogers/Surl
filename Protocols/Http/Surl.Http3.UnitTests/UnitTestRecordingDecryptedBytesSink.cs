using Surl.Protocol.Abstractions;

namespace Surl.Http3;

/// <summary>A decrypted-bytes sink that keeps every report, copied, in order.</summary>
internal sealed class UnitTestRecordingDecryptedBytesSink : IDecryptedBytesSink
{
    private readonly List<Entry> entries = [];

    public Entry[] Entries
    {
        get
        {
            lock (entries)
            {
                return [.. entries];
            }
        }
    }

    public void Received(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes) => Add(true, layer, streamId, bytes);

    public void ReceivedFramePayload(ExchangeBytesLayer layer, long streamId, ReadOnlySpan<byte> bytes) =>
        Add(true, layer, streamId, bytes, isFramePayload: true);

    public void Sent(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes) => Add(false, layer, streamId, bytes);

    private void Add(bool isReceived, ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes, bool isFramePayload = false)
    {
        lock (entries)
        {
            entries.Add(new Entry(isReceived, layer, streamId, bytes.ToArray(), isFramePayload));
        }
    }

    internal sealed record Entry(bool IsReceived, ExchangeBytesLayer Layer, long? StreamId, byte[] Bytes, bool IsFramePayload);
}
