using System.Runtime.InteropServices;
using System.Text;
using Surl.McpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Mcp;

/// <summary>
/// Entry point of the <c>surl-mcp</c> executable (ADR-0103 decisions 2 and 5): one MCP session on the process's
/// standard input and output, until the input ends or Ctrl+C or SIGTERM arrives, then every server it started is
/// stopped.
/// </summary>
internal static class Program
{
    /// <summary>The <c>serverInfo</c> name (ADR-0103 decision 6).</summary>
    public const string ServerName = "surl-mcp";

    /// <summary>The <c>serverInfo</c> title (ADR-0103 decision 6).</summary>
    public const string ServerTitle = "Surl";

    /// <summary>
    /// The <c>initialize</c> result's <c>instructions</c> (ADR-0103 decision 6): they name every registered tool,
    /// grouped by job.
    /// </summary>
    public const string Instructions =
        "These tools run Surl protocol servers: start_server, list_servers and stop_server. "
        + "To watch what clients do, get_exchanges shows what each client sent and what the server sent back, and "
        + "get_events and wait_for_event show and wait for each server's events (exchanges, logins, store changes, "
        + "faults). Seed a server's stores with seed_mail, put_file, publish_message, set_key, add_ldap_entries and "
        + "add_dns_zone, and delete a file with delete_file. Read back what the stores hold, what a client left there "
        + "included, with list_mailboxes, list_messages, read_message, read_file, list_files, browse_queue, "
        + "read_retained_message, read_log, read_key, list_keys, read_directory and read_zone. Manage who may log in "
        + "with add_account, remove_account and set_account_password; for proxy accounts add_proxy_account, "
        + "remove_proxy_account and set_proxy_account_password; for TLS-SRP accounts add_tls_srp_account, "
        + "remove_tls_srp_account and set_tls_srp_account_password. reset_server, snapshot_server and restore_server "
        + "control a server's stores between tests, and add_fault, list_faults and remove_fault make it misbehave on "
        + "purpose. The servers listen on loopback addresses unless whoever started surl-mcp allowed others. "
        + "The surl://aihelp resources explain the listen URLs and options start_server takes.";

    /// <summary>Runs <c>surl-mcp</c> on the process's standard streams until its input ends or a signal stops it.</summary>
    /// <param name="args">The command-line arguments, without the program name.</param>
    /// <returns>The exit code <see cref="RunAsync(Stream, Stream, TextWriter, IReadOnlyList{string}, Func{string, string?}, CancellationToken)"/> returns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is null.</exception>
    internal static Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return RunUntilSignalledAsync(args);
    }

    /// <summary>Runs <c>surl-mcp</c> with the process's seams: real sockets, the disk and the process's current directory.</summary>
    /// <param name="input">Standard input, where the client writes its messages.</param>
    /// <param name="output">Standard output, which carries only MCP messages, or the help or the version.</param>
    /// <param name="error">Standard error, where every log line and refusal goes.</param>
    /// <param name="arguments">The command-line arguments, without the program name.</param>
    /// <param name="readEnvironmentVariable">Reads an environment variable; <c>SURL_MCP_ROOT</c> is read.</param>
    /// <param name="cancellationToken">Cancelled to end the session.</param>
    /// <returns>0, or 2 for a refused command line or a root that does not exist.</returns>
    internal static Task<int> RunAsync(
        Stream input,
        Stream output,
        TextWriter error,
        IReadOnlyList<string> arguments,
        Func<string, string?> readEnvironmentVariable,
        CancellationToken cancellationToken) =>
        RunAsync(input, output, error, arguments, readEnvironmentVariable, SurlMcpHostSeams.Process, cancellationToken);

    /// <summary>Runs <c>surl-mcp</c> with <paramref name="seams"/>: the entry point tests call with in-memory streams.</summary>
    /// <param name="input">Standard input.</param>
    /// <param name="output">Standard output.</param>
    /// <param name="error">Standard error.</param>
    /// <param name="arguments">The command-line arguments, without the program name.</param>
    /// <param name="readEnvironmentVariable">Reads an environment variable.</param>
    /// <param name="seams">What the host asks of the process.</param>
    /// <param name="cancellationToken">Cancelled to end the session.</param>
    /// <returns>0, or 2 for a refused command line or a root that does not exist.</returns>
    internal static async Task<int> RunAsync(
        Stream input,
        Stream output,
        TextWriter error,
        IReadOnlyList<string> arguments,
        Func<string, string?> readEnvironmentVariable,
        SurlMcpHostSeams seams,
        CancellationToken cancellationToken)
    {
        var commandLine = SurlMcpCommandLine.Parse(arguments, readEnvironmentVariable(SurlMcpCommandLine.RootEnvironmentVariable));
        switch (commandLine.Action)
        {
            case SurlMcpCommandLineAction.Refuse:
                await error.WriteLineAsync(commandLine.Refusal);
                return (int)SurlExitCode.FailedInit;
            case SurlMcpCommandLineAction.ShowHelp:
                return await WriteAnswerAsync(output, SurlMcpHelpText.Help, cancellationToken);
            case SurlMcpCommandLineAction.ShowVersion:
                return await WriteAnswerAsync(output, SurlMcpHelpText.VersionLine, cancellationToken);
            default:
                return await ServeAsync(input, output, error, commandLine.Settings!, seams, cancellationToken);
        }
    }

    /// <summary>
    /// Creates the Ctrl+C and SIGTERM handler: it keeps the runtime from ending the process at once, and cancels
    /// <paramref name="stop"/> so the session ends and every server is stopped.
    /// </summary>
    /// <param name="stop">The source whose token ends the session.</param>
    /// <returns>The handler.</returns>
    internal static Action<PosixSignalContext> CreateStopOnSignal(CancellationTokenSource stop) =>
        context =>
        {
            context.Cancel = true;
            stop.Cancel();
        };

    private static async Task<int> RunUntilSignalledAsync(string[] args)
    {
        using var stop = new CancellationTokenSource();
        var stopOnSignal = CreateStopOnSignal(stop);
        using var interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, stopOnSignal);
        using var terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, stopOnSignal);
        await using var input = System.Console.OpenStandardInput();
        await using var output = System.Console.OpenStandardOutput();

        return await RunAsync(input, output, System.Console.Error, args, Environment.GetEnvironmentVariable, stop.Token);
    }

    private static async Task<int> WriteAnswerAsync(Stream output, string text, CancellationToken cancellationToken)
    {
        await output.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
        await output.FlushAsync(cancellationToken);
        return (int)SurlExitCode.Ok;
    }

    private static async Task<int> ServeAsync(
        Stream input, Stream output, TextWriter error, SurlMcpHostSettings settings, SurlMcpHostSeams seams, CancellationToken cancellationToken)
    {
        if (settings.Root is not null)
        {
            if (!seams.DirectoryExists(settings.Root))
            {
                await error.WriteLineAsync($"surl-mcp: ({(int)SurlExitCode.FailedInit}) the root {settings.Root} is not a directory");
                return (int)SurlExitCode.FailedInit;
            }

            // PathConfinement takes a relative path from the current directory, so the root must be it (BL-624).
            settings = settings with { Root = Path.GetFullPath(settings.Root) };
            seams.SetCurrentDirectory(settings.Root);
        }

        await using var servers = seams.CreateServers(settings, error);
        var registry = new McpServerRegistry();
        registry.AddTool(StartServerTool.Create(servers));
        registry.AddTool(ListServersTool.Create(servers));
        registry.AddTool(StopServerTool.Create(servers));
        registry.AddTool(GetExchangesTool.Create(servers));
        registry.AddTool(GetEventsTool.Create(servers));
        registry.AddTool(WaitForEventTool.Create(servers));
        registry.AddTool(SeedMailTool.Create(servers));
        registry.AddTool(PutFileTool.Create(servers));
        registry.AddTool(DeleteFileTool.Create(servers));
        registry.AddTool(ListMailboxesTool.Create(servers));
        registry.AddTool(ListMessagesTool.Create(servers));
        registry.AddTool(ReadMessageTool.Create(servers));
        registry.AddTool(ReadFileTool.Create(servers));
        registry.AddTool(ListFilesTool.Create(servers));
        registry.AddTool(BrowseQueueTool.Create(servers));
        registry.AddTool(ReadRetainedMessageTool.Create(servers));
        registry.AddTool(ReadLogTool.Create(servers));
        registry.AddTool(ReadKeyTool.Create(servers));
        registry.AddTool(ListKeysTool.Create(servers));
        registry.AddTool(ReadDirectoryTool.Create(servers));
        registry.AddTool(ReadZoneTool.Create(servers));
        registry.AddTool(PublishMessageTool.Create(servers));
        registry.AddTool(AddAccountTool.Create(servers));
        registry.AddTool(SetKeyTool.Create(servers));
        registry.AddTool(AddLdapEntriesTool.Create(servers));
        registry.AddTool(AddDnsZoneTool.Create(servers));
        registry.AddTool(RemoveAccountTool.Create(servers));
        registry.AddTool(AddProxyAccountTool.Create(servers));
        registry.AddTool(RemoveProxyAccountTool.Create(servers));
        registry.AddTool(AddTlsSrpAccountTool.Create(servers));
        registry.AddTool(RemoveTlsSrpAccountTool.Create(servers));
        registry.AddTool(SetAccountPasswordTool.Create(servers));
        registry.AddTool(SetProxyAccountPasswordTool.Create(servers));
        registry.AddTool(SetTlsSrpAccountPasswordTool.Create(servers));
        registry.AddTool(ResetServerTool.Create(servers));
        registry.AddTool(SnapshotServerTool.Create(servers));
        registry.AddTool(RestoreServerTool.Create(servers));
        registry.AddTool(AddFaultTool.Create(servers));
        registry.AddTool(ListFaultsTool.Create(servers));
        registry.AddTool(RemoveFaultTool.Create(servers));
        registry.AddResources(new AiHelpResourceProvider());
        var options = new McpSessionOptions(new McpServerInfo(ServerName, ServerTitle, SurlMcpHelpText.Version), Instructions);
        await McpStreamSession.RunAsync(input, output, registry, options, cancellationToken);
        return (int)SurlExitCode.Ok;
    }
}
