namespace Surl.Conformance;

/// <summary>
/// What one run of a pinned upstream curl build produced: its exit code, the bytes it wrote
/// to stdout and the text it wrote to stderr, or that it was stopped at its timeout.
/// </summary>
public sealed class UpstreamCurlRunResult
{
    private UpstreamCurlRunResult(PinnedUpstreamCurlBuild build, int? exitCode, byte[] standardOutput, string standardError)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        Build = build;
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    /// <summary>
    /// Gets the pinned build that ran.
    /// </summary>
    public PinnedUpstreamCurlBuild Build { get; }

    /// <summary>
    /// Gets curl's exit code, or <see langword="null"/> when the run was stopped at its timeout.
    /// </summary>
    public int? ExitCode { get; }

    /// <summary>
    /// Gets a value indicating whether the run was stopped at its timeout rather than exiting.
    /// </summary>
    public bool TimedOut => ExitCode is null;

    /// <summary>
    /// Gets the bytes curl wrote to stdout, unchanged.
    /// </summary>
    public byte[] StandardOutput { get; }

    /// <summary>
    /// Gets the text curl wrote to stderr.
    /// </summary>
    public string StandardError { get; }

    /// <summary>
    /// Creates the result of a run that exited by itself.
    /// </summary>
    /// <param name="build">The pinned build that ran.</param>
    /// <param name="exitCode">curl's exit code.</param>
    /// <param name="standardOutput">The bytes curl wrote to stdout.</param>
    /// <param name="standardError">The text curl wrote to stderr.</param>
    /// <returns>An exited result.</returns>
    public static UpstreamCurlRunResult Exited(
        PinnedUpstreamCurlBuild build, int exitCode, byte[] standardOutput, string standardError) =>
        new(build, exitCode, standardOutput, standardError);

    /// <summary>
    /// Creates the result of a run stopped at its timeout, keeping whatever curl had written.
    /// </summary>
    /// <param name="build">The pinned build that ran.</param>
    /// <param name="standardOutput">The bytes curl wrote to stdout before it was stopped.</param>
    /// <param name="standardError">The text curl wrote to stderr before it was stopped.</param>
    /// <returns>A timed-out result, with no exit code.</returns>
    public static UpstreamCurlRunResult StoppedAtTimeout(
        PinnedUpstreamCurlBuild build, byte[] standardOutput, string standardError) =>
        new(build, null, standardOutput, standardError);

    /// <summary>
    /// Describes the result in one line for a test's output: the build's SHA-256, then the
    /// exit code or the timeout.
    /// </summary>
    /// <returns>The description.</returns>
    public override string ToString() =>
        $"upstream curl {Build.Sha256} {(TimedOut ? "was stopped at its timeout" : $"exited {ExitCode}")}";
}
