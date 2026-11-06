using Surl.Api;
using Surl.Protocol.Abstractions;

namespace Surl.Mcp;

/// <summary>
/// The servers this host started and has not stopped, keyed by their IDs (ADR-0103 decisions 3 and 5): it starts
/// each through <c>Surl.Api</c>, refusing a start past <see cref="SurlMcpHostSettings.MaxServers"/>, lists them in
/// start order, stops one, and stops every one still running when disposed.
/// </summary>
public sealed class RunningSurlServers : IAsyncDisposable
{
    private readonly Func<SurlServerOptions, CancellationToken, Task<SurlServer>> startServer;
    private readonly Lock gate = new();
    private readonly List<RunningSurlServer> servers = [];
    private int startsInProgress;
    private long lastServerNumber;

    /// <summary>Initializes a new instance of the <see cref="RunningSurlServers"/> class, starting servers with <see cref="SurlServer.StartAsync(SurlServerOptions, CancellationToken)"/>.</summary>
    /// <param name="settings">The host's settings.</param>
    /// <param name="errorOutput">The host's standard error, where every server's log is copied.</param>
    /// <param name="timeProvider">The clock every server runs on and start times are read from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public RunningSurlServers(SurlMcpHostSettings settings, TextWriter errorOutput, TimeProvider timeProvider)
        : this(settings, errorOutput, timeProvider, SurlServer.StartAsync)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RunningSurlServers"/> class, starting servers with <paramref name="startServer"/>.</summary>
    /// <param name="settings">The host's settings.</param>
    /// <param name="errorOutput">The host's standard error.</param>
    /// <param name="timeProvider">The host's clock.</param>
    /// <param name="startServer">Starts one server.</param>
    internal RunningSurlServers(
        SurlMcpHostSettings settings,
        TextWriter errorOutput,
        TimeProvider timeProvider,
        Func<SurlServerOptions, CancellationToken, Task<SurlServer>> startServer)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(errorOutput);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Settings = settings;
        ErrorOutput = errorOutput;
        TimeProvider = timeProvider;
        this.startServer = startServer;
    }

    /// <summary>Gets the host's settings.</summary>
    public SurlMcpHostSettings Settings { get; }

    /// <summary>Gets the host's standard error.</summary>
    public TextWriter ErrorOutput { get; }

    /// <summary>Gets the host's clock.</summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>Lists the running servers in start order.</summary>
    /// <returns>A snapshot later starts and stops do not change.</returns>
    public IReadOnlyList<RunningSurlServer> List()
    {
        lock (gate)
        {
            return [.. servers];
        }
    }

    /// <summary>Finds the running server <paramref name="serverId"/> names.</summary>
    /// <param name="serverId">The server's ID.</param>
    /// <returns>The server, or <see langword="null"/> when none is running under that ID.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="serverId"/> is <see langword="null"/>.</exception>
    public RunningSurlServer? Find(string serverId)
    {
        ArgumentNullException.ThrowIfNull(serverId);
        lock (gate)
        {
            return servers.Find(server => server.ServerId == serverId);
        }
    }

    /// <summary>
    /// Starts a server from <paramref name="options"/> and keeps it under the next ID, or returns
    /// <see langword="null"/> without starting one when <see cref="SurlMcpHostSettings.MaxServers"/> are already
    /// running or starting.
    /// </summary>
    /// <param name="listenUrls">The listen URLs as the caller gave them, kept for the list.</param>
    /// <param name="options">What to start.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The running server, or <see langword="null"/> when the limit is reached.</returns>
    /// <exception cref="ArgumentException">The options are misused, as <c>Surl.Api</c> says.</exception>
    /// <exception cref="SurlServerStartException"><c>surl</c> would refuse the start.</exception>
    public async Task<RunningSurlServer?> StartAsync(
        IReadOnlyList<string> listenUrls, SurlServerOptions options, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (servers.Count + startsInProgress >= Settings.MaxServers)
            {
                return null;
            }

            startsInProgress++;
        }

        try
        {
            var server = await startServer(options, cancellationToken);
            lock (gate)
            {
                var running = new RunningSurlServer($"s{++lastServerNumber}", [.. listenUrls], server, TimeProvider.GetUtcNow());
                servers.Add(running);
                return running;
            }
        }
        finally
        {
            lock (gate)
            {
                startsInProgress--;
            }
        }
    }

    /// <summary>Stops the server <paramref name="serverId"/> names and forgets it, or returns <see langword="null"/> when none is running under that ID.</summary>
    /// <param name="serverId">The server's ID.</param>
    /// <param name="cancellationToken">Cancels the wait, not the stop.</param>
    /// <returns>The exit code <c>surl</c> would have exited with, or <see langword="null"/>.</returns>
    public async Task<SurlExitCode?> StopAsync(string serverId, CancellationToken cancellationToken)
    {
        RunningSurlServer? running;
        lock (gate)
        {
            running = servers.Find(server => server.ServerId == serverId);
            if (running is null)
            {
                return null;
            }

            servers.Remove(running);
        }

        return await running.Server.StopAsync(cancellationToken);
    }

    /// <summary>Stops every server still running, all at once, and forgets them.</summary>
    /// <returns>A task that completes once every one has stopped.</returns>
    public async ValueTask DisposeAsync()
    {
        RunningSurlServer[] stopping;
        lock (gate)
        {
            stopping = [.. servers];
            servers.Clear();
        }

        await Task.WhenAll(stopping.Select(running => running.Server.StopAsync()));
    }
}
