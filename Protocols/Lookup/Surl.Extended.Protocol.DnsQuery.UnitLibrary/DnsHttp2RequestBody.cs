using System.Security.Cryptography;
using Surl.Http2;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// An HTTP/2 request stream's body as an <see cref="IHttpRequestBody"/>, so
/// <see cref="DnsOverHttpsHandler"/> decides an HTTP/2 request as it decides an HTTP/1.1 one
/// (ADR-0092 decision 5): read piece by piece as its <c>DATA</c> arrives. A body the client
/// reset or closed before its end is <see cref="HttpRequestBodyDiscardOutcome.Incomplete"/>.
/// </summary>
/// <param name="stream">The request stream.</param>
internal sealed class DnsHttp2RequestBody(Http2RequestStream stream) : IHttpRequestBody
{
    private bool isRead;

    public bool IsAnnounced => stream.IsRequestBodyAnnounced && !isRead;

    public long? AnnouncedLength => stream.Head.ContentLength;

    public bool IsReadable => true;

    public Task<HttpRequestBodyDiscardOutcome> DiscardAsync(IncrementalHash? bodyHash, CancellationToken cancellationToken) =>
        ReadAsync(
            piece =>
            {
                bodyHash?.AppendData(piece);
                return ValueTask.CompletedTask;
            },
            cancellationToken);

    public Task<HttpRequestBodyDiscardOutcome> CopyToAsync(Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return ReadAsync(piece => destination.WriteAsync(piece, cancellationToken), cancellationToken);
    }

    private async Task<HttpRequestBodyDiscardOutcome> ReadAsync(Func<byte[], ValueTask> keep, CancellationToken cancellationToken)
    {
        while (await stream.ReadBodyAsync(cancellationToken) is { } piece)
        {
            await keep(piece);
        }

        isRead = true;
        return stream.HasRequestBodyEndedEarly ? HttpRequestBodyDiscardOutcome.Incomplete : HttpRequestBodyDiscardOutcome.Discarded;
    }
}
