using System.Buffers;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ws;

/// <summary>
/// An <see cref="IReceivedBytesRedactor"/> for one WebSocket connection: it masks the credentials of
/// the upgrade request's <c>Authorization</c> field exactly as <see cref="HttpRequestHeadBytesRedactor"/>
/// does (ADR-0182), and stops at the blank line that ends that head - the first empty line after a
/// non-empty one, so empty lines before the request line do not stop it (amendment 26). Every byte
/// after it is taken for WebSocket frames and is passed through unchanged, so a frame whose payload
/// reads <c>Authorization: x</c> is logged as it was sent.
/// </summary>
internal sealed class WebSocketUpgradeHeadBytesRedactor : IReceivedBytesRedactor
{
    private const byte CarriageReturn = 0x0D;
    private const byte LineFeed = 0x0A;

    private readonly HttpRequestHeadBytesRedactor head = new();
    private bool headEnded;
    private bool headHasNonEmptyLine;
    private bool lineIsEmpty = true;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        int headLength = headEnded ? 0 : LengthThroughHeadEnd(received);
        if (headLength > 0)
        {
            head.Redact(received[..headLength], output);
        }

        output.Write(received[headLength..]);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent) => head.ObserveSent(sent);

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output) => head.Flush(output);

    private int LengthThroughHeadEnd(ReadOnlySpan<byte> received)
    {
        for (int index = 0; index < received.Length; index++)
        {
            byte next = received[index];
            if (next == LineFeed)
            {
                // Empty lines before the request line end no head: the server skips them (RFC 9112, section 2.2).
                if (lineIsEmpty && headHasNonEmptyLine)
                {
                    headEnded = true;
                    return index + 1;
                }

                lineIsEmpty = true;
            }
            else if (next != CarriageReturn)
            {
                headHasNonEmptyLine = true;
                lineIsEmpty = false;
            }
        }

        return received.Length;
    }
}
