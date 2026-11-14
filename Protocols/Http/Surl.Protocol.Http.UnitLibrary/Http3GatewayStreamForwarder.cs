using System.Globalization;
using Surl.Http3;
using Surl.Qpack;

namespace Surl.Protocol.Http;

/// <summary>
/// Forwards each HTTP/3 request stream's request to the case's <c>http</c> half as HTTP/1.1 and
/// answers the stream with the reply as nghttpx builds it (ADR-0154 decisions 2 and 3).
/// </summary>
/// <param name="backend">The case's <c>http</c> half.</param>
internal sealed class Http3GatewayStreamForwarder(NghttpxGatewayBackend backend) : IHttp3RequestStreamHandler
{
    /// <inheritdoc/>
    public async Task ServeRequestStreamAsync(Http3RequestStream stream, CancellationToken cancellationToken)
    {
        Http3RequestHead head = stream.Head;
        byte[] body = await ReadRequestBodyAsync(stream, cancellationToken);
        byte[] request = NghttpxGatewayRequestTranslator.Translate(
            NghttpxGatewayVersion.Http3, head.Method, head.Scheme, head.Authority, head.Target, head.Fields, body);
        bool isHead = head.Method == "HEAD";

        byte[] reply = await backend.ReplyToAsync(request, isHead, cancellationToken);
        if (NghttpxGatewayReplyFramingReader.Read(reply, isHead) == NghttpxGatewayReplyFraming.Short)
        {
            await stream.ResetAsync(Http3ErrorCode.InternalError, "the backend closed in the middle of the reply body", cancellationToken);

            return;
        }

        NghttpxGatewayResponse response = NghttpxGatewayResponseTranslator.Translate(NghttpxGatewayVersion.Http3, reply) ?? BadGatewayWithoutServer();
        bool hasBody = !isHead && response.Body.Length > 0;
        List<QpackFieldLine> fields = [new(":status", response.Status.ToString(CultureInfo.InvariantCulture))];
        fields.AddRange(response.Fields.Select(field => new QpackFieldLine(field.Key, field.Value)));
        await stream.WriteHeadersAsync(fields, endStream: !hasBody, cancellationToken);
        if (hasBody)
        {
            await stream.WriteDataAsync(response.Body, endStream: true, cancellationToken);
        }
    }

    // HTTP/3 never gains server: nghttpx (ADR-0154 decision 3), so neither does its 502.
    private static NghttpxGatewayResponse BadGatewayWithoutServer()
    {
        NghttpxGatewayResponse badGateway = NghttpxGatewayBackend.BadGateway();

        return badGateway with { Fields = [.. badGateway.Fields.Where(field => field.Key != "server")] };
    }

    private static async Task<byte[]> ReadRequestBodyAsync(Http3RequestStream stream, CancellationToken cancellationToken)
    {
        using MemoryStream body = new();

        // A request whose HEADERS ended the stream has no body to wait for.
        while (stream.IsRequestBodyAnnounced && await stream.ReadBodyAsync(cancellationToken) is { } piece)
        {
            body.Write(piece);
        }

        return body.ToArray();
    }
}
