using System.Text.Json;
using Surl.Api;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// A <see cref="RunningSurlServers"/> whose servers start through <see cref="ApiTestSeams"/>, so no socket is
/// opened, with its standard error kept in <see cref="ErrorOutput"/> and its clock fixed at <see cref="Now"/>; and
/// the four tools over it.
/// </summary>
internal sealed class McpTestHost : IAsyncDisposable
{
    /// <summary>The time the host's clock always reads.</summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

    public McpTestHost(
        SurlMcpHostSettings? settings = null,
        StartServerSafetyCheck? safetyCheck = null,
        FakeListenerFactory? listenerFactory = null,
        TimeProvider? timeProvider = null)
    {
        Seams = new ApiTestSeams(listenerFactory);
        var hostSettings = settings ?? new SurlMcpHostSettings();
        Servers = new RunningSurlServers(
            hostSettings,
            ErrorOutput,
            timeProvider ?? new FixedTimeProvider(Now),
            (options, token) => SurlServer.StartAsync(options, Seams.Host, token));
        StartServer = safetyCheck is null ? StartServerTool.Create(Servers) : StartServerTool.Create(Servers, safetyCheck);
        ListServers = ListServersTool.Create(Servers);
        StopServer = StopServerTool.Create(Servers);
        GetExchanges = GetExchangesTool.Create(Servers);
        GetEvents = GetEventsTool.Create(Servers);
        WaitForEvent = WaitForEventTool.Create(Servers);
        SeedMail = SeedMailTool.Create(Servers);
        PutFile = PutFileTool.Create(Servers);
        PublishMessage = PublishMessageTool.Create(Servers);
        AddAccount = AddAccountTool.Create(Servers);
        ResetServer = ResetServerTool.Create(Servers);
        SnapshotServer = SnapshotServerTool.Create(Servers);
        RestoreServer = RestoreServerTool.Create(Servers);
        AddFault = AddFaultTool.Create(Servers);
        RemoveFault = RemoveFaultTool.Create(Servers);
    }

    public ApiTestSeams Seams { get; }

    public StringWriter ErrorOutput { get; } = new();

    public RunningSurlServers Servers { get; }

    public McpTool StartServer { get; }

    public McpTool ListServers { get; }

    public McpTool StopServer { get; }

    public McpTool GetExchanges { get; }

    public McpTool GetEvents { get; }

    public McpTool WaitForEvent { get; }

    public McpTool SeedMail { get; }

    public McpTool PutFile { get; }

    public McpTool PublishMessage { get; }

    public McpTool AddAccount { get; }

    public McpTool ResetServer { get; }

    public McpTool SnapshotServer { get; }

    public McpTool RestoreServer { get; }

    public McpTool AddFault { get; }

    public McpTool RemoveFault { get; }

    /// <summary>Parses <paramref name="json"/> as a tool's arguments.</summary>
    public static JsonElement Arguments(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>Calls <paramref name="tool"/> with <paramref name="argumentsJson"/>.</summary>
    public static Task<McpToolResult> CallAsync(McpTool tool, string argumentsJson, CancellationToken cancellationToken) =>
        tool.Handler(Arguments(argumentsJson), cancellationToken);

    public ValueTask DisposeAsync() => Servers.DisposeAsync();

    /// <summary>A clock that always reads one time and otherwise behaves as the system's.</summary>
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
