using System.Globalization;
using System.Text;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// Moves a message body from one side of the proxy to the other (ADR-0088 decision 3): a
/// <c>Content-Length</c> body streamed through, a chunked body read chunk by chunk and written
/// on chunked (or, towards an HTTP/2 stream, as its data alone), a close-delimited body copied
/// until the sender closes.
/// </summary>
internal static class ProxyBodyCopier
{
    private const int CopyBufferBytes = 16384;

    private const int MaxChunkLineBytes = 8192;

    /// <summary>
    /// Copies exactly <paramref name="length"/> bytes.
    /// </summary>
    /// <param name="source">Where the body is read.</param>
    /// <param name="destination">Where it is written.</param>
    /// <param name="length">How many bytes the body holds.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns><see langword="false"/> when the source closed before the body ended.</returns>
    public static Task<bool> CopyLengthAsync(HttpConnectionReader source, IConnection destination, long length, CancellationToken cancellationToken) =>
        CopyLengthAsync(source, destination.WriteAsync, length, cancellationToken);

    /// <summary>
    /// Copies exactly <paramref name="length"/> bytes, each piece handed to <paramref name="write"/>.
    /// </summary>
    /// <param name="source">Where the body is read.</param>
    /// <param name="write">Writes one piece where the body goes.</param>
    /// <param name="length">How many bytes the body holds.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns><see langword="false"/> when the source closed before the body ended.</returns>
    public static async Task<bool> CopyLengthAsync(HttpConnectionReader source, Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write, long length, CancellationToken cancellationToken)
    {
        var buffer = new byte[CopyBufferBytes];
        var left = length;
        while (left > 0)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(left, buffer.Length)), cancellationToken);
            if (read == 0)
            {
                return false;
            }

            await write(buffer.AsMemory(0, read), cancellationToken);
            left -= read;
        }

        return true;
    }

    /// <summary>
    /// Copies every byte until the source closes.
    /// </summary>
    /// <param name="source">Where the body is read.</param>
    /// <param name="destination">Where it is written.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>A task that completes when the source has closed.</returns>
    public static Task CopyUntilClosedAsync(HttpConnectionReader source, IConnection destination, CancellationToken cancellationToken) =>
        CopyUntilClosedAsync(source, destination.WriteAsync, cancellationToken);

    /// <summary>
    /// Copies every byte until the source closes, each piece handed to <paramref name="write"/>.
    /// </summary>
    /// <param name="source">Where the body is read.</param>
    /// <param name="write">Writes one piece where the body goes.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>A task that completes when the source has closed.</returns>
    public static async Task CopyUntilClosedAsync(HttpConnectionReader source, Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write, CancellationToken cancellationToken)
    {
        var buffer = new byte[CopyBufferBytes];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await write(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    /// <summary>
    /// Reads a chunked body (RFC 9112 section 7.1) and writes it on chunked: each chunk's size
    /// in hexadecimal without extensions, its data, and the last chunk with no trailer fields.
    /// </summary>
    /// <param name="source">Where the chunked body is read.</param>
    /// <param name="destination">Where it is written.</param>
    /// <param name="maxBodyBytes">The most data bytes the body may hold; 0 means no limit.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>How the copy ended.</returns>
    public static Task<ChunkedCopyOutcome> CopyChunkedAsync(HttpConnectionReader source, IConnection destination, long maxBodyBytes, CancellationToken cancellationToken) =>
        CopyChunksAsync(source, destination.WriteAsync, rechunks: true, maxBodyBytes, cancellationToken);

    /// <summary>
    /// Reads a chunked body (RFC 9112 section 7.1) and hands <paramref name="write"/> its data
    /// alone, without the chunk framing: what an HTTP/2 stream carries in <c>DATA</c>.
    /// </summary>
    /// <param name="source">Where the chunked body is read.</param>
    /// <param name="write">Writes one piece of the data where the body goes.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>How the copy ended.</returns>
    public static Task<ChunkedCopyOutcome> CopyChunkedDataAsync(HttpConnectionReader source, Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write, CancellationToken cancellationToken) =>
        CopyChunksAsync(source, write, rechunks: false, 0, cancellationToken);

    private static async Task<ChunkedCopyOutcome> CopyChunksAsync(HttpConnectionReader source, Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write, bool rechunks, long maxBodyBytes, CancellationToken cancellationToken)
    {
        long total = 0;
        long? size;
        while ((size = ParseChunkSize(await source.ReadLineAsync(MaxChunkLineBytes, cancellationToken))) is > 0)
        {
            total += size.Value;
            if (maxBodyBytes > 0 && total > maxBodyBytes)
            {
                return ChunkedCopyOutcome.TooLarge;
            }

            await WriteFramingAsync(write, rechunks, size.Value.ToString("x", CultureInfo.InvariantCulture) + "\r\n", cancellationToken);
            if (!await CopyLengthAsync(source, write, size.Value, cancellationToken) || !IsEmptyLine(await source.ReadLineAsync(MaxChunkLineBytes, cancellationToken)))
            {
                return ChunkedCopyOutcome.Malformed;
            }

            await WriteFramingAsync(write, rechunks, "\r\n", cancellationToken);
        }

        if (size is null || !await SkipTrailerAsync(source, cancellationToken))
        {
            return ChunkedCopyOutcome.Malformed;
        }

        await WriteFramingAsync(write, rechunks, "0\r\n\r\n", cancellationToken);

        return ChunkedCopyOutcome.Copied;
    }

    // The chunk sizes and line ends, written only when the body goes on chunked.
    private static async Task WriteFramingAsync(Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write, bool rechunks, string framing, CancellationToken cancellationToken)
    {
        if (rechunks)
        {
            await write(Encoding.ASCII.GetBytes(framing), cancellationToken);
        }
    }

    // Trailer fields are read and dropped up to the empty line (the Trailer field is hop-by-hop
    // here, ADR-0088 decision 3); false when the source closed first.
    private static async Task<bool> SkipTrailerAsync(HttpConnectionReader source, CancellationToken cancellationToken)
    {
        byte[]? line;
        do
        {
            line = await source.ReadLineAsync(MaxChunkLineBytes, cancellationToken);
        }
        while (line is not null && !IsEmptyLine(line));

        return line is not null;
    }

    private static bool IsEmptyLine(byte[]? line) => line is [] or [(byte)'\r'];

    // The size before any ';' extension, in hexadecimal; null for a missing or malformed line.
    private static long? ParseChunkSize(byte[]? line)
    {
        var text = line is null ? string.Empty : Encoding.Latin1.GetString(line).TrimEnd('\r');
        var extension = text.IndexOf(';');
        var sizeText = (extension < 0 ? text : text[..extension]).TrimEnd(' ', '\t');

        return sizeText.Length is > 0 and <= 15 && long.TryParse(sizeText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var size)
            ? size
            : null;
    }
}
