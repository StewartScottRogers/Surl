using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>read_retained_message</c> tool (ADR-0164 decision 6): reads a topic's retained message, through
/// <c>Surl.Api</c>'s <c>SurlServer.Messages.ReadRetainedAsync</c>. Result <c>{"topic", "qos", "text" or
/// "base64"}</c>, or <c>{"retained": false}</c> when the topic has none, which is not an error. It drops nothing
/// and publishes no event.
/// </summary>
public static class ReadRetainedMessageTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "read_retained_message";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Read a topic's retained message on a running Surl server's message bus: its QoS and payload (text when "
        + "UTF-8 with no control character other than tab, CR and LF, else base64), or {retained: false} when it has "
        + "none. Does not drop it.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "topic": { "type": "string", "minLength": 1, "description": "The topic, e.g. sensors/1" }
          },
          "required": ["server_id", "topic"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, reading from servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Read a topic retained message", Description, InputSchema, ReadAsync);

    private static async Task<McpToolResult> ReadAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        var retained = await running.Server.Messages.ReadRetainedAsync(arguments.GetProperty("topic").GetString()!, cancellationToken);
        return ToolResults.FromObject(writer =>
        {
            if (retained is null)
            {
                writer.WriteBoolean("retained", false);
                return;
            }

            writer.WriteString("topic", retained.Topic);
            writer.WriteNumber("qos", retained.QualityOfService);
            ReadBackJson.WriteBytes(writer, retained.Payload);
        });
    }
}
