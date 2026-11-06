using System.Text.Json;
using Surl.Api;
using Surl.McpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Mcp;

/// <summary>
/// The <c>get_events</c> tool (ADR-0124 decision 8): the events a running server kept - exchanges starting and
/// ending, logins judged, store changes, faults applied and hooks failing - oldest first, found with
/// <see cref="SurlServer.FindEvents"/> and filtered by kind, scheme, listen URL, account and sequence. An ID that
/// names no running server, or a <c>listen_url</c> that is not an absolute URL, is an <c>isError</c> result.
/// </summary>
public static class GetEventsTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "get_events";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Show the events a Surl server kept, oldest first: exchanges started and ended, logins judged, store changes, "
        + "faults applied and hooks failed, filtered by kind, scheme, listen URL, account and sequence.";

    /// <summary>How many events one call returns when <c>max_events</c> is not given.</summary>
    internal const int DefaultMaxEvents = 100;

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
        """ + EventToolFilter.EventProperties + """
        ,
            "max_events": { "type": "integer", "minimum": 1, "maximum": 500, "default": 100,
              "description": "The most events to return, 100 if not given" }
          },
          "required": ["server_id"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, reading events from <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers)
    {
        ArgumentNullException.ThrowIfNull(servers);
        return new(Name, "Show a Surl server's events", Description, InputSchema,
            (arguments, _) => Task.FromResult(GetEvents(servers, arguments)));
    }

    /// <summary>The result for <paramref name="found"/>, the events the query matched, oldest first.</summary>
    /// <param name="found">The events <see cref="SurlServer.FindEvents"/> found.</param>
    /// <param name="filter">The call's filters, for its <c>kinds</c>.</param>
    /// <param name="droppedEventCount">How many events the server dropped, oldest first, past its bound.</param>
    /// <param name="arguments">The call's arguments, for <c>max_events</c>.</param>
    /// <returns>The result.</returns>
    internal static McpToolResult Answer(
        IReadOnlyList<SurlEvent> found, EventToolFilter filter, long droppedEventCount, JsonElement arguments)
    {
        var maxEvents = arguments.TryGetProperty("max_events", out var max) ? max.GetInt32() : DefaultMaxEvents;
        var matching = found.Where(filter.IsOfKind).Take(maxEvents + 1).ToList();
        return ToolResults.FromObject(writer =>
        {
            writer.WriteStartArray("events");
            foreach (var surlEvent in matching.Take(maxEvents))
            {
                RecordedEventWriter.WriteEvent(writer, surlEvent);
            }

            writer.WriteEndArray();
            writer.WriteBoolean("has_more", matching.Count > maxEvents);
            writer.WriteNumber("dropped_event_count", droppedEventCount);
        });
    }

    private static McpToolResult GetEvents(RunningSurlServers servers, JsonElement arguments)
    {
        var serverId = arguments.GetProperty("server_id").GetString()!;
        var running = servers.List().FirstOrDefault(server => server.ServerId == serverId);
        if (running is null)
        {
            return McpToolResult.FromFailure(StopServerTool.NoRunningServer(serverId));
        }

        return EventToolFilter.TryRead(arguments, out var filter) is { } refusal
            ? McpToolResult.FromFailure(refusal)
            : Answer(running.Server.FindEvents(filter!.Query), filter, running.Server.DroppedEventCount, arguments);
    }
}
