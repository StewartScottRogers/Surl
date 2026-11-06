using System.Globalization;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// Reads the client's frames (ADR-0118 decisions 0 and 8): every line ends with LF or CR LF, any
/// number of EOLs between frames (heart-beats) are skipped, and a frame is read however its bytes
/// are split across reads.
/// </summary>
/// <remarks>
/// A frame is a command line, header lines, an empty line, the body and a NUL. With
/// <c>content-length</c>, exactly that many octets are the body, NULs included; without it, the body
/// ends at the first NUL. Headers are decoded by <see cref="Version"/>'s escapes, except on CONNECT
/// and STOMP, and the first of a repeated name is kept. Each breach throws
/// <see cref="StompProtocolViolationException"/>: a line over the line limit (EOL included), more
/// than <see cref="MaxHeaders"/> headers, a body over the body limit, and a malformed frame - a
/// header line without <c>:</c>, an undefined escape, invalid UTF-8, a bad <c>content-length</c>,
/// or no NUL after the body.
/// </remarks>
/// <param name="connection">The connection read from.</param>
/// <param name="maxLineBytes">The longest command or header line, its EOL included (<c>--max-line</c>).</param>
/// <param name="maxBodyBytes">The largest body (<c>--max-message</c>).</param>
internal sealed class StompFrameReader(IConnection connection, long maxLineBytes, long maxBodyBytes)
{
    /// <summary>The most headers one frame may carry, repeats included (ADR-0118 decision 8).</summary>
    public const int MaxHeaders = 128;

    private const int InitialBufferBytes = 4096;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private byte[] buffer = new byte[InitialBufferBytes];
    private int start;
    private int end;
    private long lastReceivedTicks;

    /// <summary>
    /// The version whose escapes headers are decoded by, once negotiated; <see langword="null"/>
    /// before, when nothing is decoded.
    /// </summary>
    public StompVersion? Version { get; set; }

    /// <summary>The clock <see cref="LastReceivedAt"/> is read from.</summary>
    public TimeProvider Clock { get; init; } = TimeProvider.System;

    /// <summary>
    /// When a read last returned from the client, frames and heart-beat EOLs alike; read from
    /// another thread by the heart-beat watch (ADR-0118 decision 7).
    /// </summary>
    public DateTimeOffset LastReceivedAt => new(Interlocked.Read(ref lastReceivedTicks), TimeSpan.Zero);

    /// <summary>Records that something arrived from the client now.</summary>
    public void MarkReceived() => Interlocked.Exchange(ref lastReceivedTicks, Clock.GetUtcNow().UtcTicks);

    /// <summary>
    /// Reads the next frame; <see langword="null"/> once the client has closed its side (a part
    /// frame it left is dropped).
    /// </summary>
    /// <param name="cancellationToken">Ends the read.</param>
    /// <exception cref="StompProtocolViolationException">The frame breaks a rule or a limit.</exception>
    public async ValueTask<StompFrame?> ReadFrameAsync(CancellationToken cancellationToken)
    {
        var command = await ReadCommandAsync(cancellationToken);
        if (command is null)
        {
            return null;
        }

        var headers = await ReadHeadersAsync(command, cancellationToken);
        if (headers is null)
        {
            return null;
        }

        var body = await ReadBodyAsync(ContentLength(headers), cancellationToken);
        return body is null ? null : new StompFrame(command, headers, body);
    }

    private async ValueTask<string?> ReadCommandAsync(CancellationToken cancellationToken)
    {
        while (await ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length > 0)
            {
                return Text(line);
            }
        }

        return null;
    }

    private async ValueTask<List<KeyValuePair<string, string>>?> ReadHeadersAsync(string command, CancellationToken cancellationToken)
    {
        var escaping = command is "CONNECT" or "STOMP" ? null : Version;
        var headers = new List<KeyValuePair<string, string>>();
        var count = 0;
        while (await ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
            {
                return headers;
            }

            AddHeader(headers, ++count, line, escaping);
        }

        return null;
    }

    // ADR-0118 decisions 0 and 8: past MaxHeaders the frame is refused; the first of a repeated
    // name is kept.
    private static void AddHeader(List<KeyValuePair<string, string>> headers, int count, byte[] line, StompVersion? escaping)
    {
        if (count > MaxHeaders)
        {
            throw new StompProtocolViolationException(StompErrors.TooManyHeaders, $"A frame carried more than {MaxHeaders} headers");
        }

        var header = Header(Text(line), escaping);
        if (!headers.Exists(existing => existing.Key == header.Key))
        {
            headers.Add(header);
        }
    }

    private static KeyValuePair<string, string> Header(string line, StompVersion? escaping)
    {
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            throw Malformed("A header line had no colon");
        }

        return new(Decoded(line[..colon], escaping), Decoded(line[(colon + 1)..], escaping));
    }

    private static string Decoded(string text, StompVersion? escaping)
    {
        if (escaping is not { } version)
        {
            return text;
        }

        return StompHeaderEscaping.TryDecode(text, version, out var decoded)
            ? decoded
            : throw Malformed("A header held an escape its version does not define");
    }

    private long? ContentLength(List<KeyValuePair<string, string>> headers)
    {
        var header = headers.Find(existing => existing.Key == "content-length");
        if (header.Key is null)
        {
            return null;
        }

        if (!long.TryParse(header.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var length))
        {
            throw Malformed("A content-length header was not a non-negative decimal integer");
        }

        ThrowIfBodyOverLimit(length);
        return length;
    }

    private async ValueTask<byte[]?> ReadBodyAsync(long? contentLength, CancellationToken cancellationToken) =>
        contentLength is { } length
            ? await ReadCountedBodyAsync((int)length, cancellationToken)
            : await ReadBodyToNulAsync(cancellationToken);

    private async ValueTask<byte[]?> ReadCountedBodyAsync(int length, CancellationToken cancellationToken)
    {
        while (end - start <= length)
        {
            if (!await FillAsync(cancellationToken))
            {
                return null;
            }
        }

        if (buffer[start + length] != 0)
        {
            throw Malformed("No NUL followed the content-length body");
        }

        return Take(length);
    }

    private async ValueTask<byte[]?> ReadBodyToNulAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var nul = buffer.AsSpan(start, end - start).IndexOf((byte)0);
            if (nul >= 0)
            {
                ThrowIfBodyOverLimit(nul);
                return Take(nul);
            }

            // Without its NUL yet, the body is already at least this long.
            ThrowIfBodyOverLimit(end - start);
            if (!await FillAsync(cancellationToken))
            {
                return null;
            }
        }
    }

    // Takes a body of the given length and the NUL after it.
    private byte[] Take(int length)
    {
        var body = buffer.AsSpan(start, length).ToArray();
        start += length + 1;
        return body;
    }

    private void ThrowIfBodyOverLimit(long bodyBytes)
    {
        if (bodyBytes > maxBodyBytes)
        {
            throw new StompProtocolViolationException(
                StompErrors.FrameTooLarge, $"A frame's body was longer than {maxBodyBytes} bytes");
        }
    }

    private async ValueTask<byte[]?> ReadLineAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = TryTakeLine();
            if (line is not null)
            {
                return line;
            }

            if (!await FillAsync(cancellationToken))
            {
                return null;
            }
        }
    }

    private byte[]? TryTakeLine()
    {
        var pending = buffer.AsSpan(start, end - start);
        var lineFeed = pending.IndexOf((byte)'\n');
        if (lineFeed < 0)
        {
            // Without its line feed yet, the line is already at least one byte longer than this.
            ThrowIfLineOverLimit(pending.Length + 1);
            return null;
        }

        ThrowIfLineOverLimit(lineFeed + 1);
        start += lineFeed + 1;
        var content = pending[..lineFeed];
        return (content.EndsWith((byte)'\r') ? content[..^1] : content).ToArray();
    }

    private void ThrowIfLineOverLimit(long lineBytes)
    {
        if (lineBytes > maxLineBytes)
        {
            throw new StompProtocolViolationException(
                StompErrors.HeaderLineTooLong, $"A command or header line was longer than {maxLineBytes} bytes");
        }
    }

    private async ValueTask<bool> FillAsync(CancellationToken cancellationToken)
    {
        Array.Copy(buffer, start, buffer, 0, end - start);
        end -= start;
        start = 0;
        if (end == buffer.Length)
        {
            Array.Resize(ref buffer, buffer.Length * 2);
        }

        var read = await connection.ReadAsync(buffer.AsMemory(end), cancellationToken);
        end += read;
        MarkReceived();
        return read > 0;
    }

    private static string Text(byte[] line)
    {
        try
        {
            return StrictUtf8.GetString(line);
        }
        catch (DecoderFallbackException)
        {
            throw Malformed("A command or header line was not valid UTF-8");
        }
    }

    private static StompProtocolViolationException Malformed(string reason) => new(StompErrors.MalformedFrame, reason);
}
