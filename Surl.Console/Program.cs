using System.Runtime.InteropServices;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// Entry point of the <c>surl</c> executable.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Runs Surl with the command line it was given, on the process's stdout and stderr, until
    /// Ctrl+C (SIGINT) or SIGTERM stops it.
    /// </summary>
    /// <param name="args">The command-line arguments, without the program name.</param>
    /// <returns>The <see cref="Protocol.Abstractions.SurlExitCode"/> <see cref="RunAsync"/> returns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is null.</exception>
    internal static Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return RunUntilSignalledAsync(args);
    }

    private static async Task<int> RunUntilSignalledAsync(string[] args)
    {
        using var stop = new CancellationTokenSource();
        var stopOnSignal = CreateStopOnSignal(stop);
        using var interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, stopOnSignal);
        using var terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, stopOnSignal);

        return await RunAsync(args, System.Console.Out, System.Console.Error, stop.Token);
    }

    /// <summary>
    /// Runs Surl with <paramref name="args"/>: the entry point <see cref="Main"/> and the
    /// in-process conformance tests share. It serves over real TCP and UDP listeners
    /// (<see cref="SocketListenerFactory"/>, securing connections with the process's TLS
    /// settings) with the system clock.
    /// </summary>
    /// <param name="args">The command-line arguments, without the program name.</param>
    /// <param name="output">Where the help, the version, the status lines and a <c>-</c> trace or log file go.</param>
    /// <param name="error">Where every <c>surl: </c> message and, without <c>--log-file</c>, the log go.</param>
    /// <param name="cancellationToken">Cancelled to stop serving.</param>
    /// <returns>The exit code, as ADR-0007 section 5 gives it.</returns>
    internal static async Task<int> RunAsync(
        string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken) =>
        (int)await new CommandLineRunner(
                CreateListenerFactory, DataDirectoryProbe.CanOpen, DataDirectoryLock.Take, TimeProvider.System)
            .RunAsync(args, output, error, cancellationToken);

    /// <summary>
    /// Creates the socket-backed listener factory, securing connections with
    /// <paramref name="tlsSettings"/>.
    /// </summary>
    /// <param name="tlsSettings">The process's TLS settings, or <see langword="null"/> for none.</param>
    /// <returns>The factory.</returns>
    internal static IListenerFactory CreateListenerFactory(ServerTlsSettings? tlsSettings) =>
        new SocketListenerFactory(tlsSettings);

    /// <summary>
    /// Creates the Ctrl+C and SIGTERM handler: it keeps the runtime from ending the process at
    /// once, and cancels <paramref name="stop"/> so serving winds down and <see cref="Main"/>
    /// returns.
    /// </summary>
    /// <param name="stop">The source whose token stops serving.</param>
    /// <returns>The handler, which cancels the signal's default handling.</returns>
    internal static Action<PosixSignalContext> CreateStopOnSignal(CancellationTokenSource stop) =>
        context =>
        {
            context.Cancel = true;
            stop.Cancel();
        };
}
