using Surl.Http2;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// The server for <c>grpc</c> and <c>grpcs</c> (ADR-0116 decision 0): gRPC over one HTTP/2 connection
/// whose first bytes are the HTTP/2 preface, serving <see cref="EchoService"/> and server reflection.
/// </summary>
/// <remarks>
/// <c>grpc</c> is HTTP/2 with prior knowledge and <c>grpcs</c> is the same inside TLS from the first byte
/// (the serving engine wraps it; ALPN offers <c>h2</c> only). Bytes that are not the preface are answered
/// with <c>GOAWAY</c> <c>PROTOCOL_ERROR</c> (RFC 9113 section 3.4) and the connection is closed.
/// </remarks>
/// <param name="authenticationPolicy">Judges each call's <c>authorization</c> through one session per connection.</param>
public sealed class GrpcProtocolServer(IAuthenticationPolicy authenticationPolicy) : IConnectionProtocolServer
{
    /// <summary>The scheme served in plain text.</summary>
    public const string PlainScheme = "grpc";

    /// <summary>The scheme served inside TLS from the first byte.</summary>
    public const string TlsScheme = "grpcs";

    // RFC 9113 section 6.8: GOAWAY, stream 0, last stream 0, PROTOCOL_ERROR (1), no debug data.
    private static readonly byte[] GoAwayProtocolError = [0, 0, 8, 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1];

    private static readonly GrpcService[] ServedServices =
    [
        EchoService.Service,
        .. new ServerReflectionService([EchoService.File]).Services,
    ];

    /// <inheritdoc />
    public IReadOnlyList<string> Schemes { get; } = [PlainScheme, TlsScheme];

    /// <inheritdoc />
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var preface = new Http2ConnectionPreface();
        await preface.ReadAsync(connection, context.CancellationToken);
        if (!preface.IsComplete)
        {
            await connection.WriteAsync(GoAwayProtocolError, context.CancellationToken);
            await connection.CompleteWritesAsync(context.CancellationToken);

            return;
        }

        var session = authenticationPolicy.StartHttpConnection(connection.TlsSession, LoginExchange.Of(context));
        var handler = new GrpcStreamHandler(ServedServices, session, context);
        await new Http2ConnectionServer(handler, context, new AlreadyReadBytesConnection(connection, preface.BytesReadAfterPreface)).ServeAsync();
    }
}
