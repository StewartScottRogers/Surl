using Surl.McpServing;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// One session of the extended MCP server, independent of any transport: it gives the shared core
/// <c>Surl.McpServing</c> this server's identity (<see cref="McpServerIdentity" />) and registry, and the core
/// answers the lifecycle - <c>initialize</c> with version negotiation, <c>notifications/initialized</c>,
/// <c>ping</c>, refusal of requests before <c>initialize</c> and the JSON-RPC errors (ADR-0111 decisions 1, 3
/// and 7). Streamable HTTP feeds it one message at a time; stdio runs the core's stream session over the same
/// registry and options.
/// </summary>
public sealed class McpServerSession
{
    private readonly McpRequestDispatcher _dispatcher;

    /// <summary>Initializes a new instance of the <see cref="McpServerSession" /> class.</summary>
    /// <param name="registry">The server's one registry, shared by all its sessions.</param>
    /// <param name="options">The options <see cref="CreateOptions" /> made.</param>
    public McpServerSession(McpServerRegistry registry, McpSessionOptions options)
    {
        _dispatcher = new McpRequestDispatcher(registry, options);
    }

    /// <summary>Gets the revision <c>initialize</c> answered, or <see langword="null" /> until then.</summary>
    public string? NegotiatedProtocolVersion => _dispatcher.NegotiatedProtocolVersion;

    /// <summary>Creates the options every session of this server uses.</summary>
    /// <param name="surlVersion">surl's informational version, given by the host.</param>
    /// <param name="maximumMessageBytes">The longest message accepted, in bytes: <c>--max-message</c>.</param>
    /// <returns>The options carrying this server's <c>serverInfo</c> and <c>instructions</c>.</returns>
    public static McpSessionOptions CreateOptions(string surlVersion, int maximumMessageBytes) =>
        new(McpServerIdentity.CreateServerInfo(surlVersion), McpServerIdentity.Instructions, maximumMessageBytes);

    /// <summary>Answers one message from the client through the core.</summary>
    /// <param name="message">The message's UTF-8 bytes, without its line terminator.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The response's UTF-8 JSON, or <see langword="null" /> when the message gets no response.</returns>
    public Task<byte[]?> AnswerAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken) =>
        _dispatcher.AnswerAsync(message, cancellationToken);
}
