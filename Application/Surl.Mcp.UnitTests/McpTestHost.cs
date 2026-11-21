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
        TimeProvider? timeProvider = null,
        bool areExtensionsTurnedOn = false)
    {
        Seams = new ApiTestSeams(listenerFactory);
        var hostSettings = settings ?? new SurlMcpHostSettings();
        Servers = new RunningSurlServers(
            hostSettings,
            ErrorOutput,
            timeProvider ?? new FixedTimeProvider(Now),
            (options, token) =>
            {
                options.AreExtensionsTurnedOn = areExtensionsTurnedOn ? true : options.AreExtensionsTurnedOn;
                return SurlServer.StartAsync(options, Seams.Host, token);
            });
        StartServer = safetyCheck is null ? StartServerTool.Create(Servers) : StartServerTool.Create(Servers, safetyCheck);
        ListServers = ListServersTool.Create(Servers);
        StopServer = StopServerTool.Create(Servers);
        GetExchanges = GetExchangesTool.Create(Servers);
        GetEvents = GetEventsTool.Create(Servers);
        WaitForEvent = WaitForEventTool.Create(Servers);
        SeedMail = SeedMailTool.Create(Servers);
        PutFile = PutFileTool.Create(Servers);
        DeleteFile = DeleteFileTool.Create(Servers);
        ListMailboxes = ListMailboxesTool.Create(Servers);
        ListMessages = ListMessagesTool.Create(Servers);
        ReadMessage = ReadMessageTool.Create(Servers);
        ReadFile = ReadFileTool.Create(Servers);
        ListFiles = ListFilesTool.Create(Servers);
        BrowseQueue = BrowseQueueTool.Create(Servers);
        ReadRetainedMessage = ReadRetainedMessageTool.Create(Servers);
        ReadLog = ReadLogTool.Create(Servers);
        ReadKey = ReadKeyTool.Create(Servers);
        ListKeys = ListKeysTool.Create(Servers);
        ReadDirectory = ReadDirectoryTool.Create(Servers);
        ReadZone = ReadZoneTool.Create(Servers);
        RemoveAccount = RemoveAccountTool.Create(Servers);
        AddProxyAccount = AddProxyAccountTool.Create(Servers);
        RemoveProxyAccount = RemoveProxyAccountTool.Create(Servers);
        AddTlsSrpAccount = AddTlsSrpAccountTool.Create(Servers);
        RemoveTlsSrpAccount = RemoveTlsSrpAccountTool.Create(Servers);
        SetAccountPassword = SetAccountPasswordTool.Create(Servers);
        SetProxyAccountPassword = SetProxyAccountPasswordTool.Create(Servers);
        SetTlsSrpAccountPassword = SetTlsSrpAccountPasswordTool.Create(Servers);
        PublishMessage = PublishMessageTool.Create(Servers);
        AddAccount = AddAccountTool.Create(Servers);
        SetKey = SetKeyTool.Create(Servers);
        AddLdapEntries = AddLdapEntriesTool.Create(Servers);
        AddDnsZone = AddDnsZoneTool.Create(Servers);
        ResetServer = ResetServerTool.Create(Servers);
        SnapshotServer = SnapshotServerTool.Create(Servers);
        RestoreServer = RestoreServerTool.Create(Servers);
        AddFault = AddFaultTool.Create(Servers);
        ListFaults = ListFaultsTool.Create(Servers);
        RemoveFault = RemoveFaultTool.Create(Servers);
    }

    public ApiTestSeams Seams { get; }

    public StringWriter ErrorOutput { get; } = new();

    public RunningSurlServers Servers { get; }

    public McpTool StartServer { get; }

    public McpTool ListServers { get; }

    public McpTool StopServer { get; }

    public McpTool DeleteFile { get; }

    public McpTool ListMailboxes { get; }

    public McpTool ListMessages { get; }

    public McpTool ReadMessage { get; }

    public McpTool ReadFile { get; }

    public McpTool ListFiles { get; }

    public McpTool BrowseQueue { get; }

    public McpTool ReadRetainedMessage { get; }

    public McpTool ReadLog { get; }

    public McpTool ReadKey { get; }

    public McpTool ListKeys { get; }

    public McpTool ReadDirectory { get; }

    public McpTool ReadZone { get; }

    public McpTool RemoveAccount { get; }
    public McpTool AddProxyAccount { get; }
    public McpTool RemoveProxyAccount { get; }
    public McpTool AddTlsSrpAccount { get; }
    public McpTool RemoveTlsSrpAccount { get; }
    public McpTool SetAccountPassword { get; }
    public McpTool SetProxyAccountPassword { get; }
    public McpTool SetTlsSrpAccountPassword { get; }

    public McpTool GetExchanges { get; }

    public McpTool GetEvents { get; }

    public McpTool WaitForEvent { get; }

    public McpTool SeedMail { get; }

    public McpTool PutFile { get; }

    public McpTool PublishMessage { get; }

    public McpTool AddAccount { get; }

    public McpTool SetKey { get; }

    public McpTool AddLdapEntries { get; }

    public McpTool AddDnsZone { get; }

    public McpTool ResetServer { get; }

    public McpTool SnapshotServer { get; }

    public McpTool RestoreServer { get; }

    public McpTool AddFault { get; }

    public McpTool ListFaults { get; }

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
