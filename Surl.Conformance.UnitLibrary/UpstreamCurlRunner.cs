using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Runs the pinned upstream curl build <see cref="UpstreamCurlLocator"/> found, with an
/// argument list, and returns its exit code, stdout bytes and stderr text - stopping it at a
/// timeout so a server that never answers cannot hang a conformance test.
/// </summary>
/// <remarks>
/// The runner takes an <see cref="UpstreamCurlLocation"/> rather than a path, so the only
/// curl it can start is one the locator verified against its pin (ADR-0003).
/// </remarks>
public sealed class UpstreamCurlRunner
{
    private readonly TimeSpan timeout;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes a new runner for the build <paramref name="location"/> names.
    /// </summary>
    /// <param name="location">What the locator found; it must hold a verified build.</param>
    /// <param name="timeout">How long one run may take before curl is stopped.</param>
    /// <param name="timeProvider">The clock the timeout runs on.</param>
    /// <exception cref="ArgumentException"><paramref name="location"/> holds no build.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is not positive.</exception>
    public UpstreamCurlRunner(UpstreamCurlLocation location, TimeSpan timeout, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        Build = location.Build ?? throw new ArgumentException(location.Message, nameof(location));
        this.timeout = timeout;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Gets the verified pinned build this runner starts.
    /// </summary>
    public PinnedUpstreamCurlBuild Build { get; }

    /// <summary>
    /// Runs the build with <paramref name="arguments"/>, stdin closed, until it exits or the
    /// timeout passes; at the timeout it kills curl and returns what curl had written.
    /// </summary>
    /// <param name="arguments">curl's arguments, each passed as one argument, unquoted.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>What the run produced.</returns>
    // Excluded from coverage: see the overload it calls.
    [ExcludeFromCodeCoverage]
    public Task<UpstreamCurlRunResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        RunAsync(arguments, ReadOnlyMemory<byte>.Empty, cancellationToken);

    /// <summary>
    /// Runs the build with <paramref name="arguments"/>, writes <paramref name="standardInput"/>
    /// to its stdin all at once and closes it, as <c>Record-CurlExchange.ps1</c> does, then waits
    /// until curl exits or the timeout passes; at the timeout it kills curl and returns what
    /// curl had written.
    /// </summary>
    /// <param name="arguments">curl's arguments, each passed as one argument, unquoted.</param>
    /// <param name="standardInput">The bytes curl reads on stdin, unchanged; empty for none.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>What the run produced.</returns>
    // Excluded from coverage: it starts a process, and the fast tests start none by design;
    // the start information and the result shapes it uses are fast-tested, and the
    // Integration tests in Surl.Conformance.UnitTests run it against surl.
    [ExcludeFromCodeCoverage]
    public async Task<UpstreamCurlRunResult> RunAsync(
        IReadOnlyList<string> arguments, ReadOnlyMemory<byte> standardInput, CancellationToken cancellationToken)
    {
        using var process = Process.Start(CreateStartInfo(arguments))
            ?? throw new InvalidOperationException($"{Build.DefaultPath} did not start.");
        await process.StandardInput.BaseStream.WriteAsync(standardInput, cancellationToken);
        process.StandardInput.Close();

        using var standardOutput = new MemoryStream();
        var copyingOutput = process.StandardOutput.BaseStream.CopyToAsync(standardOutput, CancellationToken.None);
        var readingError = process.StandardError.ReadToEndAsync(CancellationToken.None);

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
            return UpstreamCurlRunResult.StoppedAtTimeout(Build, standardOutput.ToArray(), errorSoFar);
        }

        await copyingOutput;
        var error = await readingError;
        return UpstreamCurlRunResult.Exited(Build, process.ExitCode, standardOutput.ToArray(), error);
    }

    /// <summary>
    /// Builds the start information for one run: the pinned build's path, each argument as
    /// its own <see cref="ProcessStartInfo.ArgumentList"/> entry, no shell, no window, and
    /// all three standard streams redirected.
    /// </summary>
    /// <param name="arguments">curl's arguments.</param>
    /// <returns>The start information.</returns>
    internal ProcessStartInfo CreateStartInfo(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo(Build.DefaultPath)
        {
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

        return startInfo;
    }
}
