using Surl.Cli;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// Where <c>surl</c>'s log goes (ADR-0033, sections 1, 4 and 6): the log stream - stderr, or
/// the <c>--log-file</c>, appended, with <c>-</c> for stdout - and the trace file, truncated,
/// with <c>-</c> for stdout. Disposing it closes the files it opened, never stdout or stderr.
/// </summary>
internal sealed class LogStreams : IDisposable
{
    private readonly List<TextWriter> openedFiles;

    private LogStreams(TextWriter log, TextWriter? trace, List<TextWriter> openedFiles)
    {
        Log = log;
        Trace = trace;
        this.openedFiles = openedFiles;
    }

    /// <summary>The log stream: the warnings, the throwaway-certificate note and the exchange log below the trace level.</summary>
    public TextWriter Log { get; }

    /// <summary>Where the trace dump goes when a trace file was given, or <see langword="null"/> to dump to <see cref="Log"/>.</summary>
    public TextWriter? Trace { get; }

    /// <summary>
    /// Opens the streams the command line names, before any listener binds. A file that cannot
    /// be opened ends <c>surl</c> with <see cref="SurlExitCode.CouldNotWriteFile"/> (ADR-0033,
    /// section 6), and any file opened before it is closed again; so does a trace file whose full
    /// path, ignoring case, is the <c>--log-file</c> file's (ADR-0036).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="output">stdout, for <c>-</c>.</param>
    /// <param name="error">stderr: the log stream without <c>--log-file</c>.</param>
    /// <param name="openLogFile">Opens a file in the given mode; <c>surl</c> passes <see cref="LogFile.Open"/>.</param>
    /// <returns>The streams, or the <c>(23)</c> message after the <c>surl: </c> prefix when a file cannot be opened.</returns>
    internal static (LogStreams? Streams, string? FailureMessage) Open(
        SurlCommandLine commandLine, TextWriter output, TextWriter error, Func<string, FileMode, TextWriter> openLogFile)
    {
        var openedFiles = new List<TextWriter>();
        var (log, failureMessage) = Choose(
            commandLine.LogFile, "--log-file", FileMode.Append, logFile: null, output, openLogFile, openedFiles);
        var (trace, traceFailureMessage) = failureMessage is null
            ? Choose(
                commandLine.TraceFile,
                NameTraceOption(commandLine.TraceLayout),
                FileMode.Create,
                commandLine.LogFile,
                output,
                openLogFile,
                openedFiles)
            : (null, null);
        failureMessage ??= traceFailureMessage;
        if (failureMessage is not null)
        {
            openedFiles.ForEach(file => file.Dispose());
            return (null, failureMessage);
        }

        return (new LogStreams(log ?? error, trace, openedFiles), null);
    }

    /// <summary>
    /// Builds the exchange log of the command line's level: the dump at
    /// <see cref="LogLevel.Trace"/>, to the trace file or else the log stream, and the levelled
    /// log to the log stream below it, each stamped with <paramref name="timeProvider"/>'s local
    /// time when <c>--trace-time</c> was given.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="timeProvider">The clock that stamps the lines.</param>
    /// <returns>The exchange log factory.</returns>
    public IExchangeLogFactory CreateExchangeLogFactory(SurlCommandLine commandLine, TimeProvider timeProvider) =>
        commandLine.LogLevel == LogLevel.Trace
            ? new TraceExchangeLogFactory(Trace ?? Log, commandLine.TraceLayout, commandLine.TraceTime, timeProvider)
            : new LevelledExchangeLogFactory(Log, commandLine.LogLevel, commandLine.TraceTime, timeProvider);

    /// <inheritdoc/>
    public void Dispose() => openedFiles.ForEach(file => file.Dispose());

    private static string NameTraceOption(TraceDumpLayout layout) =>
        layout == TraceDumpLayout.Ascii ? "--trace-ascii" : "--trace";

    // No file is none, "-" is stdout, a trace file that is the --log-file file is refused
    // (ADR-0036), and any other is opened and kept to close later.
    private static (TextWriter? Writer, string? FailureMessage) Choose(
        string? file,
        string option,
        FileMode mode,
        string? logFile,
        TextWriter output,
        Func<string, FileMode, TextWriter> openLogFile,
        List<TextWriter> openedFiles)
    {
        if (file is null or "-")
        {
            return (file is null ? null : output, null);
        }

        try
        {
            if (NameTheSameFile(file, logFile))
            {
                return (null, $"(23) Could not open {file} for {option}: --log-file names the same file");
            }

            var opened = openLogFile(file, mode);
            openedFiles.Add(opened);
            return (opened, null);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return (null, $"(23) Could not open {file} for {option}: {failure.Message}");
        }
    }

    // The same full path, ignoring case on every platform, so Windows, Linux and macOS refuse
    // alike (ADR-0036). "-" is stdout, never a file.
    private static bool NameTheSameFile(string file, string? logFile) =>
        logFile is not null and not "-"
        && string.Equals(Path.GetFullPath(file), Path.GetFullPath(logFile), StringComparison.OrdinalIgnoreCase);
}
