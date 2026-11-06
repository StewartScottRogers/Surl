using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Runs the pinned Curl port client <see cref="CurlPortClientLocator"/> found, with an argument
/// list, and returns its exit code, stdout bytes and stderr text - stopping it at a timeout so a
/// server that never answers cannot hang an extended server's integration test (ADR-0083).
/// </summary>
/// <remarks>
/// The runner takes a <see cref="CurlPortClientLocation"/> rather than a path, so the only
/// client it can start is one the locator verified against <c>CurlPortBuilds.json</c>. It is a
/// separate type from <see cref="UpstreamCurlRunner"/>, which takes only an upstream curl.
/// </remarks>
public sealed class CurlPortClientRunner
{
    private readonly TimeSpan timeout;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes a new runner for the client <paramref name="location"/> names.
    /// </summary>
    /// <param name="location">What the locator found; it must hold a verified client.</param>
    /// <param name="timeout">How long one run may take before the client is stopped.</param>
    /// <param name="timeProvider">The clock the timeout runs on.</param>
    /// <exception cref="ArgumentException"><paramref name="location"/> holds no client, or holds the port's library, which is never run.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is not positive.</exception>
    public CurlPortClientRunner(CurlPortClientLocation location, TimeSpan timeout, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        Client = location.Client ?? throw new ArgumentException(location.Message, nameof(location));
        if (Client.Kind != CurlPortClientKind.Curl)
        {
            throw new ArgumentException($"{location.Path} is the Curl port's pinned library; it is called by a libcurl driver, never run as a curl.", nameof(location));
        }

        Path = location.Path!;
        this.timeout = timeout;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Gets the verified pinned client this runner starts.
    /// </summary>
    public PinnedCurlPortClient Client { get; }

    /// <summary>
    /// Gets the verified client's path: its pin's path with <c>%NAME%</c> variables expanded.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Runs the client with <paramref name="arguments"/>, writes <paramref name="standardInput"/>
    /// to its stdin all at once and closes it, sets each of <paramref name="environment"/>'s
    /// variables in its environment, then waits until it exits or the timeout passes; at the
    /// timeout it kills the client and returns what it had written.
    /// </summary>
    /// <param name="arguments">The client's arguments, each passed as one argument, unquoted.</param>
    /// <param name="standardInput">The bytes the client reads on stdin, unchanged; empty for none.</param>
    /// <param name="environment">The environment variables to set, by name; empty for none.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>What the run produced.</returns>
    // Excluded from coverage: see the overload it calls.
    [ExcludeFromCodeCoverage]
    public Task<CurlPortClientRunResult> RunAsync(
        IReadOnlyList<string> arguments,
        ReadOnlyMemory<byte> standardInput,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken) =>
        RunAsync(arguments, standardInput, environment, string.Empty, cancellationToken);

    /// <summary>
    /// Runs the client as <see cref="RunAsync(IReadOnlyList{string}, ReadOnlyMemory{byte}, IReadOnlyDictionary{string, string}, CancellationToken)"/>
    /// does, in <paramref name="workingDirectory"/>, so an upstream test case's relative paths are
    /// relative to its own folder (ADR-0089 decision 1(b)). stdout and stderr are read before stdin is
    /// written, and a client that exits without reading its stdin is not an error, as
    /// <see cref="UpstreamCurlRunner"/> runs upstream curl.
    /// </summary>
    /// <param name="arguments">The client's arguments, each passed as one argument, unquoted.</param>
    /// <param name="standardInput">The bytes the client reads on stdin, unchanged; empty for none.</param>
    /// <param name="environment">The environment variables to set, by name; empty for none.</param>
    /// <param name="workingDirectory">The client's working directory; empty for this process's.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>What the run produced.</returns>
    // Excluded from coverage: it starts a process, and the fast tests start none by design;
    // the start information and the result shapes it uses are fast-tested, and the
    // Integration tests in Surl.Conformance.UnitTests run it against surl.
    [ExcludeFromCodeCoverage]
    public async Task<CurlPortClientRunResult> RunAsync(
        IReadOnlyList<string> arguments,
        ReadOnlyMemory<byte> standardInput,
        IReadOnlyDictionary<string, string> environment,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        using var process = Process.Start(CreateStartInfo(arguments, environment, workingDirectory))
            ?? throw new InvalidOperationException($"{Path} did not start.");

        using var standardOutput = new MemoryStream();
        var copyingOutput = process.StandardOutput.BaseStream.CopyToAsync(standardOutput, CancellationToken.None);
        var readingError = process.StandardError.ReadToEndAsync(CancellationToken.None);
        await UpstreamCurlRunner.WriteStandardInputAsync(process.StandardInput, standardInput, cancellationToken);

        using var deadline = new CancellationTokenSource(timeout, timeProvider);
        using var waitEnds = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            await process.WaitForExitAsync(waitEnds.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            await copyingOutput;
            var errorSoFar = await readingError;
            cancellationToken.ThrowIfCancellationRequested();
            return CurlPortClientRunResult.StoppedAtTimeout(Client, standardOutput.ToArray(), errorSoFar);
        }

        await copyingOutput;
        var error = await readingError;
        return CurlPortClientRunResult.Exited(Client, process.ExitCode, standardOutput.ToArray(), error);
    }

    /// <summary>
    /// Builds the start information for one run: the verified client's expanded path, each
    /// argument as its own <see cref="ProcessStartInfo.ArgumentList"/> entry, each of
    /// <paramref name="environment"/>'s variables set, no shell, no window, and all three
    /// standard streams redirected.
    /// </summary>
    /// <param name="arguments">The client's arguments.</param>
    /// <param name="environment">The environment variables to set, by name.</param>
    /// <returns>The start information.</returns>
    internal ProcessStartInfo CreateStartInfo(IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment) =>
        CreateStartInfo(arguments, environment, string.Empty);

    /// <summary>
    /// Builds the start information for one run as
    /// <see cref="CreateStartInfo(IReadOnlyList{string}, IReadOnlyDictionary{string, string})"/> does,
    /// in <paramref name="workingDirectory"/>.
    /// </summary>
    /// <param name="arguments">The client's arguments.</param>
    /// <param name="environment">The environment variables to set, by name.</param>
    /// <param name="workingDirectory">The client's working directory; empty for this process's.</param>
    /// <returns>The start information.</returns>
    internal ProcessStartInfo CreateStartInfo(
        IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(workingDirectory);

        var startInfo = new ProcessStartInfo(Path)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument ?? throw new ArgumentException("An argument is null.", nameof(arguments)));
        }

        foreach (var (name, value) in environment)
        {
            startInfo.Environment[name] = value;
        }

        return startInfo;
    }
}
