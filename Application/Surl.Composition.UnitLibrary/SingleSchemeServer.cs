using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Registers a connection protocol server that answers several schemes for one of them alone,
/// such as an SSH server for <c>scp</c>: so each scheme can be answered by its own instance,
/// composed with the view of the accounts allowed on that scheme (ADR-0102, decision 4.3). It
/// answers every exchange by handing it to that server unchanged.
/// </summary>
public sealed class SingleSchemeServer : IConnectionProtocolServer
{
    private readonly IConnectionProtocolServer server;

    /// <summary>
    /// Registers <paramref name="server"/> for <paramref name="scheme"/> alone.
    /// </summary>
    /// <param name="server">The server, which answers <paramref name="scheme"/> among others.</param>
    /// <param name="scheme">One of the server's schemes.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="scheme"/> is not one of the server's schemes.</exception>
    public SingleSchemeServer(IConnectionProtocolServer server, string scheme)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(scheme);

        if (!server.Schemes.Contains(scheme, StringComparer.Ordinal))
        {
            throw new ArgumentException($"'{scheme}' is not a scheme of the server.", nameof(scheme));
        }

        this.server = server;
        Schemes = Array.AsReadOnly([scheme]);
    }

    /// <summary>
    /// The one scheme answered.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; }

    /// <summary>
    /// Hands the connection to the server this one registers.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>The server's own task.</returns>
    public Task ServeAsync(IConnection connection, ExchangeContext context) => server.ServeAsync(connection, context);
}
