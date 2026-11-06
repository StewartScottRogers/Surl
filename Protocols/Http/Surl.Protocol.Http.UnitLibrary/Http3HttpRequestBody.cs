using System.Security.Cryptography;
using Surl.HttpServing;

namespace Surl.Protocol.Http;

/// <summary>
/// The <see cref="IHttpRequestBody"/> of a request on one HTTP/3 request stream (RFC 9114
/// section 4.1; ADR-0082 decision 9): the stream's <c>DATA</c>, read through
/// <see cref="Http3RequestStream.ReadBodyAsync"/> as the answerer asks for it, so QUIC's flow
/// control holds the client back until it does, counted against the upload limit.
/// </summary>
/// <remarks>
/// <para>
/// Past the upload limit the read stops at once as <see cref="HttpRequestBodyDiscardOutcome.TooLarge"/>
/// and the body is still arriving. A <c>content-length</c> the <c>DATA</c> does not match ends the
/// read as <see cref="HttpRequestBodyDiscardOutcome.Incomplete"/>, and the stream then says
/// <see cref="Http3RequestStream.HasContentLengthMismatch"/>; a stream the client resets, or a
/// connection that ends, is <see cref="HttpRequestBodyDiscardOutcome.Incomplete"/> too.
/// </para>
/// <para>
/// When the request expects <c>100-continue</c>, the interim <c>:status 100</c> goes before the
/// first read, as ADR-0027 answers HTTP/1.1.
/// </para>
/// </remarks>
/// <param name="stream">The request stream.</param>
/// <param name="maxUploadBytes">The upload limit, 0 for none (<c>--max-filesize</c>).</param>
internal sealed class Http3HttpRequestBody(Http3RequestStream stream, long maxUploadBytes) : IHttpRequestBody
{
    private bool hasBeenReadWhole;

    /// <inheritdoc />
    public bool IsAnnounced => !hasBeenReadWhole && stream.IsRequestBodyAnnounced;

    /// <inheritdoc />
    public long? AnnouncedLength => stream.Head.ContentLength;

    /// <summary>Always <see langword="true"/>: HTTP/3 frames every body the same way.</summary>
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

    // A body already read whole, or never announced, reads as empty and whole.
    private async Task<HttpRequestBodyDiscardOutcome> ReadToEndAsync(IncrementalHash? bodyHash, Stream? destination, CancellationToken cancellationToken)
    {
        if (!IsAnnounced)
        {
            return HttpRequestBodyDiscardOutcome.Discarded;
        }

        if (stream.Head.ExpectsContinue)
        {
            await stream.WriteHeadersAsync([new(":status", "100")], endStream: false, cancellationToken);
        }

        long received = 0;
        while (await stream.ReadBodyAsync(cancellationToken) is { } piece)
        {
            received += piece.Length;
            await KeepAsync(piece, bodyHash, destination, cancellationToken);
            if (maxUploadBytes != 0 && received > maxUploadBytes)
            {
                return HttpRequestBodyDiscardOutcome.TooLarge;
            }
        }

        return EndOutcome();
    }

    private HttpRequestBodyDiscardOutcome EndOutcome()
    {
        if (stream.HasRequestBodyEndedEarly || stream.HasContentLengthMismatch)
        {
            return HttpRequestBodyDiscardOutcome.Incomplete;
        }

        hasBeenReadWhole = true;
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
