using Surl.McpServing;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// Who the extended MCP server says it is in every <c>initialize</c> result (ADR-0111 decision 1): its
/// <c>serverInfo</c> and its <c>instructions</c>.
/// </summary>
public static class McpServerIdentity
{
    /// <summary>The <c>serverInfo</c> name.</summary>
    public const string Name = "surl";

    /// <summary>The <c>serverInfo</c> title, written when <c>2025-06-18</c> is negotiated.</summary>
    public const string Title = "Surl MCP server";

    /// <summary>The <c>instructions</c> the <c>initialize</c> result carries.</summary>
    public const string Instructions =
        "This server lists, reads and, when uploads are allowed, writes the files Surl serves. "
        + "Paths are '/'-separated and relative to the served directory.";

    /// <summary>Creates the <c>serverInfo</c> for a given <c>surl</c> version.</summary>
    /// <param name="surlVersion">surl's informational version, given by the host.</param>
    /// <returns>The <c>serverInfo</c>: <see cref="Name" />, <see cref="Title" /> and <paramref name="surlVersion" />.</returns>
    /// <exception cref="ArgumentException"><paramref name="surlVersion" /> is null, empty or white space.</exception>
    public static McpServerInfo CreateServerInfo(string surlVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surlVersion);
        return new McpServerInfo(Name, Title, surlVersion);
    }
}
