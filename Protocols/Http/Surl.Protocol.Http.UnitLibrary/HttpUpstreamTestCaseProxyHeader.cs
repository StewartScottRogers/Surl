using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// Reads past the PROXY protocol version 1 header upstream curl sends before its first request
/// with <c>--haproxy-protocol</c> or <c>--haproxy-clientip</c> (HAProxy's
/// <c>proxy-protocol.txt</c>, section 2.1: <c>PROXY TCP4|TCP6|UNKNOWN ...\r\n</c>, at most 107
/// bytes), so the HTTP server half of an upstream test case (ADR-0080 decision 2) reads the
/// request after it.
/// </summary>
/// <remarks>
/// The header's bytes are read through the connection it is given, so a recording connection
/// records them as the first received line, as upstream's <c>&lt;protocol&gt;</c> sections
/// expect. A connection that does not start with <c>PROXY </c> is handed on with every byte it
/// sent. One that does, but sends no CRLF within 107 bytes, is not taken as a request.
/// </remarks>
internal static class HttpUpstreamTestCaseProxyHeader
{
    /// <summary>The most bytes a version 1 header may take, its CRLF included.</summary>
    internal const int MaxHeaderBytes = 107;

    private static ReadOnlySpan<byte> Signature => "PROXY "u8;

    /// <summary>
    /// Reads the connection's first bytes and, when they are a PROXY version 1 header, reads past it.
    /// </summary>
    /// <param name="connection">The accepted connection, read through for every byte.</param>
    /// <param name="context">The exchange: its log and cancellation.</param>
    /// <returns>
    /// The connection to read the first request from, returning first the bytes read here that
    /// follow the header (or every byte read here, when there was no header); <see langword="null"/>
    /// when a PROXY header had no CRLF within <see cref="MaxHeaderBytes"/> bytes.
    /// </returns>
    internal static async Task<IConnection?> ReadPastAsync(IConnection connection, ExchangeContext context)
    {
        var buffer = new byte[MaxHeaderBytes];
        var count = await ReadFirstBytesAsync(connection, buffer, context.CancellationToken);

        return buffer.AsSpan(0, count).StartsWith(Signature)
            ? ReadPastHeader(connection, buffer, count, context)
            : new AlreadyReadBytesConnection(connection, buffer.AsMemory(0, count));
    }

    // Reads until the bytes cannot be an unfinished header, the buffer is full or the client half-closes.
    private static async Task<int> ReadFirstBytesAsync(IConnection connection, byte[] buffer, CancellationToken cancellationToken)
    {
        var count = 0;
        int read;
        do
        {
            read = await connection.ReadAsync(buffer.AsMemory(count), cancellationToken);
            count += read;
        }
        while (read > 0 && count < MaxHeaderBytes && MayBeUnfinishedHeader(buffer.AsSpan(0, count)));

        return count;
    }

    private static AlreadyReadBytesConnection? ReadPastHeader(IConnection connection, byte[] buffer, int count, ExchangeContext context)
    {
        var lineFeed = Array.IndexOf(buffer, (byte)'\n', 0, count);
        if (lineFeed < 0 || buffer[lineFeed - 1] != (byte)'\r')
        {
            context.Log.Note($"A PROXY protocol header had no CRLF within {MaxHeaderBytes} bytes; closed with no bytes.");

            return null;
        }

        context.Log.Note($"Read a PROXY protocol header of {lineFeed + 1} bytes.");

        return new AlreadyReadBytesConnection(connection, buffer.AsMemory(lineFeed + 1, count - lineFeed - 1));
    }

    // True while the bytes so far could still be the start of a header whose line has not ended.
    private static bool MayBeUnfinishedHeader(ReadOnlySpan<byte> bytes) =>
        bytes.Length < Signature.Length
            ? Signature.StartsWith(bytes)
            : bytes.StartsWith(Signature) && !bytes.Contains((byte)'\n');
}
