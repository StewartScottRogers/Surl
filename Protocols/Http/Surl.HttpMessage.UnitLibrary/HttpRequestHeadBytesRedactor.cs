using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.HttpMessage;

/// <summary>
/// An <see cref="IReceivedBytesRedactor"/> for the HTTP/1.x (and RTSP) request heads a client
/// sends on one connection: it masks the credentials of <c>Authorization</c> and
/// <c>Proxy-Authorization</c> (and any further secret fields it is given), keeping the field name and
/// the auth scheme word, so <c>Authorization: Basic YWxpY2U6</c> logs as
/// <c>Authorization: Basic ****</c> (ADR-0182, decisions 1 and 7).
/// </summary>
/// <remarks>
/// A head is held back one line at a time (see <see cref="LineBytesRedactor"/>). A line ends at a
/// line feed, with or without a carriage return before it, as <see cref="HttpConnectionReader"/>
/// reads it, so each line is judged on its own and an empty one ended either way ends the head; the
/// line end is written as it arrived. A line with no colon after a secret field is masked as part of
/// it, and a line naming a secret field is masked even where the request line belongs (ADR-0182,
/// amendment 24). Once the blank line ends a head, the body its framing declares - a
/// <c>Content-Length</c> count or a chunked sequence - passes through, its chunk sizes and data
/// unchanged. The trailer section of a chunked body (RFC 9112, section 7.1.2) is held back and
/// judged line by line as a head's field lines are, so a secret field in it, and each folded line of
/// one, is masked; its framing fields change nothing (ADR-0182, amendment 32, which closes the limit
/// amendments 18 and 24 give, that a secret in the trailers is logged). The next head on a
/// persistent connection is then redacted again. A head whose framing cannot be read is followed by
/// another head, so nothing is left unredacted. A field name is compared without regard to case
/// (RFC 9110, section 5.1).
/// </remarks>
public sealed class HttpRequestHeadBytesRedactor : IReceivedBytesRedactor
{
    private const byte CarriageReturn = 0x0D;
    private const byte LineFeed = 0x0A;

    // Ordered by how their bytes are read: as field lines, as counted data, then as chunk framing.
    private enum State
    {
        Head,
        Trailers,
        ContentLengthBody,
        ChunkData,
        ChunkSizeLine,
        ChunkDataEnd,
    }

    private readonly HttpRequestHeadLineRule rule;
    private readonly LineBytesRedactor lines;
    private State state = State.Head;
    private long remaining;
    private long chunkSize;
    private bool inChunkExtension;
    private bool previousFieldLineByteWasCarriageReturn;

    /// <summary>
    /// Creates a redactor for one connection.
    /// </summary>
    /// <param name="additionalSecretFieldNames">Fields besides <c>Authorization</c> and <c>Proxy-Authorization</c> whose whole value is masked (an API key header); empty for none.</param>
    public HttpRequestHeadBytesRedactor(IEnumerable<string>? additionalSecretFieldNames = null)
    {
        rule = new HttpRequestHeadLineRule(additionalSecretFieldNames ?? []);
        lines = new LineBytesRedactor(rule);
    }

    /// <summary>
    /// Whether the next byte begins a new request: no body, chunk framing or trailer section is
    /// being read and no byte of a head line is held back. A protocol that interleaves its own frames between
    /// requests (RTSP's <c>$</c> frames) uses this to tell where one may start.
    /// </summary>
    public bool IsAtRequestStart => state == State.Head && lines.HeldByteCount == 0 && rule.NoHeadLineRead;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        int index = 0;
        while (index < received.Length)
        {
            index += state switch
            {
                State.Head or State.Trailers => RedactFieldLineByte(received[index], output),
                State.ContentLengthBody or State.ChunkData => PassThroughBody(received[index..], output),
                _ => PassThroughFramingByte(received[index], output),
            };
        }
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent) => lines.ObserveSent(sent);

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        lines.Flush(output);
        previousFieldLineByteWasCarriageReturn = false;
    }

    // A byte of a head line or of a trailer line.
    private int RedactFieldLineByte(byte next, ArrayBufferWriter<byte> output)
    {
        if (next == LineFeed && !previousFieldLineByteWasCarriageReturn)
        {
            EndFieldLineAtBareLineFeed(output);
        }
        else
        {
            lines.Redact([next], output);
        }

        previousFieldLineByteWasCarriageReturn = next == CarriageReturn;
        if (rule.FieldSectionIsComplete)
        {
            EndFieldSection();
        }

        return 1;
    }

    // A head is followed by the body it declares; a trailer section by the next head.
    private void EndFieldSection()
    {
        if (state == State.Head)
        {
            StartBody(rule.Framing);
        }
        else
        {
            state = State.Head;
        }

        rule.StartNextHead();
    }

    // LineBytesRedactor ends a line at CR LF only, so it is given both and the CR it writes back is dropped.
    private void EndFieldLineAtBareLineFeed(ArrayBufferWriter<byte> output)
    {
        var endedLine = new ArrayBufferWriter<byte>();
        lines.Redact("\r\n"u8, endedLine);
        output.Write(endedLine.WrittenSpan[..^2]);
        output.Write([LineFeed]);
    }

    private void StartBody(HttpRequestBodyFraming framing)
    {
        switch (framing.Kind)
        {
            case HttpRequestBodyFramingKind.ContentLength:
                remaining = framing.ContentLength;
                state = State.ContentLengthBody;
                break;
            case HttpRequestBodyFramingKind.Chunked:
                StartChunkSizeLine();
                break;
            default:
                state = State.Head;
                break;
        }
    }

    private int PassThroughBody(ReadOnlySpan<byte> bytes, ArrayBufferWriter<byte> output)
    {
        int take = (int)Math.Min(remaining, bytes.Length);
        output.Write(bytes[..take]);
        remaining -= take;
        if (remaining == 0)
        {
            state = state == State.ChunkData ? State.ChunkDataEnd : State.Head;
        }

        return take;
    }

    private int PassThroughFramingByte(byte next, ArrayBufferWriter<byte> output)
    {
        output.Write([next]);
        if (state == State.ChunkSizeLine)
        {
            ReadChunkSizeByte(next);
        }
        else if (next == LineFeed)
        {
            StartChunkSizeLine();
        }

        return 1;
    }

    private void StartChunkSizeLine()
    {
        state = State.ChunkSizeLine;
        chunkSize = 0;
        inChunkExtension = false;
    }

    private void ReadChunkSizeByte(byte next)
    {
        if (next == LineFeed)
        {
            EndChunkSizeLine();
        }
        else if (next is (byte)';' or CarriageReturn)
        {
            inChunkExtension = true;
        }
        else if (!inChunkExtension && Uri.IsHexDigit((char)next))
        {
            int digit = next <= (byte)'9' ? next - '0' : (next | 0x20) - 'a' + 10;
            chunkSize = ((chunkSize << 4) | (long)digit) & long.MaxValue;
        }
    }

    // The last chunk, of size 0, is followed by the trailer section; any other by its data.
    private void EndChunkSizeLine()
    {
        if (chunkSize == 0)
        {
            rule.StartTrailerSection();
            state = State.Trailers;
        }
        else
        {
            remaining = chunkSize;
            state = State.ChunkData;
        }
    }
}
