using System.Security.Cryptography;
using Surl.HttpServing;

namespace Surl.Protocol.Http;

/// <summary>
/// The <see cref="IHttpRequestBody"/> of a request on one HTTP/2 stream (RFC 9113 section 8.1;
/// ADR-0081, decision 7): the stream's <c>DATA</c>, read through
/// <see cref="Http2RequestStream.ReadBodyAsync"/>, which gives the client back window as each
/// piece is taken, counted against the upload limit.
/// </summary>
/// <remarks>
/// <para>
/// A <c>content-length</c> the <c>DATA</c> does not match ends the read as
/// <see cref="HttpRequestBodyDiscardOutcome.Incomplete"/>, as does a body the client reset or cut
/// off; the stream then says <see cref="Http2RequestStream.HasContentLengthMismatch"/>.
/// </para>
/// <para>
/// When the request expects <c>100-continue</c>, an interim <c>:status 100</c> is sent before
/// the first read, as ADR-0027 answers HTTP/1.1.
/// </para>
/// </remarks>
/// <param name="stream">The request stream.</param>
/// <param name="expectsContinue">Whether the request expects <c>100-continue</c>.</param>
/// <param name="maxUploadBytes">The upload limit, 0 for none (<c>--max-filesize</c>).</param>
internal sealed class Http2HttpRequestBody(Http2RequestStream stream, bool expectsContinue, long maxUploadBytes) : IHttpRequestBody
{
    private bool isAnnounced = stream.IsRequestBodyAnnounced;

    /// <inheritdoc />
    public bool IsAnnounced => isAnnounced;

    /// <inheritdoc />
    public long? AnnouncedLength => stream.Head.ContentLength;

    /// <summary>Always <see langword="true"/>: HTTP/2 frames every body the same way.</summary>
    public bool IsReadable => true;

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
        await SendContinueIfExpectedAsync();
        long received = 0;
        while (await stream.ReadBodyAsync(cancellationToken) is { } piece)
        {
            received += piece.Length;
            if (IsPastTheUploadLimit(received))
            {
                return HttpRequestBodyDiscardOutcome.TooLarge;
            }

            await KeepAsync(piece, bodyHash, destination, cancellationToken);
        }

        return EndOutcome();
    }

    private async Task SendContinueIfExpectedAsync()
    {
        if (isAnnounced && expectsContinue)
        {
            await stream.WriteHeadersAsync([new(":status", "100")], endStream: false);
        }
    }

    private bool IsPastTheUploadLimit(long received) => maxUploadBytes != 0 && received > maxUploadBytes;

    private HttpRequestBodyDiscardOutcome EndOutcome()
    {
        if (stream.HasRequestBodyEndedEarly || stream.HasContentLengthMismatch)
        {
            return HttpRequestBodyDiscardOutcome.Incomplete;
        }

        isAnnounced = false;

        return HttpRequestBodyDiscardOutcome.Discarded;
    }

    private static async Task KeepAsync(byte[] piece, IncrementalHash? bodyHash, Stream? destination, CancellationToken cancellationToken)
    {
        bodyHash?.AppendData(piece);
        if (destination is not null)
        {
            await destination.WriteAsync(piece, cancellationToken);
        }
    }
}
