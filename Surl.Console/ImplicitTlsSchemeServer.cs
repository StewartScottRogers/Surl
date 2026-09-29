using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// Registers a connection protocol server for one implicit-TLS scheme, such as the HTTP server
/// for <c>https</c>: the same server, serving a connection the serving engine has already
/// secured (ADR-0002, "Consequences"; ADR-0010, section 2). It answers every exchange by
/// handing it to that server unchanged, and adds no second server (ADR-0019).
/// </summary>
internal sealed class ImplicitTlsSchemeServer : IConnectionProtocolServer
{
    private readonly IConnectionProtocolServer server;

    /// <summary>
    /// Registers <paramref name="server"/> for <paramref name="implicitTlsScheme"/>.
    /// </summary>
    /// <param name="server">The server that answers the scheme's plaintext twin.</param>
    /// <param name="implicitTlsScheme">A scheme <see cref="TlsSchemes.IsImplicitTls"/> names, such as <c>https</c>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="implicitTlsScheme"/> is not TLS from the first byte.</exception>
    public ImplicitTlsSchemeServer(IConnectionProtocolServer server, string implicitTlsScheme)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (!TlsSchemes.IsImplicitTls(implicitTlsScheme))
        {
            throw new ArgumentException($"'{implicitTlsScheme}' is not TLS from the first byte.", nameof(implicitTlsScheme));
        }

        this.server = server;
        Schemes = Array.AsReadOnly([implicitTlsScheme]);
    }

    /// <summary>
    /// The one implicit-TLS scheme answered.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; }

    /// <summary>
    /// Hands the secured connection to the server this one registers.
    /// </summary>
    /// <param name="connection">The accepted connection, its TLS handshake already completed.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>The server's own task.</returns>
    public Task ServeAsync(IConnection connection, ExchangeContext context) => server.ServeAsync(connection, context);
}
