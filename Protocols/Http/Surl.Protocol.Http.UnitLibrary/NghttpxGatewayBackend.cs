using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// The connection from the <c>http/2</c> gateway half (ADR-0154) to the case's <c>http</c> half: one
/// fresh connection from the injected <see cref="IOriginConnector"/> per request, closed after the reply.
/// </summary>
/// <param name="connector">Opens each connection; loopback-only in a conformance run.</param>
/// <param name="port">The port of the case's <c>http</c> half on <c>127.0.0.1</c>.</param>
internal sealed class NghttpxGatewayBackend(IOriginConnector connector, int port)
{
    private const string BackendHost = "127.0.0.1";

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Writes <paramref name="request"/> to the backend and reads until the reply is whole or the backend closes.</summary>
    /// <param name="request">The forwarded HTTP/1.1 request.</param>
    /// <param name="isHead">Whether the request is a <c>HEAD</c>, whose reply has no body.</param>
    /// <param name="cancellationToken">Cancelled when the exchange ends.</param>
    /// <returns>Every byte the backend sent; empty when it could not be reached.</returns>
    public async Task<byte[]> ReplyToAsync(byte[] request, bool isHead, CancellationToken cancellationToken)
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

    /// <summary>The <c>502 Bad Gateway</c> nghttpx answers when the backend sends no complete reply head.</summary>
    /// <returns>The response.</returns>
    public static NghttpxGatewayResponse BadGateway() => new(
        502,
        [
            new("content-type", "text/html; charset=UTF-8"),
            new("server", "nghttpx"),
            new("via", "1.1 nghttpx"),
        ],
        "<html><head><title>502 Bad Gateway</title></head><body><h1>502 Bad Gateway</h1></body></html>"u8.ToArray());
}
