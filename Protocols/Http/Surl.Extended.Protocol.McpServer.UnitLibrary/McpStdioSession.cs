using Surl.Content;
using Surl.McpServing;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// Serves the extended MCP server's one <c>mcp+stdio:</c> session over a pair of byte streams (ADR-0111
/// decision 5, the library half): the shared core's <see cref="McpStreamSession" /> frames the messages as
/// newline-delimited lines, and this server gives it its identity and its tools, run as the anonymous caller
/// because the client that started the process is trusted. The session ends when the input ends. Never touches
/// <see cref="Console" />: the executable binds the streams to its own standard input and output.
/// </summary>
public static class McpStdioSession
{
    /// <summary>The scheme the session's tool calls are made under, as the content store sees them.</summary>
    public const string Scheme = "mcp+stdio";

    /// <summary>
    /// Serves one session until <paramref name="input" /> ends or <paramref name="cancellationToken" /> is
    /// cancelled; running tool calls are cancelled and awaited first.
    /// </summary>
    /// <param name="input">The stream the client writes its messages to: standard input.</param>
    /// <param name="output">The stream only MCP messages are written to: standard output.</param>
    /// <param name="store">The shared content store the tools read and write.</param>
    /// <param name="surlVersion">surl's informational version, given by the host.</param>
    /// <param name="maximumMessageBytes">The longest message accepted, in bytes: <c>--max-message</c>.</param>
    /// <param name="cancellationToken">Ends the session.</param>
    /// <returns>A task that completes when the session has ended.</returns>
    public static Task RunAsync(
        Stream input, Stream output, ContentStore store, string surlVersion, int maximumMessageBytes, CancellationToken cancellationToken)
    {
        McpServerRegistry registry = McpServerTools.CreateRegistry(store, ContentCaller.Anonymous(Scheme), maximumMessageBytes);
        McpSessionOptions options = McpServerSession.CreateOptions(surlVersion, maximumMessageBytes);
        return McpStreamSession.RunAsync(input, output, registry, options, cancellationToken);
    }
}
