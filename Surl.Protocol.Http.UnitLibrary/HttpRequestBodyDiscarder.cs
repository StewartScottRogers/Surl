using System.Globalization;
using System.Security.Cryptography;
using Surl.HttpMessage;

namespace Surl.Protocol.Http;

/// <summary>
/// Reads a request body the server does not keep and throws its bytes away, so the next
/// request on a persistent connection can be found after it, and refuses one past the upload
/// limit (ADR-0006, sections 1 and 5); for a login that binds the body, it hashes the bytes
/// on their way out (ADR-0045).
/// </summary>
/// <remarks>
/// A chunked body (RFC 9112, section 7.1) counts its chunk data and its trailer field lines
/// against the upload limit, and is refused as soon as a chunk-size line or a trailer line
/// takes the count past it, before that chunk's data is read. A chunk-size line or trailer
/// line may take at most <see cref="MaxChunkLineBytes"/>, its line ending included, and must end with CRLF:
/// a bare LF, or whitespace before the chunk size, makes the body malformed, because such
/// leniencies are where two parsers disagree about where a message ends.
/// </remarks>
internal sealed class HttpRequestBodyDiscarder
{
    /// <summary>
    /// The most bytes one chunk-size line or trailer field line may take: 8192, the line
    /// limit ADR-0006 gives the line-oriented protocols.
    /// </summary>
    public const int MaxChunkLineBytes = 8192;

    private const int DiscardBufferBytes = 8192;

    private readonly HttpConnectionReader reader;
    private readonly long maxUploadBytes;
    private readonly byte[] discardBuffer = new byte[DiscardBufferBytes];
    private long countedBytes;
    private IncrementalHash? bodyHash;

    /// <summary>
    /// Creates a discarder that reads through <paramref name="reader"/>.
    /// </summary>
    /// <param name="reader">The connection's reader, standing just after a request head.</param>
    /// <param name="maxUploadBytes">The upload limit; 0 means no limit.</param>
    public HttpRequestBodyDiscarder(HttpConnectionReader reader, long maxUploadBytes)
    {
        this.reader = reader;
        this.maxUploadBytes = maxUploadBytes;
    }

    /// <summary>
    /// Reads and discards the body <paramref name="framing"/> declares, hashing each body byte
    /// into <paramref name="bodyHash"/> first when one is given: the content bytes, without the
    /// chunked coding's size lines and trailer (ADR-0045).
    /// </summary>
    /// <param name="framing">The body's framing; <see cref="HttpRequestBodyFramingKind.Unreadable"/> is never passed.</param>
    /// <param name="bodyHash">Where the body's bytes are hashed, or <see langword="null"/> when nothing needs them.</param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>How the discard ended.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async Task<HttpRequestBodyDiscardOutcome> DiscardAsync(HttpRequestBodyFraming framing, IncrementalHash? bodyHash, CancellationToken cancellationToken)
    {
        countedBytes = 0;
        this.bodyHash = bodyHash;
        try
        {
            return await ReadAndDiscardAsync(framing, cancellationToken);
        }
        finally
        {
            // The caller owns the hash; none is kept past the call.
            this.bodyHash = null;
        }
    }

    private async Task<HttpRequestBodyDiscardOutcome> ReadAndDiscardAsync(HttpRequestBodyFraming framing, CancellationToken cancellationToken)
    {

        if (framing.Kind == HttpRequestBodyFramingKind.ContentLength)
        {
            return await SkipAsync(framing.ContentLength, cancellationToken)
                ? HttpRequestBodyDiscardOutcome.Discarded
                : HttpRequestBodyDiscardOutcome.Incomplete;
        }

        return framing.Kind == HttpRequestBodyFramingKind.Chunked
            ? await DiscardChunkedAsync(cancellationToken)
            : HttpRequestBodyDiscardOutcome.Discarded;
    }

    private async Task<HttpRequestBodyDiscardOutcome> DiscardChunkedAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var sizeLine = await ReadCrlfLineAsync(cancellationToken);
            if (!TryParseChunkSize(sizeLine, out var size))
            {
                return HttpRequestBodyDiscardOutcome.Incomplete;
            }

            if (size == 0)
            {
                return await DiscardTrailerAsync(cancellationToken);
            }

            if (TakesPastLimit(size))
            {
                return HttpRequestBodyDiscardOutcome.TooLarge;
            }

            if (!await DiscardChunkDataAsync(size, cancellationToken))
            {
                return HttpRequestBodyDiscardOutcome.Incomplete;
            }
        }
    }

    // The chunk's data and the CRLF after it; false when either is missing.
    private async Task<bool> DiscardChunkDataAsync(long size, CancellationToken cancellationToken) =>
        await SkipAsync(size, cancellationToken)
        && await ReadCrlfLineAsync(cancellationToken) is [];

    private async Task<HttpRequestBodyDiscardOutcome> DiscardTrailerAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await ReadCrlfLineAsync(cancellationToken);
            if (line is null)
            {
                return HttpRequestBodyDiscardOutcome.Incomplete;
            }

            if (line.Length == 0)
            {
                return HttpRequestBodyDiscardOutcome.Discarded;
            }

            if (TakesPastLimit(line.Length))
            {
                return HttpRequestBodyDiscardOutcome.TooLarge;
            }
        }
    }

    // chunk-size [ chunk-ext ]: hex digits, then optionally BWS ';' and extensions, which are
    // ignored; whitespace is allowed only between the digits and ';' (RFC 9112, section
    // 7.1.1). A size of 16 hex digits with the top bit set parses negative and is refused.
    private static bool TryParseChunkSize(byte[]? line, out long size)
    {
        size = 0;
        if (line is null)
        {
            return false;
        }

        var extension = Array.IndexOf(line, (byte)';');
        var digits = extension < 0 ? line : line.AsSpan(0, extension).TrimEnd(" \t"u8);

        return long.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out size) && size >= 0;
    }

    // One line ended by CRLF, without the CRLF; null when it is too long, the client
    // half-closed first, or it ends with a bare LF.
    private async Task<byte[]?> ReadCrlfLineAsync(CancellationToken cancellationToken)
    {
        var line = await reader.ReadLineAsync(MaxChunkLineBytes, cancellationToken);

        return line is [.., (byte)'\r'] ? line[..^1] : null;
    }

    // Counts bytes against the upload limit; true once they take the count past it.
    private bool TakesPastLimit(long bytes)
    {
        if (maxUploadBytes == 0)
        {
            return false;
        }

        if (bytes > maxUploadBytes - countedBytes)
        {
            return true;
        }

        countedBytes += bytes;

        return false;
    }

    // Reads and drops exactly count bytes; false when the client half-closed first.
    private async Task<bool> SkipAsync(long count, CancellationToken cancellationToken)
    {
        var remaining = count;
        while (remaining > 0)
        {
            var read = await reader.ReadAsync(discardBuffer.AsMemory(0, (int)Math.Min(remaining, discardBuffer.Length)), cancellationToken);
            if (read == 0)
            {
                return false;
            }

            bodyHash?.AppendData(discardBuffer, 0, read);
            remaining -= read;
        }

        return true;
    }
}
