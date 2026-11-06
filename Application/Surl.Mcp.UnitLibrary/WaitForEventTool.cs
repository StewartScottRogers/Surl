using System.Text.Json;
using Surl.Api;
using Surl.McpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Mcp;

/// <summary>
/// The <c>wait_for_event</c> tool (ADR-0124 decision 8): the first event a running server kept that matches the
/// same filters <c>get_events</c> takes, waited for with <see cref="SurlServer.WaitForEventAsync"/> for at most
/// <c>timeout_ms</c> on the host's clock. An event already kept is returned at once. Running out of time is not
/// an error: the result is <c>{"timed_out": true}</c>. An ID that names no running server, a <c>timeout_ms</c>
/// outside 1 to 60000, or a <c>listen_url</c> that is not an absolute URL is an <c>isError</c> result.
/// </summary>
public static class WaitForEventTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "wait_for_event";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Wait, at most timeout_ms milliseconds, for the first event of a Surl server that matches the filters "
        + "get_events takes; an event already kept is returned at once, and running out of time returns timed_out.";

    /// <summary>The shortest wait, in milliseconds.</summary>
    internal const int MinTimeoutMilliseconds = 1;

    /// <summary>The longest wait, in milliseconds.</summary>
    internal const int MaxTimeoutMilliseconds = 60000;

    /// <summary>The wait when <c>timeout_ms</c> is not given, in milliseconds.</summary>
    internal const int DefaultTimeoutMilliseconds = 10000;

    /// <summary>
    /// The tool's input schema. <c>timeout_ms</c>'s range is checked by the tool rather than the schema, so a wait
    /// out of range is an <c>isError</c> result the agent reads, not a protocol error.
    /// </summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
        """ + EventToolFilter.EventProperties + """
        ,
            "timeout_ms": { "type": "integer", "default": 10000,
              "description": "How long to wait, 1 to 60000 milliseconds, 10000 if not given" }
          },
          "required": ["server_id"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, waiting on <paramref name="servers"/> with their clock.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers)
    {
        ArgumentNullException.ThrowIfNull(servers);
        return new(Name, "Wait for a Surl server's event", Description, InputSchema,
            (arguments, cancellationToken) => WaitAsync(servers, arguments, cancellationToken));
    }

    /// <summary>The refusal of a <c>timeout_ms</c> outside the range.</summary>
    /// <param name="timeoutMilliseconds">The value given.</param>
    /// <returns>The refusal's text.</returns>
    internal static string TimeoutOutOfRange(long timeoutMilliseconds) =>
        $"timeout_ms must be {MinTimeoutMilliseconds} to {MaxTimeoutMilliseconds}, not {timeoutMilliseconds}";

    private static async Task<McpToolResult> WaitAsync(RunningSurlServers servers, JsonElement arguments, CancellationToken cancellationToken)
    {
        var serverId = arguments.GetProperty("server_id").GetString()!;
        var running = servers.List().FirstOrDefault(server => server.ServerId == serverId);
        if (running is null)
        {
            return McpToolResult.FromFailure(StopServerTool.NoRunningServer(serverId));
        }

        if (TryReadWait(arguments, out var filter, out var timeout) is { } refusal)
        {
            return McpToolResult.FromFailure(refusal);
        }

        var found = await WaitForMatchAsync(running.Server, filter!, servers.TimeProvider, timeout, cancellationToken);
        return ToolResults.FromObject(writer => WriteFound(writer, found));
    }

    // Returns why the arguments are refused, or null with the filter and the wait.
    private static string? TryReadWait(JsonElement arguments, out EventToolFilter? filter, out TimeSpan timeout)
    {
        filter = null;
        var timeoutMilliseconds = arguments.TryGetProperty("timeout_ms", out var given) ? given.GetInt64() : DefaultTimeoutMilliseconds;
        var isInRange = timeoutMilliseconds is >= MinTimeoutMilliseconds and <= MaxTimeoutMilliseconds;
        timeout = isInRange ? TimeSpan.FromMilliseconds(timeoutMilliseconds) : TimeSpan.Zero;
        return isInRange ? EventToolFilter.TryRead(arguments, out filter) : TimeoutOutOfRange(timeoutMilliseconds);
    }

    private static void WriteFound(Utf8JsonWriter writer, SurlEvent? found)
    {
        if (found is null)
        {
            writer.WriteBoolean("timed_out", true);
            return;
        }

        writer.WritePropertyName("event");
        RecordedEventWriter.WriteEvent(writer, found);
    }

    // SurlServer waits for one query, and a query names at most one kind, so an event the query matches but whose
    // kind kinds leaves out moves the query past it and the wait goes on. One deadline on the host's clock bounds
    // every wait of the loop together.
    private static async Task<SurlEvent?> WaitForMatchAsync(
        SurlServer server, EventToolFilter filter, TimeProvider timeProvider, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(timeout, timeProvider);
        using var deadlineOrCancelled = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var query = filter.Query;
        try
        {
            while (true)
            {
                var surlEvent = await server.WaitForEventAsync(query, Timeout.InfiniteTimeSpan, deadlineOrCancelled.Token);
                if (filter.IsOfKind(surlEvent))
                {
                    return surlEvent;
                }

                query = query with { AfterSequence = surlEvent.Sequence };
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
