using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>list_mailboxes</c> tool (ADR-0164 decision 6): lists an account's mailboxes on a running server's mail
/// store, through <c>Surl.Api</c>'s <c>SurlServer.Mail.ListMailboxesAsync</c>. Result <c>{"mailboxes": [{"name",
/// "message_count", "unseen_count", "uid_validity", "use"?}]}</c> (<c>use</c>, such as <c>\Sent</c>, only for a mailbox that has one). It changes no flag and publishes no event; an absent
/// account is an <c>isError</c> result with the API's message.
/// </summary>
public static class ListMailboxesTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "list_mailboxes";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "List an account's mailboxes on a running Surl server's mail store: name, message count, unseen count and "
        + "UIDVALIDITY, and its RFC 6154 use (such as \\Sent) when it has one. Changes no flag.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "owner": { "type": "string", "description": "The account's name; the empty string names the --allow-anonymous mailbox owner" }
          },
          "required": ["server_id", "owner"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, reading from servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "List the mailboxes of a Surl server account", Description, InputSchema, ReadAsync);

    private static async Task<McpToolResult> ReadAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        var mailboxes = await running.Server.Mail.ListMailboxesAsync(arguments.GetProperty("owner").GetString()!, cancellationToken);
        return ToolResults.FromObject(writer =>
        {
            writer.WriteStartArray("mailboxes");
            foreach (var mailbox in mailboxes)
            {
                writer.WriteStartObject();
                writer.WriteString("name", mailbox.Name);
                writer.WriteNumber("message_count", mailbox.MessageCount);
                writer.WriteNumber("unseen_count", mailbox.UnseenCount);
                writer.WriteNumber("uid_validity", mailbox.UidValidity);
                if (mailbox.Use is not null)
                {
                    writer.WriteString("use", mailbox.Use);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        });
    }
}
