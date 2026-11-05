using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// Reads the start of a cleartext connection to tell the HTTP/2 client connection preface,
/// <c>PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n</c> (RFC 9113 section 3.4), from an HTTP/1.x request head
/// (ADR-0081, decision 3).
/// </summary>
/// <remarks>
/// It reads only while what has arrived is still a prefix of the preface, so an HTTP/1.x
/// request - a <c>PUT</c> or <c>POST</c> starts with the same <c>P</c> - is told apart as soon as
/// one byte differs. Each read takes up to <see cref="ReadBufferLength"/> bytes, as the HTTP/1.x
/// head reader's first read does, so a request that arrives at once is read at once; the bytes
/// read are handed back for the HTTP/1.x reader, or past the preface for the HTTP/2 frame
/// reader, to read first.
/// </remarks>
internal sealed class Http2ConnectionPreface
{
    /// <summary>The most bytes read in all by default: 4096, the HTTP/1.x head reader's first buffer.</summary>
    public const int ReadBufferLength = 4096;

    private readonly byte[] bytesRead;
    private int count;

    /// <summary>
    /// Creates a reader that reads at most <paramref name="readLimit"/> bytes in all: the default
    /// <see cref="ReadBufferLength"/> where the bytes after the preface are handed on, or the
    /// preface's own length where the connection's later bytes must stay unread, as after an
    /// <c>Upgrade: h2c</c>'s <c>101</c> (ADR-0091).
    /// </summary>
    /// <param name="readLimit">The most bytes read in all; at least the preface's length.</param>
    public Http2ConnectionPreface(int readLimit = ReadBufferLength)
    {
        bytesRead = new byte[readLimit];
    }

    /// <summary>The preface's 24 bytes.</summary>
    public static ReadOnlySpan<byte> Preface => "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8;

    /// <summary>The bytes read so far.</summary>
    public ReadOnlyMemory<byte> BytesRead => bytesRead.AsMemory(0, count);

    /// <summary>Whether the bytes read start with the whole preface.</summary>
    public bool IsComplete => BytesRead.Span.StartsWith(Preface);

    /// <summary>The bytes read after the preface: the start of the client's first frames.</summary>
    public ReadOnlyMemory<byte> BytesReadAfterPreface => BytesRead[Preface.Length..];

    /// <summary>
    /// Reads until the bytes read stop matching the preface, match all of it, or the client
    /// half-closes. The bytes read stay in <see cref="BytesRead"/> if the read is cut off.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>A task that completes when the read is over.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    public async Task ReadAsync(IConnection connection, CancellationToken cancellationToken)
    {
        while (count < Preface.Length && bytesRead.AsSpan(0, count).SequenceEqual(Preface[..count]))
        {
            var read = await connection.ReadAsync(bytesRead.AsMemory(count), cancellationToken);
            if (read == 0)
            {
                return;
            }

            count += read;
        }
    }
}
