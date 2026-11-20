using System.Buffers;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> of an RTSP connection: it masks the credentials of the
/// request heads through <see cref="HttpRequestHeadBytesRedactor"/> and passes an interleaved
/// <c>$</c> frame (RFC 2326 section 10.12) the client sends between requests through unchanged,
/// so a packet that happens to contain <c>Authorization:</c> is logged as it arrived (ADR-0182).
/// </summary>
internal sealed class RtspRequestBytesRedactor : IReceivedBytesRedactor
{
    private readonly HttpRequestHeadBytesRedactor heads = new();
    private readonly byte[] frameHeader = new byte[RtspInterleavedFrame.FrameHeaderBytes];
    private int frameHeaderBytesRead = -1;
    private int frameBytesRemaining;

    private bool InFrame => frameHeaderBytesRead >= 0;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        int index = 0;
        while (index < received.Length)
        {
            if (InFrame)
            {
                index += PassFrameBytes(received[index..], output);
            }
            else if (received[index] == RtspInterleavedFrame.Marker && heads.IsAtRequestStart)
            {
                output.Write(received.Slice(index++, 1));
                frameHeaderBytesRead = 1;
            }
            else
            {
                heads.Redact(received.Slice(index++, 1), output);
            }
        }
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent) => heads.ObserveSent(sent);

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output) => heads.Flush(output);

    private int PassFrameBytes(ReadOnlySpan<byte> bytes, ArrayBufferWriter<byte> output)
    {
        int take;
        if (frameHeaderBytesRead < frameHeader.Length)
        {
            frameHeader[frameHeaderBytesRead] = bytes[0];
            output.Write(bytes[..1]);
            take = 1;
            frameHeaderBytesRead++;
            if (frameHeaderBytesRead == frameHeader.Length)
            {
                frameBytesRemaining = (frameHeader[2] << 8) | frameHeader[3];
                EndFrameWhenEmpty();
            }

            return take;
        }

        take = Math.Min(frameBytesRemaining, bytes.Length);
        output.Write(bytes[..take]);
        frameBytesRemaining -= take;
        EndFrameWhenEmpty();

        return take;
    }

    private void EndFrameWhenEmpty()
    {
        if (frameBytesRemaining == 0)
        {
            frameHeaderBytesRead = -1;
        }
    }
}
