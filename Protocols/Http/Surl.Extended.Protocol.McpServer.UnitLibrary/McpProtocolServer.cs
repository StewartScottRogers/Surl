using System.Security.Cryptography;
using Surl.Content;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// The MCP server on the Streamable HTTP transport (ADR-0111 decision 4): one endpoint answering <c>POST</c>
/// with JSON, or with a one-event <c>text/event-stream</c> for <c>tools/call</c>; <c>GET</c> and other methods
/// <c>405</c>; <c>DELETE</c> ending a session. It issues and checks <c>Mcp-Session-Id</c>, checks
/// <c>MCP-Protocol-Version</c> and <c>Origin</c>, requires a Bearer token unless the policy lets anonymous
/// requests in (decision 6.1), and serves its RFC 9728 protected resource metadata (decision 6.2). It is judged
/// by the MCP specification, revision 2025-06-18, not by upstream curl, which has no MCP scheme (ADR-0083).
/// </summary>
/// <remarks>
/// HTTP/1.1 framing, the head limit and timeouts are <see cref="Http1ConnectionServer" />'s. <c>mcps</c> is
/// the same server behind the host's implicit TLS (decision 4.9); the host composes it (BL-468).
/// </remarks>
public sealed class McpProtocolServer : IConnectionProtocolServer
{
    /// <summary>The plain HTTP/1.1 scheme.</summary>
    public const string PlainScheme = "mcp";

    /// <summary>The scheme over implicit TLS.</summary>
    public const string SecureScheme = "mcps";

    /// <summary>The endpoint's path when the listen URL names no path, or only <c>/</c> (decision 4.1).</summary>
    public const string DefaultEndpointPath = "/mcp";

    private readonly McpHttpServerSettings _settings;

    private readonly IAuthenticationPolicy _authenticationPolicy;

    /// <summary>Initializes a new instance of the <see cref="McpProtocolServer" /> class.</summary>
    /// <param name="store">The shared content store the tools serve.</param>
    /// <param name="authenticationPolicy">The policy whose HTTP session judges each request's login.</param>
    /// <param name="surlVersion">surl's informational version, for <c>serverInfo</c>.</param>
    /// <param name="timeProvider">The clock sessions expire on.</param>
    /// <param name="sessionIdSource">Where session IDs come from: <see cref="RandomNumberGenerator.Create()" /> in production.</param>
    /// <param name="listenPath">The listen URL's path; <see langword="null" />, empty or <c>/</c> is <see cref="DefaultEndpointPath" />.</param>
    /// <exception cref="ArgumentNullException">A reference argument is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="surlVersion" /> is empty, or <paramref name="listenPath" /> does not start with <c>/</c>.</exception>
    public McpProtocolServer(
        ContentStore store,
        IAuthenticationPolicy authenticationPolicy,
        string surlVersion,
        TimeProvider timeProvider,
        RandomNumberGenerator sessionIdSource,
        string? listenPath = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);
        ArgumentException.ThrowIfNullOrWhiteSpace(surlVersion);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(sessionIdSource);
        _authenticationPolicy = authenticationPolicy;
        _settings = new McpHttpServerSettings(store, surlVersion, EndpointPathFor(listenPath), new McpHttpSessionTable(sessionIdSource, timeProvider));
    }

    /// <summary>Gets the endpoint's path.</summary>
    public string EndpointPath => _settings.EndpointPath;

    /// <inheritdoc />
    public IReadOnlyList<string> Schemes { get; } = [PlainScheme, SecureScheme];

    /// <inheritdoc />
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);
        IHttpAuthenticationSession authentication = _authenticationPolicy.StartHttpConnection(connection.TlsSession);
        McpHttpRequestAnswerer answerer = new(_settings, authentication, context, connection.TlsSession is not null);
        return new Http1ConnectionServer(answerer, context).ServeAsync(connection);
    }

    private static string EndpointPathFor(string? listenPath)
    {
        if (string.IsNullOrEmpty(listenPath) || listenPath == "/")
        {
            return DefaultEndpointPath;
        }

        return listenPath.StartsWith('/')
            ? listenPath
            : throw new ArgumentException("The listen path must start with '/'.", nameof(listenPath));
    }
}
