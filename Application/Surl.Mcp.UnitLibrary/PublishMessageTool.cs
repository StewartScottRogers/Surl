using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>publish_message</c> tool (ADR-0124 decision 8): publishes a message on a topic, retained or not, or puts
/// one into an existing queue, on a running server's message bus, through <c>Surl.Api</c>'s
/// <c>SurlServer.Messages</c>. Result <c>{"published": true}</c>; a publish the bus refuses is an <c>isError</c> result.
/// </summary>
public static class PublishMessageTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "publish_message";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Publish a message on a topic (optionally retained for later subscribers) or put one into an existing queue "
        + "on a running Surl server's message bus, as a client would, so its MQTT and other messaging servers deliver "
        + "it. Give the payload as UTF-8 text or base64, at most 16 MiB.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "topic": { "type": "string", "minLength": 1, "description": "The topic to publish on, e.g. sensors/1; not with queue" },
            "queue": { "type": "string", "minLength": 1, "description": "The existing queue to put the message into; not with topic" },
            "payload": { "type": "string", "description": "The payload as UTF-8 text" },
            "payload_base64": { "type": "string", "description": "The payload as base64 bytes" },
            "retain": { "type": "boolean", "description": "Keep it as the topic's retained message (topic only; default false)" }
          },
          "required": ["server_id"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, publishing on servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Publish a message on a Surl server", Description, InputSchema, PublishAsync);

    private static async Task<McpToolResult> PublishAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        var topic = RunningServerTool.ReadOptionalString(arguments, "topic");
        var queue = RunningServerTool.ReadOptionalString(arguments, "queue");
        var retain = arguments.TryGetProperty("retain", out var retainValue) && retainValue.GetBoolean();
        byte[]? payload = null;
        if ((FindRefusal(topic, queue, retain) ?? SeededContent.TryRead(arguments, "payload", "payload_base64", out payload)) is { } refusal)
        {
            return McpToolResult.FromFailure(refusal);
        }

        await (topic is not null
            ? running.Server.Messages.PublishAsync(topic, payload, retain, cancellationToken)
            : running.Server.Messages.EnqueueAsync(queue!, payload, cancellationToken));
        return ToolResults.FromObject(writer => writer.WriteBoolean("published", true));
    }

    private static string? FindRefusal(string? topic, string? queue, bool retain)
    {
        if ((topic is null) == (queue is null))
        {
            return "give exactly one of topic and queue";
        }

        return retain && queue is not null ? "retain applies to a topic, not a queue" : null;
    }
}
