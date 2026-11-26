using System.Buffers;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ws;

/// <summary>
/// An <see cref="IReceivedBytesRedactor"/> for one WebSocket connection: until the server sends
/// <c>101</c>, every byte received is judged by an <see cref="HttpRequestHeadBytesRedactor"/>, so
/// the credentials of each upgrade request's <c>Authorization</c> field are masked, that of a
/// request sent again after a <c>401</c> too (ADR-0182, amendment 27). Bytes received once the
/// <c>101</c> has been sent are WebSocket frames and are passed through unchanged, so a frame whose
/// payload reads <c>Authorization: x</c> is logged as it was sent. Bytes received before it, those
/// read together with the upgrade request included, are judged as a request head.
/// </summary>
internal sealed class WebSocketUpgradeHeadBytesRedactor : IReceivedBytesRedactor
{
    private readonly HttpRequestHeadBytesRedactor heads = new();
    private bool upgradeAccepted;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!upgradeAccepted)
        {
            heads.Redact(received, output);
            return;
        }

        // The unfinished line still held from before the 101 goes ahead of the frames that follow it.
        heads.Flush(output);
        output.Write(received);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent) =>
        upgradeAccepted |= sent.StartsWith("HTTP/1.1 101 "u8);

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output) => heads.Flush(output);
}
