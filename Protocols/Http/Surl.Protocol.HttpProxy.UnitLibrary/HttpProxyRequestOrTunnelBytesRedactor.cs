using System.Buffers;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> for what a client sends the proxy before any HTTP/2
/// connection preface: HTTP request heads (<see cref="HttpRequestHeadBytesRedactor"/>), until the
/// bytes turn out to be a <c>CONNECT</c> tunnel's text. From there on, every line is redacted by
/// <see cref="HttpProxyTunnelLinesRedactor"/> (ADR-0182, amendment 16). Request heads and tunnelled
/// lines alike lose the values of <see cref="HttpWithheldHeaderNames.Credentials"/> and
/// <see cref="HttpWithheldHeaderNames.ApiKeysAndSessionTokens"/> (amendment 18).
/// </summary>
/// <remarks>
/// <para>
/// The tunnel's text starts at a line where a request may start that begins with an ASCII letter, a
/// digit from 1 to 9 or a line feed and is not a request line: three words, the last starting
/// <c>HTTP/</c> or <c>RTSP/</c>, ended by CR LF. It also starts at a line of a <c>CONNECT</c> head
/// that does not end with CR LF, and after a <c>CONNECT</c> head that declares a body, because a
/// <c>CONNECT</c> has none. Each line that decides this is held until its line feed, or past 64 KiB,
/// which makes it the tunnel's.
/// </para>
/// <para>
/// The client's bytes alone say where this happens, so tunnelled bytes sent before the proxy's
/// <c>200</c> are redacted too. A tunnel starting with any other byte (TLS, LDAP, MQTT) stays with
/// the request head redactor.
/// </para>
/// </remarks>
internal sealed class HttpProxyRequestOrTunnelBytesRedactor : IReceivedBytesRedactor
{
    private const byte LineFeed = (byte)'\n';
    private const int RequestLineWordCount = 3;

    // The proxy cannot tell which origin it carries, so every request head loses the API-key fields too.
    private readonly HttpRequestHeadBytesRedactor requests = new(HttpWithheldHeaderNames.ApiKeysAndSessionTokens);
    private readonly HttpProxyTunnelLinesRedactor tunnelLines = new();

    // The start of a line not yet known to be a request head's or the tunnel's.
    private readonly ArrayBufferWriter<byte> undecidedLine = new();
    private bool isInConnectHead;
    private bool isTunnelledText;

    /// <summary>Whether the next byte may begin a new request.</summary>
    public bool IsAtRequestStart => !isTunnelledText && undecidedLine.WrittenCount == 0 && requests.IsAtRequestStart;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        while (!received.IsEmpty)
        {
            var lineEnd = received.IndexOf(LineFeed) + 1;
            var length = lineEnd > 0 ? lineEnd : received.Length;
            RedactWithinOneLine(received[..length], output);
            received = received[length..];
        }
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        requests.ObserveSent(sent);
        tunnelLines.ObserveSent(sent);
    }

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);

        // An unfinished line may hold a secret; the tunnel's rules write a request line unchanged.
        tunnelLines.Redact(undecidedLine.WrittenSpan, output);
        undecidedLine.ResetWrittenCount();
        requests.Flush(output);
        tunnelLines.Flush(output);
    }

    private static bool MayStartTunnel(byte first) =>
        char.IsAsciiLetter((char)first) || first is (>= (byte)'1' and <= (byte)'9') or LineFeed;

    private static bool IsRequestLine(ReadOnlySpan<byte> line)
    {
        var words = line.TrimEnd("\r\n"u8);
        if (words.Count((byte)' ') != RequestLineWordCount - 1)
        {
            return false;
        }

        var version = words[(words.LastIndexOf((byte)' ') + 1)..];

        return version.StartsWith("HTTP/"u8) || version.StartsWith("RTSP/"u8);
    }

    // The bytes hold at most one line feed, as their last byte.
    private void RedactWithinOneLine(ReadOnlySpan<byte> bytes, ArrayBufferWriter<byte> output)
    {
        if (isTunnelledText)
        {
            tunnelLines.Redact(bytes, output);
        }
        else if (undecidedLine.WrittenCount > 0 || isInConnectHead || (requests.IsAtRequestStart && MayStartTunnel(bytes[0])))
        {
            HoldUndecidedLine(bytes, output);
        }
        else
        {
            requests.Redact(bytes, output);
        }
    }

    private void HoldUndecidedLine(ReadOnlySpan<byte> bytes, ArrayBufferWriter<byte> output)
    {
        undecidedLine.Write(bytes);
        if (bytes[^1] != LineFeed && undecidedLine.WrittenCount <= LineBytesRedactor.DefaultMaxHeldBytes)
        {
            return;
        }

        var line = undecidedLine.WrittenSpan;
        if (line.EndsWith("\r\n"u8) && (isInConnectHead || IsRequestLine(line)))
        {
            RedactHeadLine(line, output);
        }
        else
        {
            isTunnelledText = true;
            tunnelLines.Redact(line, output);
        }

        undecidedLine.ResetWrittenCount();
    }

    // A request line, or a later line of a CONNECT head, ended by CR LF.
    private void RedactHeadLine(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        var endsConnectHead = isInConnectHead && line.SequenceEqual("\r\n"u8);
        isInConnectHead = !endsConnectHead && (isInConnectHead || line.StartsWith("CONNECT "u8));
        requests.Redact(line, output);

        // A CONNECT has no body, so whatever its head declares, what follows it is the tunnel's.
        isTunnelledText = endsConnectHead && !requests.IsAtRequestStart;
    }
}
