using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The SignalR server: one hub per listener, answering the <c>signalr</c> and <c>signalrs</c>
/// schemes the Curl port adds over HTTP/1.1 (ADR-0119; ADR-0083). It is judged by ASP.NET Core
/// SignalR's TransportProtocols.md and HubProtocol.md, not by upstream curl, which has no SignalR.
/// </summary>
/// <remarks>
/// It answers negotiate and judges every request's login through the
/// <see cref="IAuthenticationPolicy"/>'s HTTP session (BL-448); the WebSocket transport and the hub's
/// methods are BL-449's. <c>signalrs</c> is <c>signalr</c> inside the TLS the listener seam provides.
/// HTTP/1.1 framing, the head limit and timeouts are <see cref="Http1ConnectionServer"/>'s.
/// </remarks>
public sealed class SignalRProtocolServer : IConnectionProtocolServer
{
    /// <summary>
    /// The plain scheme.
    /// </summary>
    public const string PlainScheme = "signalr";

    /// <summary>
    /// The scheme served inside TLS.
    /// </summary>
    public const string TlsScheme = "signalrs";

    private readonly SignalRHubPath hubPath;
    private readonly SignalRHubConnections connections;
    private readonly IAuthenticationPolicy authenticationPolicy;

    /// <summary>
    /// Creates the server.
    /// </summary>
    /// <param name="hubPath">Where the hub is served, from the listen URL's path.</param>
    /// <param name="connections">The hub's connections, shared by every connection the server serves.</param>
    /// <param name="authenticationPolicy">The policy whose HTTP session judges each request's login.</param>
    public SignalRProtocolServer(SignalRHubPath hubPath, SignalRHubConnections connections, IAuthenticationPolicy authenticationPolicy)
    {
        ArgumentNullException.ThrowIfNull(hubPath);
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);

        this.hubPath = hubPath;
        this.connections = connections;
        this.authenticationPolicy = authenticationPolicy;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = [PlainScheme, TlsScheme];

    /// <inheritdoc/>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var authentication = authenticationPolicy.StartHttpConnection(connection.TlsSession);
        var answerer = new SignalRRequestAnswerer(hubPath, connections, authentication, context);

        return new Http1ConnectionServer(answerer, context).ServeAsync(connection);
    }
}
