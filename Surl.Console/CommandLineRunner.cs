using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using Surl.Cli;
using Surl.Content;
using Surl.Core;
using Surl.Output;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Http;

namespace Surl.Console;

/// <summary>
/// Runs one <c>surl</c> command line: the composition root. It parses the command line,
/// answers <c>--help</c> and <c>--version</c>, checks the served directory and the schemes,
/// then constructs the content store, the protocol servers, the exchange log and the serving
/// engine explicitly and serves until cancelled, writing ADR-0007 section 5's texts and
/// returning its exit codes.
/// </summary>
/// <param name="listenerFactory">Starts the listeners.</param>
/// <param name="canOpenServedDirectory">
/// Tells whether the served directory, as given, can be opened; <c>surl</c> passes
/// <see cref="ServedDirectoryProbe.CanOpen"/>.
/// </param>
/// <param name="timeProvider">The one clock every exchange runs on.</param>
internal sealed class CommandLineRunner(
    IListenerFactory listenerFactory, Func<string, bool> canOpenServedDirectory, TimeProvider timeProvider)
{
    private const string MessagePrefix = "surl: ";

    /// <summary>
    /// Runs <paramref name="args"/>.
    /// </summary>
    /// <param name="args">The command-line arguments, without the program name.</param>
    /// <param name="output">Where the help, the version and the status lines go.</param>
    /// <param name="error">Where every <c>surl: </c> message and the verbose log go.</param>
    /// <param name="cancellationToken">Cancelled to stop serving.</param>
    /// <returns>The exit code, as ADR-0007 section 5 gives it.</returns>
    public async Task<SurlExitCode> RunAsync(
        IReadOnlyList<string> args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var parsed = CommandLineParser.Parse(args);

        return parsed.Outcome switch
        {
            CommandLineOutcome.ShowHelp => WriteText(output, HelpText.Text),
            CommandLineOutcome.ShowVersion => WriteText(output, ComposeVersionText()),
            CommandLineOutcome.Refused => WriteRefusal(error, parsed.Failure!),
            _ => await ServeAsync(parsed.CommandLine!, output, error, cancellationToken),
        };
    }

    /// <summary>
    /// Formats the <c>(45)</c> or <c>(6)</c> message for a listener that could not bind, after
    /// the <c>surl: </c> prefix (ADR-0007 section 5).
    /// </summary>
    /// <param name="bindFailure">The failure the listener factory threw.</param>
    /// <returns>The message.</returns>
    internal static string FormatBindFailure(ListenerBindException bindFailure)
    {
        var listenUrl = bindFailure.ListenUrl;
        if (bindFailure.Failure == ListenerBindFailure.HostNotFound)
        {
            return $"(6) Could not resolve host: {listenUrl.Host}";
        }

        var address = bindFailure.EndPoint is IPEndPoint endPoint ? endPoint.Address.ToString() : listenUrl.Host;
        var bracketed = address.Contains(':', StringComparison.Ordinal) ? $"[{address}]" : address;
        return $"(45) Could not bind {listenUrl.Scheme}://{bracketed}:{listenUrl.Port}/: {DescribeBindFailure(bindFailure.Failure)}";
    }

    private static string DescribeBindFailure(ListenerBindFailure failure) => failure switch
    {
        ListenerBindFailure.AddressInUse => "Address already in use",
        ListenerBindFailure.AddressNotAvailable => "Address not available",
        ListenerBindFailure.PermissionDenied => "Permission denied",
        _ => "Bind failed",
    };

    private static SurlExitCode WriteText(TextWriter output, string text)
    {
        output.Write(text);
        return SurlExitCode.Ok;
    }

    private static SurlExitCode WriteRefusal(TextWriter error, CommandLineFailure failure)
    {
        error.WriteLine(MessagePrefix + failure.Message);
        if (failure.FollowedByTryHelpLine)
        {
            error.WriteLine(MessagePrefix + CommandLineFailure.TryHelpLine);
        }

        return failure.ExitCode;
    }

    private static SurlExitCode WriteFailure(TextWriter error, SurlExitCode exitCode, string message)
    {
        error.WriteLine(MessagePrefix + message);
        return exitCode;
    }

    private static string ComposeVersionText()
    {
        var informationalVersion = typeof(CommandLineRunner).Assembly
            .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
            .Select(attribute => attribute.InformationalVersion)
            .FirstOrDefault();
        var servers = ComposeProtocolServers(ComposeContentStore(new SurlCommandLine()));

        return VersionText.Compose(
            informationalVersion, RuntimeInformation.RuntimeIdentifier, servers.SelectMany(server => server.Schemes));
    }

    /// <summary>
    /// Builds the one content store every protocol server reads: the served directory as a full
    /// path, on disk, exposing what the command line's exposure options allow (ADR-0006).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns>The content store.</returns>
    internal static ContentStore ComposeContentStore(SurlCommandLine commandLine) =>
        new(Path.GetFullPath(commandLine.ServedDirectory), new DiskContentFileSystem(), MapExposureOptions(commandLine));

    private static ContentExposureOptions MapExposureOptions(SurlCommandLine commandLine) => new()
    {
        AllowUploads = commandLine.AllowUploads,
        ListDirectories = commandLine.ListDirectories,
        FollowSymbolicLinks = commandLine.FollowSymlinks,
        ServeDotFiles = commandLine.ServeDotFiles,
        MaxUploadBytes = commandLine.MaxUploadBytes,
    };

    // Every protocol server surl registers, each serving the one content store.
    private static IProtocolServer[] ComposeProtocolServers(ContentStore contentStore) =>
        [new HttpProtocolServer(contentStore)];

    private static string? FindUnregisteredScheme(IReadOnlyList<ListenUrl> listenUrls, IProtocolServer[] servers)
    {
        var registeredSchemes = servers
            .OfType<IConnectionProtocolServer>()
            .SelectMany(server => server.Schemes)
            .ToHashSet(StringComparer.Ordinal);

        return listenUrls.Select(listenUrl => listenUrl.Scheme).FirstOrDefault(scheme => !registeredSchemes.Contains(scheme));
    }

    private async Task<SurlExitCode> ServeAsync(
        SurlCommandLine commandLine, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (!canOpenServedDirectory(commandLine.ServedDirectory))
        {
            return WriteFailure(
                error, SurlExitCode.CouldNotReadFile, $"(37) Could not open directory {commandLine.ServedDirectory}");
        }

        var servers = ComposeProtocolServers(ComposeContentStore(commandLine));
        if (FindUnregisteredScheme(commandLine.ListenUrls, servers) is { } scheme)
        {
            return WriteFailure(error, SurlExitCode.UnsupportedProtocol, $"(1) Protocol \"{scheme}\" not supported");
        }

        var reporter = new ListenerStartReporter(
            listenerFactory, new ListenerStatusLine(output), commandLine.ListenUrls.Count);
        var engine = new ServingEngine(
            reporter,
            servers,
            new VerboseExchangeLogFactory(error, commandLine.Verbose),
            timeProvider,
            ServingEngine.DefaultShutdownGracePeriod);

        try
        {
            var exitCode = await engine.ServeAsync(commandLine.ListenUrls, cancellationToken);
            return reporter.BindFailure is { } bindFailure
                ? WriteFailure(error, exitCode, FormatBindFailure(bindFailure))
                : exitCode;
        }
        catch (Exception exception)
        {
            return WriteFailure(error, SurlExitCode.InternalError, $"(125) Internal error: {exception.Message}");
        }
    }
}
