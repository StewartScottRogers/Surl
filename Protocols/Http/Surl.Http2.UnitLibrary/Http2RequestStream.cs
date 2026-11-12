using Surl.Hpack;

namespace Surl.Http2;

/// <summary>
/// One request stream of an HTTP/2 connection, as its <see cref="IHttp2StreamHandler"/> sees it
/// (ADR-0086, decision 1): the validated request head, the request body read piece by piece, and
/// the response written as header blocks, <c>DATA</c> and trailers, or a reset.
/// </summary>
/// <remarks>
/// Reading and writing are independent, so a response may start before the request body ends.
/// Header blocks and frames are written whole, never interleaved with another stream's, and
/// <c>DATA</c> only as the client's flow-control windows allow (RFC 9113 sections 4.3 and 6.9).
/// Writes are cut off by the connection's end; waits for window by the stream's too.
/// </remarks>
public sealed class Http2RequestStream
{
    private readonly Http2Stream stream;
    private readonly Http2FrameWriter frames;
    private readonly Http2SendWindows windows;
    private readonly Func<Http2ErrorCode, string, Task> resetAsync;
    private readonly CancellationToken connectionToken;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Wraps <paramref name="stream"/> for its handler.
    /// </summary>
    /// <param name="stream">The connection's own record of the stream.</param>
    /// <param name="head">The validated request head.</param>
    /// <param name="frames">Writes the connection's frames.</param>
    /// <param name="windows">The connection's send windows.</param>
    /// <param name="resetAsync">Resets the stream through the connection, with a reason for its log.</param>
    /// <param name="connectionToken">The connection's cancellation, which cuts every write off.</param>
    /// <param name="timeProvider">The exchange's clock, which times a farewell write's deadline.</param>
    internal Http2RequestStream(
        Http2Stream stream,
        Http2RequestHead head,
        Http2FrameWriter frames,
        Http2SendWindows windows,
        Func<Http2ErrorCode, string, Task> resetAsync,
        CancellationToken connectionToken,
        TimeProvider timeProvider)
    {
        this.stream = stream;
        Head = head;
        this.frames = frames;
        this.windows = windows;
        this.resetAsync = resetAsync;
        this.connectionToken = connectionToken;
        this.timeProvider = timeProvider;
    }

    /// <summary>The stream identifier, odd.</summary>
    public int Id => stream.Id;

    /// <summary>The request head: pseudo-header fields and regular fields, decoded and validated.</summary>
    public Http2RequestHead Head { get; }

    /// <summary>Whether the request's <c>HEADERS</c> left the stream open for a body.</summary>
    public bool IsRequestBodyAnnounced => stream.Body.IsAnnounced;

    /// <summary>
    /// Whether the client may still send <c>DATA</c>: it has not ended its half of the stream,
    /// reset it, or closed the connection.
    /// </summary>
    public bool IsRequestBodyStillArriving => stream.Body.IsStillArriving;

    /// <summary>Whether the client reset the stream or closed the connection before ending the body.</summary>
    public bool HasRequestBodyEndedEarly => stream.Body.HasEndedEarly;

    /// <summary>
    /// Whether the body, read to its end, did not add up to the request's <c>content-length</c>:
    /// the request is malformed (RFC 9113 section 8.1.1), which a handler answers with
    /// <see cref="ResetAsync"/> <see cref="Http2ErrorCode.ProtocolError"/>.
    /// </summary>
    public bool HasContentLengthMismatch => stream.Body.HasContentLengthMismatch;

    /// <summary>
    /// Reads the next piece of the request body, first giving the client back the window the
    /// piece read before took.
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>The next piece, or <see langword="null"/> once the body has ended, whole or early.</returns>
    public Task<byte[]?> ReadBodyAsync(CancellationToken cancellationToken) => stream.Body.ReadAsync(cancellationToken);

    /// <summary>
    /// Writes one header block on the stream: <c>HEADERS</c> and, past one frame, <c>CONTINUATION</c>s.
    /// </summary>
    /// <param name="fields">The fields, pseudo-header fields first, names lowercase.</param>
    /// <param name="endStream">Whether the block ends the stream.</param>
    /// <returns>A task that completes when the block is written.</returns>
    /// <exception cref="OperationCanceledException">The connection ended.</exception>
    public Task WriteHeadersAsync(IEnumerable<HpackHeaderField> fields, bool endStream) =>
        frames.WriteHeaderBlockAsync(Id, fields, endStream, connectionToken);

    /// <summary>
    /// Writes <paramref name="data"/> as <c>DATA</c> frames, waiting for window as needed; with
    /// <paramref name="endStream"/>, the last frame ends the stream, an empty one when
    /// <paramref name="data"/> is empty.
    /// </summary>
    /// <param name="data">The bytes.</param>
    /// <param name="endStream">Whether the last frame ends the stream.</param>
    /// <returns>A task that completes when every byte is written.</returns>
    /// <exception cref="OperationCanceledException">The stream or the connection ended.</exception>
    /// <exception cref="IOException">The client closed before granting the window the bytes need.</exception>
    public async Task WriteDataAsync(ReadOnlyMemory<byte> data, bool endStream)
    {
        if (data.IsEmpty)
        {
            await frames.WriteAsync(new Http2DataFrame(Id, [], endStream), connectionToken);

            return;
        }

        await using var body = CreateResponseBodyStream(endStream ? data.Length : long.MaxValue);
        await body.WriteAsync(data);
    }

    /// <summary>
    /// Writes the trailers: a header block whose <c>HEADERS</c> carries <c>END_STREAM</c>, after
    /// the <c>DATA</c> (RFC 9113 section 8.1).
    /// </summary>
    /// <param name="fields">The trailer fields, names lowercase, no pseudo-header fields.</param>
    /// <returns>A task that completes when the trailers are written.</returns>
    /// <exception cref="OperationCanceledException">The connection ended.</exception>
    public Task WriteTrailersAsync(IEnumerable<HpackHeaderField> fields) => WriteHeadersAsync(fields, endStream: true);

    /// <summary>
    /// Creates the stream a response body of <paramref name="bodyLength"/> bytes is written to as
    /// <c>DATA</c>; the frame that completes it ends the stream.
    /// </summary>
    /// <param name="bodyLength">The body's length, which the response's <c>content-length</c> announced.</param>
    /// <returns>The write-only body stream.</returns>
    public Http2DataStream CreateResponseBodyStream(long bodyLength) =>
        new(frames, windows, Id, bodyLength, stream.Token, connectionToken);

    /// <summary>
    /// Resets the stream with <c>RST_STREAM</c> <paramref name="code"/>: the request body ends
    /// early and anything still writing on the stream is cut off.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="reason">Why, for the exchange log; never sent.</param>
    /// <returns>A task that completes when the reset is written.</returns>
    /// <exception cref="OperationCanceledException">The connection ended.</exception>
    public Task ResetAsync(Http2ErrorCode code, string reason) => resetAsync(code, reason);

    /// <summary>
    /// Whether the exchange was cancelled - shutdown, the idle timeout or the maximum duration
    /// (ADR-0059) - which cuts every other write on the connection off. A handler that catches
    /// the cancellation may then end its stream once with <see cref="WriteFarewellTrailersAsync"/>
    /// or <see cref="WriteFarewellResetAsync"/>, before the connection's farewell <c>GOAWAY</c>.
    /// </summary>
    public bool IsExchangeCancelled => connectionToken.IsCancellationRequested;

    /// <summary>
    /// Ends the stream after the exchange was cancelled with a header block whose <c>HEADERS</c>
    /// carries <c>END_STREAM</c>, written within <see cref="Http2ConnectionServer.FarewellWriteDeadline"/>
    /// of its own, since the exchange's cancellation already cuts every other write off.
    /// </summary>
    /// <param name="fields">The fields: trailers, or a whole Trailers-Only block with <c>:status</c> first.</param>
    /// <returns>A task that completes when the block is written.</returns>
    /// <exception cref="OperationCanceledException">The write did not finish within its deadline.</exception>
    public async Task WriteFarewellTrailersAsync(IEnumerable<HpackHeaderField> fields)
    {
        using var deadline = new CancellationTokenSource(Http2ConnectionServer.FarewellWriteDeadline, timeProvider);
        await frames.WriteHeaderBlockAsync(Id, fields, endStream: true, deadline.Token);
    }

    /// <summary>
    /// Resets the stream with <c>RST_STREAM</c> <paramref name="code"/> after the exchange was
    /// cancelled, written within <see cref="Http2ConnectionServer.FarewellWriteDeadline"/> of its own.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <returns>A task that completes when the reset is written.</returns>
    /// <exception cref="OperationCanceledException">The write did not finish within its deadline.</exception>
    public async Task WriteFarewellResetAsync(Http2ErrorCode code)
    {
        using var deadline = new CancellationTokenSource(Http2ConnectionServer.FarewellWriteDeadline, timeProvider);
        await frames.WriteAsync(new Http2RstStreamFrame(Id, code), deadline.Token);
    }
}
