using Surl.Content;
using Surl.McpServing;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// Builds the registry of the MCP server's tools (ADR-0111 decision 2), in <c>tools/list</c> order:
/// <c>list_directory</c> (only under <c>--list-directories</c>), <c>read_file</c>, <c>write_file</c> (only under
/// <c>--allow-uploads</c>) and <c>echo</c>. One registry is built per session, because every file operation runs as
/// that session's caller.
/// </summary>
public static class McpServerTools
{
    /// <summary>Creates the registry one session serves.</summary>
    /// <param name="store">The shared content store; its exposure options decide which tools are offered.</param>
    /// <param name="caller">Who the session's calls run as: the authenticated account, or anonymous.</param>
    /// <param name="maximumMessageBytes">The message limit, <c>--max-message</c>, which bounds a read's answer.</param>
    /// <returns>The registry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="store" /> or <paramref name="caller" /> is <see langword="null" />.</exception>
    public static McpServerRegistry CreateRegistry(ContentStore store, ContentCaller caller, int maximumMessageBytes)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(caller);
        McpServerRegistry registry = new();
        if (store.ExposureOptions.ListDirectories)
        {
            registry.AddTool(McpListDirectoryTool.Create(store, caller));
        }

        registry.AddTool(McpReadFileTool.Create(store, caller, maximumMessageBytes));
        if (store.ExposureOptions.AllowUploads)
        {
            registry.AddTool(McpWriteFileTool.Create(store, caller));
        }

        registry.AddTool(McpEchoTool.Create());
        return registry;
    }
}
