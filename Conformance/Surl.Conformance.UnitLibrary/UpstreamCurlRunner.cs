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
    private static readonly IReadOnlyDictionary<string, string> NoEnvironmentChanges = new Dictionary<string, string>();

    private readonly TimeSpan timeout;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes a new runner for the build <paramref name="location"/> names.
    /// </summary>
    /// <param name="location">What the locator found; it must hold a verified build.</param>
    /// <param name="timeout">How long one run may take before curl is stopped.</param>
    /// <param name="timeProvider">The clock the timeout runs on.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="location"/> holds no build, holds a libcurl, which is loaded by a
    /// driver and never run as a curl, or holds a <see cref="UpstreamCurlBuildKind.Libtest"/>
    /// entry, a directory of libtest programs (<see cref="UpstreamCurlLocator.LocateLibtest"/>
    /// answers the binary to run as a <see cref="UpstreamCurlBuildKind.Curl"/> location).
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is not positive.</exception>
    public UpstreamCurlRunner(UpstreamCurlLocation location, TimeSpan timeout, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        Build = location.Build ?? throw new ArgumentException(location.Message, nameof(location));

        if (Build.Kind != UpstreamCurlBuildKind.Curl)
        {
            var pinnedAs = Build.Kind == UpstreamCurlBuildKind.Library
                ? "a pinned upstream libcurl"
                : "a pinned directory of upstream's libtest programs";
            throw new ArgumentException($"{Build.DefaultPath} is {pinnedAs}, not a curl to run.", nameof(location));
        }

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
    public Task<UpstreamCurlRunResult> RunAsync(
        IReadOnlyList<string> arguments, ReadOnlyMemory<byte> standardInput, CancellationToken cancellationToken) =>
        RunAsync(arguments, standardInput, NoEnvironmentChanges, cancellationToken);

    /// <summary>
    /// Runs the build as <see cref="RunAsync(IReadOnlyList{string}, ReadOnlyMemory{byte}, CancellationToken)"/>
    /// does, with each of <paramref name="environment"/>'s variables set in curl's environment -
    /// such as <c>HOME</c> and <c>USERPROFILE</c> pointed at a temporary directory, so curl reads
    /// none of the operator's own files (ADR-0051 decision 8).
    /// </summary>
    /// <param name="arguments">curl's arguments, each passed as one argument, unquoted.</param>
    /// <param name="standardInput">The bytes curl reads on stdin, unchanged; empty for none.</param>
    /// <param name="environment">The environment variables to set, by name; empty for none.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>What the run produced.</returns>
    // Excluded from coverage: it starts a process, and the fast tests start none by design;
    // the start information and the result shapes it uses are fast-tested, and the
    // Integration tests in Surl.Conformance.UnitTests run it against surl.
    [ExcludeFromCodeCoverage]
    public Task<UpstreamCurlRunResult> RunAsync(
        IReadOnlyList<string> arguments,
        ReadOnlyMemory<byte> standardInput,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken) =>
        RunAsync(arguments, standardInput, environment, string.Empty, cancellationToken);

    /// <summary>
    /// Runs the build as <see cref="RunAsync(IReadOnlyList{string}, ReadOnlyMemory{byte}, IReadOnlyDictionary{string, string}, CancellationToken)"/>
    /// does, in <paramref name="workingDirectory"/>: an upstream test case's relative paths
    /// (<c>%LOGDIR</c>, <c>-O</c>'s file) are relative to runtests' own folder.
    /// </summary>
    /// <param name="arguments">curl's arguments, each passed as one argument, unquoted.</param>
    /// <param name="standardInput">The bytes curl reads on stdin, unchanged; empty for none.</param>
    /// <param name="environment">The environment variables to set, by name; empty for none.</param>
    /// <param name="workingDirectory">curl's working directory; empty for this process's.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>What the run produced.</returns>
    // Excluded from coverage: it starts a process, and the fast tests start none by design;
    // the start information and the result shapes it uses are fast-tested, and the
    // Integration tests in Surl.Conformance.UnitTests run it against surl.
    [ExcludeFromCodeCoverage]
    public async Task<UpstreamCurlRunResult> RunAsync(
        IReadOnlyList<string> arguments,
        ReadOnlyMemory<byte> standardInput,
        IReadOnlyDictionary<string, string> environment,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        // Non-Windows curl gets stdin as a seekable file when there is any (BL-1143); Windows'
        // C runtime lseek does not fail on a pipe, so it keeps the pipe.
        var stdinFile = standardInput.IsEmpty || OperatingSystem.IsWindows()
            ? string.Empty
            : Path.Combine(Path.GetTempPath(), $"surl-curl-stdin-{Guid.NewGuid():N}");
        try
        {
            if (stdinFile.Length > 0)
            {
                await File.WriteAllBytesAsync(stdinFile, standardInput.ToArray(), cancellationToken);
            }

            return await RunStartedAsync(
                CreateStartInfo(arguments, environment, workingDirectory, stdinFile), standardInput, stdinFile.Length > 0, cancellationToken);
        }
        finally
        {
            if (stdinFile.Length > 0)
            {
                File.Delete(stdinFile);
            }
        }
    }

    // Excluded from coverage: see the overload that calls it.
    [ExcludeFromCodeCoverage]
    private async Task<UpstreamCurlRunResult> RunStartedAsync(
        ProcessStartInfo startInfo, ReadOnlyMemory<byte> standardInput, bool stdinIsFile, CancellationToken cancellationToken)
    {
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"{Build.DefaultPath} did not start.");

        // Both reads start before stdin is written, so curl never blocks on a full stdout or
        // stderr pipe while the runner blocks on a full stdin pipe.
        using var standardOutput = new MemoryStream();
        var copyingOutput = process.StandardOutput.BaseStream.CopyToAsync(standardOutput, CancellationToken.None);
        var readingError = process.StandardError.ReadToEndAsync(CancellationToken.None);
        if (!stdinIsFile)
        {
            await WriteStandardInputAsync(process.StandardInput, standardInput, cancellationToken);
        }

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
    /// Writes <paramref name="standardInput"/> to curl's stdin and closes it. An
    /// <see cref="IOException"/> from writing or closing means curl exited, or closed its stdin,
    /// without reading it all - <c>EPIPE</c> ("Broken pipe") on Linux and macOS, "The pipe is
    /// being closed" on Windows - and is ignored, so the run still reports curl's exit code,
    /// stdout and stderr. Any other exception propagates.
    /// </summary>
    /// <param name="stdin">curl's standard input stream; closed on return.</param>
    /// <param name="standardInput">The bytes to write; empty for none.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when stdin is written and closed, or curl stopped reading it.</returns>
    internal static async Task WriteStandardInputAsync(
        StreamWriter stdin, ReadOnlyMemory<byte> standardInput, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stdin);

        try
        {
            await stdin.BaseStream.WriteAsync(standardInput, cancellationToken);
            stdin.Close();
        }
        catch (IOException)
        {
            // curl did not read its stdin; closing what is left of the pipe is all that remains.
            CloseIgnoringBrokenPipe(stdin);
        }
    }

    private static void CloseIgnoringBrokenPipe(StreamWriter stdin)
    {
        try
        {
            stdin.Close();
        }
        catch (IOException)
        {
            // The pipe is already gone; there is nothing left to close.
        }
    }

    /// <summary>
    /// Builds the start information for one run: the pinned build's path, each argument as
    /// its own <see cref="ProcessStartInfo.ArgumentList"/> entry, no shell, no window, and
    /// all three standard streams redirected.
    /// </summary>
    /// <param name="arguments">curl's arguments.</param>
    /// <returns>The start information.</returns>
    internal ProcessStartInfo CreateStartInfo(IReadOnlyList<string> arguments) =>
        CreateStartInfo(arguments, NoEnvironmentChanges);

    /// <summary>
    /// Builds the start information for one run as <see cref="CreateStartInfo(IReadOnlyList{string})"/>
    /// does, with each of <paramref name="environment"/>'s variables set to its value in curl's
    /// environment, which otherwise is this process's.
    /// </summary>
    /// <param name="arguments">curl's arguments.</param>
    /// <param name="environment">The environment variables to set, by name.</param>
    /// <returns>The start information.</returns>
    internal ProcessStartInfo CreateStartInfo(IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment) =>
        CreateStartInfo(arguments, environment, string.Empty);

    /// <summary>
    /// Builds the start information for one run as
    /// <see cref="CreateStartInfo(IReadOnlyList{string}, IReadOnlyDictionary{string, string})"/> does,
    /// in <paramref name="workingDirectory"/>.
    /// </summary>
    /// <param name="arguments">curl's arguments.</param>
    /// <param name="environment">The environment variables to set, by name.</param>
    /// <param name="workingDirectory">curl's working directory; empty for this process's.</param>
    /// <returns>The start information.</returns>
    internal ProcessStartInfo CreateStartInfo(
        IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment, string workingDirectory) =>
        CreateStartInfo(arguments, environment, workingDirectory, string.Empty);

    /// <summary>
    /// Builds the start information as the overload without a file does. When
    /// <paramref name="standardInputFile"/> is not empty, curl's stdin is that file, not a pipe: curl's
    /// tool rewinds an upload read from stdin with <c>lseek</c> after a 401 or a redirect, and on
    /// Linux and macOS that fails on a pipe (curl exit 65), as upstream's runtests avoids by
    /// redirecting stdin from a file. .NET cannot hand a child a file as stdin, so the pinned build
    /// is started through <c>/bin/sh</c> running <c>exec</c> with the redirect; the arguments pass
    /// as positional parameters, never through the shell's parsing.
    /// </summary>
    /// <param name="arguments">curl's arguments.</param>
    /// <param name="environment">The environment variables to set, by name.</param>
    /// <param name="workingDirectory">curl's working directory; empty for this process's.</param>
    /// <param name="standardInputFile">The file curl reads as stdin; empty for a stdin pipe.</param>
    /// <returns>The start information.</returns>
    internal ProcessStartInfo CreateStartInfo(
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> environment,
        string workingDirectory,
        string standardInputFile)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(workingDirectory);
        ArgumentNullException.ThrowIfNull(standardInputFile);

        var fromFile = standardInputFile.Length > 0;
        var startInfo = new ProcessStartInfo(fromFile ? "/bin/sh" : Build.DefaultPath)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = !fromFile,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8,
        };

        if (fromFile)
        {
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("stdin_file=$1; shift; exec \"$0\" \"$@\" < \"$stdin_file\"");
            startInfo.ArgumentList.Add(Build.DefaultPath);
            startInfo.ArgumentList.Add(standardInputFile);
        }

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
