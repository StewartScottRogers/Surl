using Surl.Protocol.Abstractions;
using Surl.Qpack;
using Surl.Quic;

namespace Surl.Http3;

/// <summary>
/// One request stream of an HTTP/3 connection, as its <see cref="IHttp3RequestStreamHandler"/>
/// sees it (ADR-0086, decision 2): the validated request head, the request body read piece by
/// piece from the stream's <c>DATA</c> frames, and the response written as <c>HEADERS</c> and
/// <c>DATA</c> frames ending with the stream's <c>FIN</c>, or a reset.
/// </summary>
/// <remarks>
/// <para>
/// The body is read only as the handler asks, so QUIC's flow control holds the client back until
/// it does (RFC 9114 section 4.1; ADR-0082 decision 9). Unknown frame types are skipped. Trailers,
/// a second <c>HEADERS</c>, are decoded and dropped, and must end the stream. A frame RFC 9114 does
/// not allow on a request stream is <c>H3_FRAME_UNEXPECTED</c>, a frame other than <c>DATA</c>
/// longer than <c>--max-message</c> <c>H3_EXCESSIVE_LOAD</c>, and trailers QPACK cannot decode
/// <c>QPACK_DECOMPRESSION_FAILED</c>: each ends the connection, which closes with that code.
/// </para>
/// <para>
/// Writes to a QUIC stream throw no <see cref="IOException"/>; one the client stopped with
/// <c>STOP_SENDING</c> throws <see cref="InvalidOperationException"/>, which the connection notes
/// and the stream's handler need not catch.
/// </para>
/// </remarks>
public sealed class Http3RequestStream
{
    private readonly QuicApplicationConnection connection;
    private readonly Http3StreamReader reader;
    private readonly Http3FieldSections fieldSections;
    private readonly Http3FrameLimit frameLimit;
    private readonly Http3ExtendedConnect extendedConnect;
    private readonly IExchangeLog log;
    private ulong dataRemaining;
    private long received;
    private bool hasEnded;

    /// <summary>
    /// Wraps the request stream <paramref name="reader"/> reads, just after its <c>HEADERS</c>.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="reader">The request stream.</param>
    /// <param name="fieldSections">The connection's QPACK.</param>
    /// <param name="frameLimit">The longest frame other than <c>DATA</c>.</param>
    /// <param name="extendedConnect">The connection's registrations, whose stream signals may not arrive as frames.</param>
    /// <param name="head">The validated request head.</param>
    /// <param name="isBodyAnnounced">Whether the stream may still carry a body.</param>
    /// <param name="log">The exchange log.</param>
    internal Http3RequestStream(
        QuicApplicationConnection connection,
        Http3StreamReader reader,
        Http3FieldSections fieldSections,
        Http3FrameLimit frameLimit,
        Http3ExtendedConnect extendedConnect,
        Http3RequestHead head,
        bool isBodyAnnounced,
        IExchangeLog log)
    {
        this.connection = connection;
        this.reader = reader;
        this.fieldSections = fieldSections;
        this.frameLimit = frameLimit;
        this.extendedConnect = extendedConnect;
        this.log = log;
        Head = head;
        IsRequestBodyAnnounced = isBodyAnnounced;
        hasEnded = !isBodyAnnounced;
    }

    /// <summary>The QUIC stream ID, a client-initiated bidirectional one.</summary>
    public ulong Id => reader.StreamId;

    /// <summary>The request head: pseudo-header fields and regular fields, decoded and validated.</summary>
    public Http3RequestHead Head { get; }

    /// <summary>
    /// Whether the request may carry a body: it announced a <c>content-length</c> above zero or
    /// <c>100-continue</c>, or announced no length and the stream holds more after its <c>HEADERS</c>.
    /// </summary>
    public bool IsRequestBodyAnnounced { get; }

    /// <summary>Whether the stream has not been read to its end: the client may still send on it.</summary>
    public bool IsRequestBodyStillArriving => !hasEnded;

    /// <summary>Whether the client reset the stream, or the connection ended, before the body ended.</summary>
    public bool HasRequestBodyEndedEarly { get; private set; }

    /// <summary>
    /// Whether the body, read to its end, did not add up to the request's <c>content-length</c>:
    /// the request is malformed (RFC 9114 section 4.1.2), which a handler answers with
    /// <see cref="ResetAsync"/> <see cref="Http3ErrorCode.MessageError"/>.
    /// </summary>
    public bool HasContentLengthMismatch { get; private set; }

    /// <summary>Whether the server has stopped the client's half: a reset, or <c>STOP_SENDING</c>.</summary>
    internal bool HasStoppedRequest { get; private set; }

    /// <summary>
    /// Reads the next piece of the request body: some or all of a <c>DATA</c> frame's content.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The next piece, or <see langword="null"/> once the body has ended, whole or early.</returns>
    public async Task<byte[]?> ReadBodyAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await ReadPieceOrEndAsync(cancellationToken);
        }
        catch (Http3StreamAbandonedException)
        {
            hasEnded = true;
            HasRequestBodyEndedEarly = true;
            return null;
        }
    }

    /// <summary>Writes one <c>HEADERS</c> frame, the field lines QPACK-encoded.</summary>
    /// <param name="fieldLines">The lines, pseudo-header fields first, names lowercase.</param>
    /// <param name="endStream">Whether the frame ends the stream with <c>FIN</c>.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes when the frame is written.</returns>
    public async Task WriteHeadersAsync(IEnumerable<QpackFieldLine> fieldLines, bool endStream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fieldLines);
        await connection.WriteAsync(reader.Stream, Http3FrameWriter.Frame(Http3FrameType.Headers, fieldSections.Encode(fieldLines)), endStream, cancellationToken);
    }

    /// <summary>
    /// Writes <paramref name="data"/> as one <c>DATA</c> frame; with <paramref name="endStream"/>,
    /// the stream's <c>FIN</c> goes with it, alone when <paramref name="data"/> is empty.
    /// </summary>
    /// <param name="data">The bytes; empty writes no frame.</param>
    /// <param name="endStream">Whether the write ends the stream with <c>FIN</c>.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes when the bytes are written.</returns>
    public async Task WriteDataAsync(ReadOnlyMemory<byte> data, bool endStream, CancellationToken cancellationToken)
    {
        if (!data.IsEmpty)
        {
            await connection.WriteAsync(reader.Stream, Http3FrameWriter.Frame(Http3FrameType.Data, data.Span), endStream, cancellationToken);
        }
        else if (endStream)
        {
            await connection.WriteAsync(reader.Stream, ReadOnlyMemory<byte>.Empty, true, cancellationToken);
        }
    }

    /// <summary>
    /// Creates a write-only stream each write to which goes out as one <c>DATA</c> frame, so a file
    /// can be copied straight onto the request stream; <see cref="WriteDataAsync"/> with no bytes
    /// sends the <c>FIN</c> after it.
    /// </summary>
    /// <returns>The write-only body stream.</returns>
    public Stream CreateResponseBodyStream() =>
        new Http3DataStream((bytes, token) => new(WriteDataAsync(bytes, false, token)));

    /// <summary>
    /// A stream error (RFC 9114 section 8): resets the response side with <c>RESET_STREAM</c>
    /// <paramref name="code"/> and asks the client to stop with <c>STOP_SENDING</c> <paramref name="code"/>.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="reason">Why, for the exchange log; never sent.</param>
    /// <param name="cancellationToken">Cuts the writes off.</param>
    /// <returns>A task that completes when both frames are queued.</returns>
    public async Task ResetAsync(Http3ErrorCode code, string reason, CancellationToken cancellationToken)
    {
        log.Note($"Stream {Id} was reset with {code}: {reason}.");
        await connection.ResetAsync(reader.Stream, (ulong)code, cancellationToken);
        await StopRequestAsync(code, cancellationToken);
    }

    /// <summary>
    /// Ends the response side alone with <c>RESET_STREAM</c> <paramref name="code"/>: an answer that
    /// cannot be finished once its <c>HEADERS</c> went out. The client's half is left to the
    /// connection, which stops it when it is still open.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes when the frame is queued.</returns>
    public async Task ResetResponseAsync(Http3ErrorCode code, CancellationToken cancellationToken) =>
        await connection.ResetAsync(reader.Stream, (ulong)code, cancellationToken);

    /// <summary>Asks the client to stop sending on the stream with <c>STOP_SENDING</c> <paramref name="code"/>.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes when the frame is queued.</returns>
    internal async Task StopRequestAsync(Http3ErrorCode code, CancellationToken cancellationToken)
    {
        HasStoppedRequest = true;
        await connection.StopSendingAsync(reader.Stream, (ulong)code, cancellationToken);
    }

    // A piece of the current DATA frame, or the next frame read, until the stream ends.
    private async Task<byte[]?> ReadPieceOrEndAsync(CancellationToken cancellationToken)
    {
        while (!hasEnded)
        {
            if (dataRemaining > 0)
            {
                var piece = await reader.ReadPieceAsync((long)Math.Min(dataRemaining, int.MaxValue), cancellationToken);
                dataRemaining -= (ulong)piece.Length;
                received += piece.Length;
                return piece;
            }

            await ReadFrameAsync(cancellationToken);
        }

        HasContentLengthMismatch = !HasRequestBodyEndedEarly && Head.ContentLength is { } length && length != received;
        return null;
    }

    // Reads one frame header, or the stream's end, and whatever of the frame is not body content.
    private async Task ReadFrameAsync(CancellationToken cancellationToken)
    {
        if (await reader.ReadFrameHeaderAsync(cancellationToken) is not { } header)
        {
            hasEnded = true;
            return;
        }

        if (header.Type == Http3FrameType.Data)
        {
            dataRemaining = header.Length;
            return;
        }

        if (Http3FrameType.IsUnexpectedOnRequestStream(header.Type))
        {
            throw new Http3ConnectionException(Http3ErrorCode.FrameUnexpected, $"a frame of type 0x{header.Type:x} arrived on request stream {Id}");
        }

        if (extendedConnect.HandlerForSignal(header.Type) is not null)
        {
            throw new Http3ConnectionException(Http3ErrorCode.FrameError, $"the stream signal 0x{header.Type:x} arrived on request stream {Id} after its first frame");
        }

        frameLimit.Check(header, Id);
        await (header.Type == Http3FrameType.Headers ? ReadTrailersAsync(header, cancellationToken) : reader.SkipAsync(header.Length, cancellationToken).AsTask());
    }

    // Trailers are decoded, so QPACK's rules hold, then dropped; nothing may follow them.
    private async Task ReadTrailersAsync(Http3FrameHeader header, CancellationToken cancellationToken)
    {
        var section = await reader.ReadExactlyAsync((int)header.Length, cancellationToken);
        if (!fieldSections.Decode(Id, section).IsDecoded)
        {
            throw new Http3ConnectionException(Http3ErrorCode.QpackDecompressionFailed, $"the trailers of stream {Id} could not be decoded");
        }

        if (await reader.ReadFrameHeaderAsync(cancellationToken) is { } after)
        {
            throw new Http3ConnectionException(Http3ErrorCode.FrameUnexpected, $"a frame of type 0x{after.Type:x} followed the trailers of stream {Id}");
        }

        hasEnded = true;
    }
}
