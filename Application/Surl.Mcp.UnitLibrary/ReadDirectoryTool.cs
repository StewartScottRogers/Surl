using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>read_directory</c> tool (ADR-0164 decision 9): reads a running server's LDAP directory back as LDIF,
/// <c>userPassword</c> values omitted, through <c>Surl.Api</c>'s <c>SurlServer.Directory.ExportLdifAsync</c>. Result
/// <c>{"ldif"}</c>. It publishes no event; a server with no LDAP directory is an <c>isError</c> result.
/// </summary>
public static class ReadDirectoryTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "read_directory";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Read a running Surl server's LDAP directory back as LDIF content records (RFC 2849), seeded entries and "
        + "configured ones alike, with every userPassword value omitted. At most 16 MiB. Reading changes nothing.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" }
          },
          "required": ["server_id"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, reading from servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Read the LDAP directory of a Surl server as LDIF", Description, InputSchema, ReadAsync);

    private static async Task<McpToolResult> ReadAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        var ldif = await running.Server.Directory.ExportLdifAsync(cancellationToken);
        return ToolResults.FromObject(writer => writer.WriteString("ldif", ldif));
    }
}
