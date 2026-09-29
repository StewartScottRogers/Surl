using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using Surl.Cli;
using Surl.Content;
using Surl.Core;
using Surl.Networking;
using Surl.Output;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Dict;
using Surl.Protocol.Gopher;
using Surl.Protocol.Http;
using Surl.Protocol.Mqtt;
using Surl.Protocol.Telnet;
using Surl.Protocol.Tftp;

namespace Surl.Console;

/// <summary>
/// Runs one <c>surl</c> command line: the composition root. It parses the command line,
/// answers <c>--help</c> and <c>--version</c>, checks the data directory when one is given and
/// the schemes, then constructs the TLS settings, the content store (on disk or in memory), the
/// protocol servers, the exchange log and the serving engine explicitly and serves until
/// cancelled, writing ADR-0007 section 5's texts and returning its exit codes.
/// </summary>
/// <param name="createListenerFactory">
/// Creates the factory that starts the listeners, given the process's TLS settings
/// (<see langword="null"/> when no listen URL is TLS from the first byte); <c>surl</c> passes
/// one that creates a <see cref="SocketListenerFactory"/>.
/// </param>
/// <param name="canOpenDataDirectory">
/// Tells whether the data directory, as given with <c>--directory</c>, can be opened; never
/// called without <c>--directory</c>. <c>surl</c> passes <see cref="ServedDirectoryProbe.CanOpen"/>.
/// </param>
/// <param name="timeProvider">The one clock every exchange runs on.</param>
internal sealed class CommandLineRunner(
    Func<ServerTlsSettings?, IListenerFactory> createListenerFactory,
    Func<string, bool> canOpenDataDirectory,
    TimeProvider timeProvider)
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

    private string ComposeVersionText()
    {
        var informationalVersion = typeof(CommandLineRunner).Assembly
            .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
            .Select(attribute => attribute.InformationalVersion)
            .FirstOrDefault();
        var servers = ComposeProtocolServers(ComposeContentStore(new SurlCommandLine(), timeProvider));

        return VersionText.Compose(
            informationalVersion, RuntimeInformation.RuntimeIdentifier, servers.SelectMany(server => server.Schemes));
    }

    /// <summary>
    /// Builds the one content store every protocol server reads, exposing what the command
    /// line's exposure options allow (ADR-0006): the data directory's full path on disk with
    /// <c>--directory</c>, and a new, empty in-memory file system at
    /// <see cref="InMemoryContentFileSystem.RootPath"/> without it (ADR-0031 decisions 1 and 4).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="timeProvider">The clock the in-memory file system stamps last-write times with.</param>
    /// <returns>The content store.</returns>
    internal static ContentStore ComposeContentStore(SurlCommandLine commandLine, TimeProvider timeProvider) =>
        new(
            commandLine.DataDirectory is { } dataDirectory ? Path.GetFullPath(dataDirectory) : InMemoryContentFileSystem.RootPath,
            ComposeContentFileSystem(commandLine, timeProvider),
            MapExposureOptions(commandLine));

    /// <summary>
    /// Chooses the file system the content store serves: <see cref="DiskContentFileSystem"/>
    /// with <c>--directory</c>, a new <see cref="InMemoryContentFileSystem"/> without it.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="timeProvider">The clock the in-memory file system stamps last-write times with.</param>
    /// <returns>The file system.</returns>
    internal static IContentFileSystem ComposeContentFileSystem(SurlCommandLine commandLine, TimeProvider timeProvider) =>
        commandLine.DataDirectory is null ? new InMemoryContentFileSystem(timeProvider) : new DiskContentFileSystem();

    private static ContentExposureOptions MapExposureOptions(SurlCommandLine commandLine) => new()
    {
        AllowUploads = commandLine.AllowUploads,
        ListDirectories = commandLine.ListDirectories,
        FollowSymbolicLinks = commandLine.FollowSymlinks,
        ServeDotFiles = commandLine.ServeDotFiles,
        MaxUploadBytes = commandLine.Limits.MaxUploadBytes,
    };

    /// <summary>
    /// Builds the connection limits the serving engine enforces from the command line's
    /// <c>--max-connections</c>, <c>--max-connections-per-address</c>, <c>--idle-timeout</c>
    /// and <c>-m</c>/<c>--max-time</c> (ADR-0006 section 1). 0 seconds, parsed as
    /// <see cref="Timeout.InfiniteTimeSpan"/>, becomes <see cref="TimeSpan.Zero"/>: no limit.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns>The connection limits.</returns>
    internal static ConnectionLimits ComposeConnectionLimits(SurlCommandLine commandLine) => new(
        commandLine.MaxConnections,
        commandLine.MaxConnectionsPerAddress,
        NoLimitAsZero(commandLine.IdleTimeout),
        NoLimitAsZero(commandLine.MaxTime));

    private static TimeSpan NoLimitAsZero(TimeSpan seconds) =>
        seconds == Timeout.InfiniteTimeSpan ? TimeSpan.Zero : seconds;

    /// <summary>
    /// Formats the message for a TLS option file that could not be loaded, after the
    /// <c>surl: </c> prefix, and the exit code it ends surl with (ADR-0010 section 3, ADR-0020).
    /// </summary>
    /// <param name="failure">The failure the loader threw.</param>
    /// <param name="caCertificateFile">The <c>--cacert</c> file as given, named by the "does not exist" lines.</param>
    /// <returns>The exit code and the message lines.</returns>
    internal static (SurlExitCode ExitCode, string[] Lines) DescribeTlsFileFailure(
        TlsFileLoadException failure, string? caCertificateFile) => failure.Failure switch
        {
            TlsFileLoadFailure.CaCertificateNotFound => (
                SurlExitCode.FailedInit,
                [$"The file '{caCertificateFile}' provided to --cacert does not exist", "option --cacert: is badly used here"]),
            TlsFileLoadFailure.CaCertificateUnreadable => (
                SurlExitCode.CaCertificateBadFile, [$"(77) Could not load the CA certificates: {failure.Message}"]),
            _ => (SurlExitCode.CertificateProblem, [$"(58) Could not load the server certificate: {failure.Message}"]),
        };

    // Every protocol server surl registers, over TCP or (TFTP) UDP; those that serve files serve
    // the one content store, and the MQTT server keeps its retained messages for as long as surl
    // runs. https is the HTTP server itself, over a connection the engine has secured (ADR-0020).
    private static IProtocolServer[] ComposeProtocolServers(ContentStore contentStore)
    {
        var httpServer = new HttpProtocolServer(contentStore);

        return
        [
            httpServer,
            new ImplicitTlsSchemeServer(httpServer, "https"),
            new DictProtocolServer(contentStore),
            new GopherProtocolServer(contentStore),
            new MqttProtocolServer(new MqttRetainedMessages()),
            new TelnetProtocolServer(),
            new TftpProtocolServer(contentStore),
        ];
    }

    private static SurlExitCode WriteTlsFileFailure(TextWriter error, TlsFileLoadException failure, string? caCertificateFile)
    {
        var (exitCode, lines) = DescribeTlsFileFailure(failure, caCertificateFile);
        foreach (var line in lines)
        {
            error.WriteLine(MessagePrefix + line);
        }

        return exitCode;
    }

    private static string? FindUnregisteredScheme(IReadOnlyList<ListenUrl> listenUrls, IProtocolServer[] servers)
    {
        var registeredSchemes = servers
            .SelectMany(server => server.Schemes)
            .ToHashSet(StringComparer.Ordinal);

        return listenUrls.Select(listenUrl => listenUrl.Scheme).FirstOrDefault(scheme => !registeredSchemes.Contains(scheme));
    }

    private async Task<SurlExitCode> ServeAsync(
        SurlCommandLine commandLine, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (commandLine.DataDirectory is { } dataDirectory && !canOpenDataDirectory(dataDirectory))
        {
            return WriteFailure(error, SurlExitCode.CouldNotReadFile, $"(37) Could not open directory {dataDirectory}");
        }

        var servers = ComposeProtocolServers(ComposeContentStore(commandLine, timeProvider));
        if (FindUnregisteredScheme(commandLine.ListenUrls, servers) is { } scheme)
        {
            return WriteFailure(error, SurlExitCode.UnsupportedProtocol, $"(1) Protocol \"{scheme}\" not supported");
        }

        ServerTlsComposition tls;
        try
        {
            tls = ServerTlsComposition.Compose(commandLine, timeProvider);
        }
        catch (TlsFileLoadException failure)
        {
            return WriteTlsFileFailure(error, failure, commandLine.CaCertificateFile);
        }

        using (tls)
        {
            return await ServeSecuredAsAskedAsync(commandLine, servers, tls, output, error, cancellationToken);
        }
    }

    private async Task<SurlExitCode> ServeSecuredAsAskedAsync(
        SurlCommandLine commandLine,
        IProtocolServer[] servers,
        ServerTlsComposition tls,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (commandLine.Verbose && tls.ThrowawayCertificateFingerprint is { } fingerprint)
        {
            error.WriteLine($"* Serving a throwaway certificate, SHA-256 {fingerprint}");
        }

        var reporter = new ListenerStartReporter(
            createListenerFactory(tls.Settings), new ListenerStatusLine(output), commandLine.ListenUrls.Count);
        var engine = new ServingEngine(
            reporter,
            servers,
            new VerboseExchangeLogFactory(error, commandLine.Verbose),
            timeProvider,
            ServingEngine.DefaultShutdownGracePeriod,
            ComposeConnectionLimits(commandLine));

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
