using System.Buffers;
using Surl.Http2;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> of one connection to the proxy (ADR-0182, amendments 8
/// and 16). Until the client sends the HTTP/2 connection preface,
/// <see cref="HttpProxyRequestOrTunnelBytesRedactor"/> redacts HTTP/1.x request heads, those inside
/// an HTTP/1.1 <c>CONNECT</c> tunnel included, and the lines of a tunnelled text protocol (FTP,
/// SMTP, IMAP, POP3). The preface is looked for wherever it falls, because HTTP/2 carries a
/// credential as an HPACK field no line rule sees.
/// When the preface starts where a request may start (ALPN <c>h2</c>, prior knowledge in a tunnel,
/// or after an <c>h2c</c> upgrade), every later byte is redacted by
/// <see cref="Http2RequestBytesRedactor"/>, which withholds the <c>DATA</c> payloads of an HTTP/2
/// <c>CONNECT</c> stream (amendment 11); a preface anywhere else withholds every later byte
/// (<see cref="HttpProxyWithheldBytesRedactor"/>). In HTTP/1.x and HTTP/2 alike, the values of
/// <see cref="HttpWithheldHeaderNames.ApiKeysAndSessionTokens"/> are withheld beside the two
/// credential fields, in every request, because the proxy cannot tell which origin it carries
/// (amendment 18).
/// </summary>
internal sealed class HttpProxyConnectionBytesRedactor : IReceivedBytesRedactor
{
    private readonly HttpProxyRequestOrTunnelBytesRedactor beforePreface = new();

    // The last bytes received before the preface, as many as the preface is long.
    private readonly byte[] latest = new byte[Http2ConnectionPreface.Preface.Length];
    private IReceivedBytesRedactor? afterPreface;

    // How many of the latest bytes are the start of a preface that began where a request may start.
    private int matchedFromRequestStart;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var rest = received;
        while (afterPreface is null && !rest.IsEmpty)
        {
            var length = SegmentLength(rest);
            RedactSegment(rest[..length], output);
            rest = rest[length..];
        }

        afterPreface?.Redact(rest, output);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent) => (afterPreface ?? beforePreface).ObserveSent(sent);

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        (afterPreface ?? beforePreface).Flush(output);
    }

    // The preface begins with 'P' and ends with a line feed, so every 'P' starts a segment and the
    // preface's last byte ends one; the bytes between, a bulk body's included, stay together.
    private static int SegmentLength(ReadOnlySpan<byte> bytes)
    {
        var stop = bytes.IndexOfAny((byte)'P', (byte)'\n');
        if (stop < 0)
        {
            return bytes.Length;
        }

        if (stop == 0)
        {
            return 1;
        }

        return bytes[stop] == (byte)'\n' ? stop + 1 : stop;
    }

    private static IReceivedBytesRedactor CreateFrameRedactor()
    {
        // The HTTP/2 redactor expects the preface first; it is already logged, so this copy is dropped.
        var frames = new Http2RequestBytesRedactor(HttpWithheldHeaderNames.ApiKeysAndSessionTokens);
        frames.Redact(Http2ConnectionPreface.Preface, new ArrayBufferWriter<byte>());

        return frames;
    }

    private void RedactSegment(ReadOnlySpan<byte> segment, ArrayBufferWriter<byte> output)
    {
        // Asked before the redactor takes the segment: afterwards it may hold those bytes.
        MatchPreface(segment, beforePreface.IsAtRequestStart);
        beforePreface.Redact(segment, output);
        KeepLatest(segment);
        if (latest.AsSpan().SequenceEqual(Http2ConnectionPreface.Preface))
        {
            // The preface ends a line, so flushing here leaves that redactor holding none of it.
            beforePreface.Flush(output);
            afterPreface = matchedFromRequestStart == latest.Length ? CreateFrameRedactor() : new HttpProxyWithheldBytesRedactor();
        }
    }

    private void MatchPreface(ReadOnlySpan<byte> segment, bool isAtRequestStart)
    {
        var preface = Http2ConnectionPreface.Preface;
        if (matchedFromRequestStart > 0 && preface[matchedFromRequestStart..].StartsWith(segment))
        {
            matchedFromRequestStart += segment.Length;
        }
        else
        {
            matchedFromRequestStart = isAtRequestStart && preface.StartsWith(segment) ? segment.Length : 0;
        }
    }

    private void KeepLatest(ReadOnlySpan<byte> segment)
    {
        var added = Math.Min(segment.Length, latest.Length);
        latest.AsSpan(added).CopyTo(latest);
        segment[^added..].CopyTo(latest.AsSpan(latest.Length - added));
    }
}
