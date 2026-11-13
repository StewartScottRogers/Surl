using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Claims the <c>mcp+stdio</c> scheme so the command line accepts its listen URL (ADR-0111 decision 5). The session
/// is not served through the engine: <see cref="McpStdioComposition" /> runs it on the host's standard streams, so
/// no connection ever reaches this server.
/// </summary>
internal sealed class McpStdioListenUrlServer : IConnectionProtocolServer
{
    /// <inheritdoc />
    public IReadOnlyList<string> Schemes { get; } = ["mcp+stdio"];

    /// <inheritdoc />
    public Task ServeAsync(IConnection connection, ExchangeContext context) =>
        throw new InvalidOperationException("mcp+stdio is served on standard input and output, not on a connection.");
}
