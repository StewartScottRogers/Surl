using System.Text;
using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>add_ldap_entries</c> tool (ADR-0158 decision 9): adds LDIF content records (RFC 2849) to a running
/// server's LDAP directory, all or none, through <c>Surl.Api</c>'s <c>SurlServer.Directory</c>. Result
/// <c>{"entry_count"}</c>; the entries are never echoed.
/// </summary>
public static class AddLdapEntriesTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "add_ldap_entries";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Add entries to a running Surl server's LDAP directory from LDIF content records, all or none, so its ldap server "
        + "answers searches with them. At most 16 MiB; a changetype record and a value read from a file or URL are refused.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "ldif": { "type": "string", "minLength": 1, "description": "The LDIF text, one record per entry, e.g. dn: dc=example,dc=com then its attributes" }
          },
          "required": ["server_id", "ldif"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, seeding the directory of servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Add LDAP entries to a Surl server", Description, InputSchema, AddAsync);

    private static async Task<McpToolResult> AddAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        var ldif = arguments.GetProperty("ldif").GetString()!;
        if (Encoding.UTF8.GetByteCount(ldif) > SeededContent.MaxBytes)
        {
            return McpToolResult.FromFailure($"ldif: more than the {SeededContent.MaxBytes} (16 MiB) one seed may carry");
        }

        await running.Server.Directory.AddEntriesAsync(ldif, cancellationToken);
        var entryCount = ldif.Split('\n').Count(line => line.StartsWith("dn:", StringComparison.OrdinalIgnoreCase));
        return ToolResults.FromObject(writer => writer.WriteNumber("entry_count", entryCount));
    }
}
