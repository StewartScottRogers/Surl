using System.Security.Cryptography;
using Surl.HttpServing;

namespace Surl.Protocol.Http;

/// <summary>
/// The body of a request on one HTTP/3 request stream: the <c>DATA</c> frames after its
/// <c>HEADERS</c>, read from the stream as the handler asks for them (RFC 9114 section 4.1;
/// ADR-0082 decision 9), so QUIC's flow control holds the client back until the handler reads.
/// </summary>
/// <remarks>
/// <para>
/// Unknown frame types are skipped. Trailers, a second <c>HEADERS</c>, are decoded and dropped, and
/// must end the stream. Any frame RFC 9114 does not allow on a request stream is
/// <c>H3_FRAME_UNEXPECTED</c>, and a frame other than <c>DATA</c> longer than <c>--max-message</c>
/// <c>H3_EXCESSIVE_LOAD</c>, both thrown as an <see cref="Http3ConnectionException"/>.
/// </para>
/// <para>
/// The content is counted against the upload limit; past it, the read stops at once as
/// <see cref="HttpRequestBodyDiscardOutcome.TooLarge"/> and the body is still arriving. A
/// <c>content-length</c> the <c>DATA</c> does not match ends the read as
/// <see cref="HttpRequestBodyDiscardOutcome.Incomplete"/> and sets
/// <see cref="HasContentLengthMismatch"/>, which the stream answers <c>H3_MESSAGE_ERROR</c>. A
/// stream the client resets, or a connection that ends, is
/// <see cref="HttpRequestBodyDiscardOutcome.Incomplete"/> too. When the request expects
/// <c>100-continue</c>, the interim <c>:status 100</c> goes before the first read.
/// </para>
/// </remarks>
/// <param name="reader">The request stream, just after its <c>HEADERS</c>.</param>
/// <param name="fieldSections">Decodes trailers.</param>
/// <param name="frameLimit">The longest frame other than <c>DATA</c>.</param>
/// <param name="head">The request's head.</param>
/// <param name="isAnnounced">Whether the stream may still carry a body.</param>
/// <param name="maxUploadBytes">The upload limit, 0 for none (<c>--max-filesize</c>).</param>
/// <param name="sendContinueAsync">Sends the interim <c>:status 100</c>.</param>
internal sealed class Http3RequestBody(
    Http3StreamReader reader,
    Http3FieldSections fieldSections,
    Http3FrameLimit frameLimit,
    Http2RequestHead head,
    bool isAnnounced,
    long maxUploadBytes,
    Func<Task> sendContinueAsync) : IHttpRequestBody
{
    private bool isStillAnnounced = isAnnounced;
    private bool hasEnded = !isAnnounced;

    /// <inheritdoc />
    public bool IsAnnounced => isStillAnnounced;

    /// <inheritdoc />
    public long? AnnouncedLength => head.ContentLength;

    /// <summary>Always <see langword="true"/>: HTTP/3 frames every body the same way.</summary>
    public bool IsReadable => true;

    /// <summary>Whether the stream has not been read to its end: the client may still send on it.</summary>
    public bool IsStillArriving => !hasEnded;

    /// <summary>Whether the <c>DATA</c> read did not add up to the request's <c>content-length</c>.</summary>
    public bool HasContentLengthMismatch { get; private set; }

    /// <inheritdoc />
    public Task<HttpRequestBodyDiscardOutcome> DiscardAsync(IncrementalHash? bodyHash, CancellationToken cancellationToken) =>
        ReadToEndAsync(bodyHash, null, cancellationToken);

    /// <inheritdoc />
    public Task<HttpRequestBodyDiscardOutcome> CopyToAsync(Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return ReadToEndAsync(null, destination, cancellationToken);
    }

    // A body already read whole, or never announced, reads as empty and whole.
    private async Task<HttpRequestBodyDiscardOutcome> ReadToEndAsync(IncrementalHash? bodyHash, Stream? destination, CancellationToken cancellationToken)
    {
        if (!isStillAnnounced)
        {
            return HttpRequestBodyDiscardOutcome.Discarded;
        }

        if (head.ExpectsContinue)
        {
            await sendContinueAsync();
        }

        long received;
        try
        {
            received = await ReadFramesAsync(bodyHash, destination, cancellationToken);
        }
        catch (Http3StreamAbandonedException)
        {
            hasEnded = true;
            return HttpRequestBodyDiscardOutcome.Incomplete;
        }

        return IsPastUploadLimit(received) ? HttpRequestBodyDiscardOutcome.TooLarge : Outcome(received);
    }

    // Reads frames until the stream ends or the content passes the upload limit; returns the content received.
    private async Task<long> ReadFramesAsync(IncrementalHash? bodyHash, Stream? destination, CancellationToken cancellationToken)
    {
        long received = 0;
        while (!hasEnded && !IsPastUploadLimit(received))
        {
            received = await ReadFrameAsync(received, bodyHash, destination, cancellationToken);
        }

        return received;
    }

    private bool IsPastUploadLimit(long received) => maxUploadBytes != 0 && received > maxUploadBytes;

    private HttpRequestBodyDiscardOutcome Outcome(long received)
    {
        HasContentLengthMismatch = head.ContentLength is { } length && length != received;
        if (HasContentLengthMismatch)
        {
            return HttpRequestBodyDiscardOutcome.Incomplete;
        }

        isStillAnnounced = false;
        return HttpRequestBodyDiscardOutcome.Discarded;
    }

    // Reads one frame, or the stream's end, and returns the content received so far.
    private async Task<long> ReadFrameAsync(long received, IncrementalHash? bodyHash, Stream? destination, CancellationToken cancellationToken)
    {
        if (await reader.ReadFrameHeaderAsync(cancellationToken) is not { } header)
        {
            hasEnded = true;
            return received;
        }

        if (header.Type == Http3FrameType.Data)
        {
            return await ReadDataAsync(header.Length, received, bodyHash, destination, cancellationToken);
        }

        RefuseUnexpected(header);
        frameLimit.Check(header, reader.StreamId);
        if (header.Type == Http3FrameType.Headers)
        {
            await ReadTrailersAsync(header, cancellationToken);
        }
        else
        {
            await reader.SkipAsync(header.Length, cancellationToken);
        }

        return received;
    }

    private void RefuseUnexpected(Http3FrameHeader header)
    {
        if (Http3FrameType.IsUnexpectedOnRequestStream(header.Type))
        {
            throw new Http3ConnectionException(Http3ErrorCode.FrameUnexpected, $"a frame of type 0x{header.Type:x} arrived on request stream {reader.StreamId}");
        }
    }

    // Reads the DATA frame's content until it is whole or the upload limit is passed.
    private async Task<long> ReadDataAsync(ulong length, long received, IncrementalHash? bodyHash, Stream? destination, CancellationToken cancellationToken)
    {
        var remaining = length;
        while (remaining > 0 && !IsPastUploadLimit(received))
        {
            var piece = await reader.ReadPieceAsync((long)Math.Min(remaining, int.MaxValue), cancellationToken);
            remaining -= (ulong)piece.Length;
            received += piece.Length;
            bodyHash?.AppendData(piece);
            if (destination is not null)
            {
                await destination.WriteAsync(piece, cancellationToken);
            }
        }

        return received;
    }

    // Trailers are decoded, so QPACK's rules hold, then dropped; nothing may follow them.
    private async Task ReadTrailersAsync(Http3FrameHeader header, CancellationToken cancellationToken)
    {
        var section = await reader.ReadExactlyAsync((int)header.Length, cancellationToken);
        if (!fieldSections.Decode(reader.StreamId, section).IsDecoded)
        {
            throw new Http3ConnectionException(Http3ErrorCode.QpackDecompressionFailed, $"the trailers of stream {reader.StreamId} could not be decoded");
        }

        if (await reader.ReadFrameHeaderAsync(cancellationToken) is { } after)
        {
            throw new Http3ConnectionException(Http3ErrorCode.FrameUnexpected, $"a frame of type 0x{after.Type:x} followed the trailers of stream {reader.StreamId}");
        }

        hasEnded = true;
    }
}
