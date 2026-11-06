using System.Text.Json;
using Surl.Api;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>get_exchanges</c> tool (ADR-0103 decision 3, as ADR-0124 decision 8 amends it): what each client sent to a
/// running server and what the server sent back, oldest first, found with <see cref="SurlServer.FindExchanges"/> by
/// <c>scheme</c> and <c>listen_url</c>. Each exchange shows its listen URL and scheme, and its chunks in the order
/// they crossed the connection, as text or base64, cut to <see cref="RecordedExchangeWriter.MaxShownBytesPerExchange"/>
/// bytes of each exchange and <see cref="RecordedExchangeWriter.MaxShownBytesPerResult"/> in all. An ID that names no
/// running server, or a <c>listen_url</c> that is not an absolute URL, is an <c>isError</c> result.
/// </summary>
public static class GetExchangesTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "get_exchanges";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Show what each client sent to a Surl server and what the server sent back, byte for byte, oldest first, "
        + "with the listen URL and scheme each exchange came through, filtered by scheme and listen URL.";

    /// <summary>How many exchanges one call returns when <c>max_exchanges</c> is not given.</summary>
    internal const int DefaultMaxExchanges = 20;

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "after_exchange_id": { "type": "integer", "minimum": 0,
              "description": "Only exchanges with a larger exchange_id; pass the last one seen to ask for the rest" },
            "max_exchanges": { "type": "integer", "minimum": 1, "maximum": 100, "default": 20,
              "description": "The most exchanges to return, 20 if not given" },
        """ + EventToolFilter.SchemeAndListenUrlProperties + """

          },
          "required": ["server_id"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, reading exchanges from <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers)
    {
        ArgumentNullException.ThrowIfNull(servers);
        return new(Name, "Show a Surl server's exchanges", Description, InputSchema,
            (arguments, _) => Task.FromResult(GetExchanges(servers, arguments)));
    }

    /// <summary>The result for <paramref name="records"/>, the exchanges server <paramref name="serverId"/> has recorded, oldest first.</summary>
    /// <param name="serverId">The server's ID.</param>
    /// <param name="records">Its recorded exchanges, oldest first.</param>
    /// <param name="droppedExchangeCount">How many it dropped, oldest first, past its record's bound.</param>
    /// <param name="arguments">The call's arguments, checked against <see cref="InputSchema"/>.</param>
    /// <returns>The result.</returns>
    internal static McpToolResult Answer(
        string serverId, IReadOnlyList<SurlExchangeRecord> records, long droppedExchangeCount, JsonElement arguments)
    {
        var afterExchangeId = arguments.TryGetProperty("after_exchange_id", out var after) ? after.GetInt64() : 0;
        var maxExchanges = arguments.TryGetProperty("max_exchanges", out var max) ? max.GetInt32() : DefaultMaxExchanges;
        return ToolResults.FromObject(writer =>
        {
            writer.WriteString("server_id", serverId);
            var hasMore = RecordedExchangeWriter.WriteExchanges(
                writer, records.Where(record => record.ExchangeId > afterExchangeId), maxExchanges);
            writer.WriteNumber("dropped_exchange_count", droppedExchangeCount);
            writer.WriteBoolean("has_more", hasMore);
        });
    }

    private static McpToolResult GetExchanges(RunningSurlServers servers, JsonElement arguments)
    {
        var serverId = arguments.GetProperty("server_id").GetString()!;
        var running = servers.List().FirstOrDefault(server => server.ServerId == serverId);
        if (running is null)
        {
            return McpToolResult.FromFailure(StopServerTool.NoRunningServer(serverId));
        }

        return EventToolFilter.TryRead(arguments, out var filter) is { } refusal
            ? McpToolResult.FromFailure(refusal)
            : Answer(serverId, running.Server.FindExchanges(filter!.Query), running.Server.DroppedExchangeCount, arguments);
    }
}
