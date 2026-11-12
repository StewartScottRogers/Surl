using Surl.Cli;
using Surl.Extended.Protocol.McpServer;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Serves <c>mcp+stdio:</c> (ADR-0111 decision 5): one MCP session on the host's standard input and output, the
/// only listen URL of its run. Standard output carries only MCP messages; every line this writes goes to the log.
/// </summary>
public static class McpStdioComposition
{
    private const string CannotCombine = "mcp+stdio: serves standard input and output and cannot be combined with another listen URL";

    /// <summary>Tells whether the command line asks for <c>mcp+stdio:</c>.</summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns><see langword="true"/> when any listen URL is <c>mcp+stdio</c>.</returns>
    public static bool IsAsked(SurlCommandLine commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        return commandLine.ListenUrls.Any(listenUrl => listenUrl.Scheme == ListenUrlParser.StdioScheme);
    }

    /// <summary>
    /// Refuses a command line that would put anything but MCP messages on standard output, or that serves another
    /// listen URL beside <c>mcp+stdio:</c>; both are <see cref="SurlExitCode.FailedInit"/>.
    /// </summary>
    /// <param name="commandLine">The parsed command line that <see cref="IsAsked"/>.</param>
    /// <returns>The refusal, or <see langword="null"/>.</returns>
    public static SurlStartFailure? FindRefusal(SurlCommandLine commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        if (commandLine.ListenUrls.Count > 1 || commandLine.ProxyListenUrls.Count > 0)
        {
            return Refuse(CannotCombine);
        }

        if (commandLine.TraceFile == "-" || commandLine.LogFile == "-")
        {
            return Refuse("mcp+stdio: standard output carries only MCP messages, so --trace, --trace-ascii and --log-file cannot write to '-'");
        }

        return null;
    }

    /// <summary>
    /// Serves one session over <paramref name="input"/> and <paramref name="output"/> until the input ends or
    /// <paramref name="cancellationToken"/> is cancelled, over the content store the command line's exposure options give.
    /// </summary>
    /// <param name="commandLine">The parsed command line, already past <see cref="FindRefusal"/>.</param>
    /// <param name="timeProvider">The clock the in-memory content store stamps with.</param>
    /// <param name="input">Standard input.</param>
    /// <param name="output">Standard output: only MCP messages are written to it.</param>
    /// <param name="log">The log stream, standard error: the status line goes here.</param>
    /// <param name="cancellationToken">Ends the session.</param>
    /// <returns>A task that completes when the session has ended.</returns>
    public static async Task ServeAsync(
        SurlCommandLine commandLine,
        TimeProvider timeProvider,
        Stream input,
        Stream output,
        TextWriter log,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(log);

        var store = SurlServerComposition.ComposeContentStore(commandLine, timeProvider);
        if (commandLine.LogLevel >= LogLevel.Info)
        {
            await log.WriteLineAsync("Listening on mcp+stdio:");
        }

        await McpStdioSession.RunAsync(
            input, output, store, ExtendedProtocolServers.SurlVersion, (int)Math.Min(commandLine.Limits.MaxMessageBytes, int.MaxValue), cancellationToken);
    }

    private static SurlStartFailure Refuse(string line) => new(SurlExitCode.FailedInit, [line]);
}
