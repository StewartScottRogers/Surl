using System.Globalization;
using Surl.Hpack;
using Surl.Http2;

namespace Surl.Protocol.Http;

/// <summary>
/// Forwards each HTTP/2 stream's request to the case's <c>http</c> half as HTTP/1.1 and answers the
/// stream with the reply as nghttpx builds it (ADR-0154 decisions 2 and 3).
/// </summary>
/// <param name="backend">The case's <c>http</c> half.</param>
/// <param name="upgradedBody">
/// The body of the HTTP/1.1 request an <c>Upgrade: h2c</c> carried, already read, which stream 1
/// takes instead of reading one; <see langword="null"/> when the connection was not upgraded.
/// </param>
internal sealed class Http2GatewayStreamForwarder(NghttpxGatewayBackend backend, byte[]? upgradedBody) : IHttp2StreamHandler
{
    private const int UpgradedStreamId = 1;

    public async Task ServeStreamAsync(Http2RequestStream stream, CancellationToken cancellationToken)
    {
        Http2RequestHead head = stream.Head;
        byte[] body = upgradedBody is not null && stream.Id == UpgradedStreamId
            ? upgradedBody
            : await ReadRequestBodyAsync(stream, cancellationToken);
        byte[] request = NghttpxGatewayRequestTranslator.Translate(
            NghttpxGatewayVersion.Http2, head.Method, head.Scheme, head.Authority, head.Target, head.Fields, body);
        bool isHead = head.Method == "HEAD";

        byte[] reply = await backend.ReplyToAsync(request, isHead, cancellationToken);
        if (NghttpxGatewayReplyFramingReader.Read(reply, isHead) == NghttpxGatewayReplyFraming.Short)
        {
            await stream.ResetAsync(Http2ErrorCode.InternalError, "the backend closed in the middle of the reply body");

            return;
        }

        NghttpxGatewayResponse response = NghttpxGatewayResponseTranslator.Translate(NghttpxGatewayVersion.Http2, reply) ?? NghttpxGatewayBackend.BadGateway();
        await WriteAsync(stream, response, isHead);
    }

    private static async Task<byte[]> ReadRequestBodyAsync(Http2RequestStream stream, CancellationToken cancellationToken)
    {
        using MemoryStream body = new();

        // A request whose HEADERS ended the stream has no body to wait for.
        while (stream.IsRequestBodyAnnounced && await stream.ReadBodyAsync(cancellationToken) is { } piece)
        {
            body.Write(piece);
        }

        return body.ToArray();
    }

    private static async Task WriteAsync(Http2RequestStream stream, NghttpxGatewayResponse response, bool isHead)
    {
        bool hasBody = !isHead && response.Body.Length > 0;
        List<HpackHeaderField> fields = [new(":status", response.Status.ToString(CultureInfo.InvariantCulture))];
        fields.AddRange(response.Fields.Select(field => new HpackHeaderField(field.Key, field.Value)));
        await stream.WriteHeadersAsync(fields, endStream: !hasBody);
        if (hasBody)
        {
            await stream.WriteDataAsync(response.Body, endStream: true);
        }
    }
}
