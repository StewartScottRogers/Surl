using System.Security.Cryptography;
using System.Threading.Channels;
using Surl.HttpServing;

namespace Surl.Protocol.Http;

/// <summary>
/// The body of a request on one HTTP/2 stream: the stream's <c>DATA</c>, which the connection's
/// reader hands over as it arrives and the request's handler reads (RFC 9113 section 8.1;
/// ADR-0081, decision 7).
/// </summary>
/// <remarks>
/// <para>
/// A body is announced when the request's <c>HEADERS</c> did not end the stream. It is read up
/// to the client's <c>END_STREAM</c>, counted against the upload limit, and each piece read is
/// handed back to <see cref="Http2RequestBody(bool, long?, bool, long, Func{int, Task}, Func{Task})"/>'s
/// replenish callback, which reopens the stream's receive window by that much, so a client never
/// sends more than the handler has room for. A <c>content-length</c> the <c>DATA</c> does not
/// match ends the read as <see cref="HttpRequestBodyDiscardOutcome.Incomplete"/> and sets
/// <see cref="HasContentLengthMismatch"/>, which the stream answers with <c>RST_STREAM</c>
/// <c>PROTOCOL_ERROR</c>.
/// </para>
/// <para>
/// When the request expects <c>100-continue</c>, an interim <c>:status 100</c> is sent before
/// the first read, as ADR-0027 answers HTTP/1.1.
/// </para>
/// </remarks>
internal sealed class Http2RequestBody : IHttpRequestBody
{
    private readonly Channel<byte[]> pieces = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly bool expectsContinue;
    private readonly long maxUploadBytes;
    private readonly Func<int, Task> replenishAsync;
    private readonly Func<Task> sendContinueAsync;
    private bool isAnnounced;
    private volatile bool hasEnded;
    private bool hasEndedEarly;

    /// <summary>
    /// Creates the body of a request whose <c>HEADERS</c> announced one or ended the stream.
    /// </summary>
    /// <param name="isAnnounced">Whether the <c>HEADERS</c> left the stream open for <c>DATA</c>.</param>
    /// <param name="contentLength">The request's <c>content-length</c>, or <see langword="null"/> when it sent none.</param>
    /// <param name="expectsContinue">Whether the request expects <c>100-continue</c>.</param>
    /// <param name="maxUploadBytes">The upload limit, 0 for none (<c>--max-filesize</c>).</param>
    /// <param name="replenishAsync">Reopens the stream's receive window by the bytes just read.</param>
    /// <param name="sendContinueAsync">Sends the interim <c>:status 100</c>.</param>
    public Http2RequestBody(bool isAnnounced, long? contentLength, bool expectsContinue, long maxUploadBytes, Func<int, Task> replenishAsync, Func<Task> sendContinueAsync)
    {
        this.isAnnounced = isAnnounced;
        AnnouncedLength = contentLength;
        this.expectsContinue = expectsContinue;
        this.maxUploadBytes = maxUploadBytes;
        this.replenishAsync = replenishAsync;
        this.sendContinueAsync = sendContinueAsync;
        if (!isAnnounced)
        {
            End();
        }
    }

    /// <inheritdoc />
    public bool IsAnnounced => isAnnounced;

    /// <inheritdoc />
    public long? AnnouncedLength { get; }

    /// <summary>Always <see langword="true"/>: HTTP/2 frames every body the same way.</summary>
    public bool IsReadable => true;

    /// <summary>
    /// Whether the client may still send <c>DATA</c> for the body: it has not ended the stream,
    /// reset it, or closed the connection.
    /// </summary>
    public bool IsStillArriving => !hasEnded;

    /// <summary>Whether the <c>DATA</c> read did not add up to the request's <c>content-length</c>.</summary>
    public bool HasContentLengthMismatch { get; private set; }

    /// <summary>
    /// Hands over one <c>DATA</c> frame's content, padding removed.
    /// </summary>
    /// <param name="data">The bytes.</param>
    public void Receive(byte[] data) => pieces.Writer.TryWrite(data);

    /// <summary>The client's <c>END_STREAM</c>: the body is whole.</summary>
    public void End()
    {
        hasEnded = true;
        pieces.Writer.TryComplete();
    }

    /// <summary>The client reset the stream or closed the connection: the body ends early.</summary>
    public void Abandon()
    {
        if (pieces.Writer.TryComplete())
        {
            hasEndedEarly = true;
        }

        hasEnded = true;
    }

    /// <inheritdoc />
    public Task<HttpRequestBodyDiscardOutcome> DiscardAsync(IncrementalHash? bodyHash, CancellationToken cancellationToken) =>
        ReadToEndAsync(bodyHash, null, cancellationToken);

    /// <inheritdoc />
    public Task<HttpRequestBodyDiscardOutcome> CopyToAsync(Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        return ReadToEndAsync(null, destination, cancellationToken);
    }

    private async Task<HttpRequestBodyDiscardOutcome> ReadToEndAsync(IncrementalHash? bodyHash, Stream? destination, CancellationToken cancellationToken)
    {
        if (isAnnounced && expectsContinue)
        {
            await sendContinueAsync();
        }

        long received = 0;
        await foreach (var piece in pieces.Reader.ReadAllAsync(cancellationToken))
        {
            received += piece.Length;
            if (maxUploadBytes != 0 && received > maxUploadBytes)
            {
                return HttpRequestBodyDiscardOutcome.TooLarge;
            }

            await KeepAsync(piece, bodyHash, destination, cancellationToken);
            await replenishAsync(piece.Length);
        }

        return Outcome(received);
    }

    private static async Task KeepAsync(byte[] piece, IncrementalHash? bodyHash, Stream? destination, CancellationToken cancellationToken)
    {
        bodyHash?.AppendData(piece);
        if (destination is not null)
        {
            await destination.WriteAsync(piece, cancellationToken);
        }
    }

    private HttpRequestBodyDiscardOutcome Outcome(long received)
    {
        HasContentLengthMismatch = !hasEndedEarly && AnnouncedLength is { } length && length != received;
        if (hasEndedEarly || HasContentLengthMismatch)
        {
            return HttpRequestBodyDiscardOutcome.Incomplete;
        }

        isAnnounced = false;

        return HttpRequestBodyDiscardOutcome.Discarded;
    }
}
