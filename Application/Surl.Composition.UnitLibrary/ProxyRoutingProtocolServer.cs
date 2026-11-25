using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Serves a listen URL named by <c>--proxy</c> with the HTTP proxy server and every other listen
/// URL of the same scheme with the server that answers it (ADR-0088 decision 7): the serving engine
/// picks a server by scheme, and <c>http</c> and <c>https</c> name both the origin servers and the
/// proxy, so this one picks by <see cref="ListenUrl.IsProxy"/> per exchange.
/// </summary>
/// <remarks>
/// A connection refused over a connection limit is answered by the origin server's writer when it
/// has one: the refusal is written before any request is read, so the listen URL is not known there.
/// </remarks>
public sealed class ProxyRoutingProtocolServer : IConnectionProtocolServer, IConnectionRefusalWriter, IExchangeBytesRedactingServer
{
    private readonly IConnectionProtocolServer originServer;
    private readonly IConnectionProtocolServer proxyServer;

    /// <summary>Routes between the two servers.</summary>
    /// <param name="originServer">The server that answers a listen URL not named by <c>--proxy</c>, such as the HTTP server.</param>
    /// <param name="proxyServer">The server that answers a listen URL named by <c>--proxy</c>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ProxyRoutingProtocolServer(IConnectionProtocolServer originServer, IConnectionProtocolServer proxyServer)
    {
        ArgumentNullException.ThrowIfNull(originServer);
        ArgumentNullException.ThrowIfNull(proxyServer);

        this.originServer = originServer;
        this.proxyServer = proxyServer;
    }

    /// <summary>The schemes the origin server answers.</summary>
    public IReadOnlyList<string> Schemes => originServer.Schemes;

    /// <summary>Hands the exchange to the proxy server on a <c>--proxy</c> listen URL, else to the origin server.</summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>The chosen server's own task.</returns>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return (context.ListenUrl.IsProxy ? proxyServer : originServer).ServeAsync(connection, context);
    }

    /// <summary>Refuses a connection over a limit as the origin server does; nothing is written when it has no refusal.</summary>
    /// <param name="connection">The refused connection.</param>
    /// <param name="refusal">Which limit was reached.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the refusal is written.</returns>
    public ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken) =>
        originServer is IConnectionRefusalWriter writer
            ? writer.WriteRefusalAsync(connection, refusal, cancellationToken)
            : ValueTask.CompletedTask;

    /// <summary>
    /// Hands the engine the redactor of a server behind this one (ADR-0182, amendment 8): the proxy
    /// server's when it redacts, else the origin server's, else one that passes every byte through.
    /// The redaction context does not say whether the listen URL is a proxy's, so with <c>--proxy</c>
    /// named the origin <c>http</c> and <c>https</c> listen URLs use the proxy's redactor too. For a
    /// conforming client - one that sends the HTTP/2 connection preface outside a body only to start
    /// HTTP/2, and does send it after <c>101 Switching Protocols</c> for <c>h2c</c> - the proxy's
    /// redactor withholds every credential the HTTP origin server's withholds. It also withholds
    /// every later byte when that preface appears where no request may start, an upload body
    /// included.
    /// </summary>
    /// <param name="context">The exchange the redactor is for.</param>
    /// <returns>The redactor for the exchange.</returns>
    public IReceivedBytesRedactor CreateRedactor(ExchangeRedactionContext context) =>
        proxyServer is IExchangeBytesRedactingServer proxy ? proxy.CreateRedactor(context)
        : originServer is IExchangeBytesRedactingServer origin ? origin.CreateRedactor(context)
        : new PassThroughRedactor();
}
