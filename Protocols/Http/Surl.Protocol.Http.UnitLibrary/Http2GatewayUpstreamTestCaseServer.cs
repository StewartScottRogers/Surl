using System.Globalization;
using Surl.Hpack;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// The <c>http/2</c> server half of upstream test cases (ADR-0154): what upstream's nghttpx is in
/// front of the case's <c>http</c> half. It serves HTTP/2 on a connection the engine has already
/// secured with ALPN <c>h2</c>, or on a plaintext one with prior knowledge, forwards each stream's
/// request to the case's <c>http</c> half as HTTP/1.1 (<see cref="NghttpxGatewayRequestTranslator"/>)
/// over a fresh connection from the injected <see cref="IOriginConnector"/>, and answers the stream
/// with the reply as nghttpx builds it (<see cref="NghttpxGatewayResponseTranslator"/>).
/// </summary>
/// <remarks>
/// <para>
/// The stream ends when the reply's <c>Content-Length</c> or last chunk has arrived, or when the
/// backend closes. A backend that sends no complete reply head is answered <c>502 Bad Gateway</c>
/// (as is one that cannot be reached); one that closes in the middle of a body has the stream reset
/// with <c>INTERNAL_ERROR</c>. The connection stays open for the next stream either way.
/// </para>
/// <para>
/// Never composed by the shipped <c>surl</c> (ADR-0080 decision 2): it uses no store, publishes
/// nothing on an event stream and has no fault rules.
/// </para>
/// </remarks>
/// <param name="backendConnector">Opens each stream's connection to the backend; loopback-only in a conformance run.</param>
/// <param name="backendPort">The port of the case's <c>http</c> half on <c>127.0.0.1</c>.</param>
public sealed class Http2GatewayUpstreamTestCaseServer(IOriginConnector backendConnector, int backendPort) : IConnectionProtocolServer
{
    private const string BackendHost = "127.0.0.1";

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    private readonly IOriginConnector backendConnector = backendConnector ?? throw new ArgumentNullException(nameof(backendConnector));

    /// <summary>The one scheme answered: <c>http</c>; <c>https</c> is the same server on a connection the engine has secured.</summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["http"]);

    /// <summary>Serves HTTP/2 on <paramref name="connection"/> until the client or the exchange ends it.</summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var preface = new Http2ConnectionPreface();
        await preface.ReadAsync(connection, context.CancellationToken);
        if (!preface.IsComplete)
        {
            context.Log.Note("The client did not send the HTTP/2 connection preface; closed with no bytes.");

            return;
        }

        await new Http2ConnectionServer(
            new StreamForwarder(backendConnector, backendPort),
            context,
            new AlreadyReadBytesConnection(connection, preface.BytesReadAfterPreface)).ServeAsync();
    }

    private sealed class StreamForwarder(IOriginConnector connector, int port) : IHttp2StreamHandler
    {
        public async Task ServeStreamAsync(Http2RequestStream stream, CancellationToken cancellationToken)
        {
            Http2RequestHead head = stream.Head;
            byte[] body = await ReadRequestBodyAsync(stream, cancellationToken);
            byte[] request = NghttpxGatewayRequestTranslator.Translate(
                NghttpxGatewayVersion.Http2, head.Method, head.Scheme, head.Authority, head.Target, head.Fields, body);
            bool isHead = head.Method == "HEAD";

            byte[] reply = await ForwardAsync(request, isHead, cancellationToken);
            if (NghttpxGatewayReplyFramingReader.Read(reply, isHead) == NghttpxGatewayReplyFraming.Short)
            {
                await stream.ResetAsync(Http2ErrorCode.InternalError, "the backend closed in the middle of the reply body");

                return;
            }

            NghttpxGatewayResponse response = NghttpxGatewayResponseTranslator.Translate(NghttpxGatewayVersion.Http2, reply) ?? BadGateway();
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

        // Every byte the backend sent, read until the reply is whole or the backend closes; empty when it could not be reached.
        private async Task<byte[]> ForwardAsync(byte[] request, bool isHead, CancellationToken cancellationToken)
        {
            IConnection backend;
            try
            {
                backend = await connector.ConnectAsync(BackendHost, port, ConnectTimeout, cancellationToken);
            }
            catch (OriginConnectionException)
            {
                return [];
            }

            await using (backend)
            {
                await backend.WriteAsync(request, cancellationToken);
                using MemoryStream reply = new();
                byte[] buffer = new byte[8192];
                while (NghttpxGatewayReplyFramingReader.Read(reply.ToArray(), isHead) != NghttpxGatewayReplyFraming.Whole)
                {
                    int read = await backend.ReadAsync(buffer, cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }

                    reply.Write(buffer, 0, read);
                }

                return reply.ToArray();
            }
        }

        private static NghttpxGatewayResponse BadGateway() => new(
            502,
            [
                new("content-type", "text/html; charset=UTF-8"),
                new("server", "nghttpx"),
                new("via", "1.1 nghttpx"),
            ],
            "<html><head><title>502 Bad Gateway</title></head><body><h1>502 Bad Gateway</h1></body></html>"u8.ToArray());

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
}
