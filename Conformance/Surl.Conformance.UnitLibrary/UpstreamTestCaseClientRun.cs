namespace Surl.Conformance;

/// <summary>
/// What one client run of an upstream test case produced, whichever client ran it: its exit code,
/// the bytes it wrote to stdout and the text it wrote to stderr, or that it was stopped at its
/// timeout. A pinned upstream build's run and the Curl port's run take this one shape, so the same
/// case is checked the same way for both (ADR-0089 decision 1(b)); which client ran stays in the
/// <see cref="IUpstreamTestCaseClient"/> that made it.
/// </summary>
/// <param name="ExitCode">The client's exit code, or <see langword="null"/> when it was stopped at its timeout.</param>
/// <param name="StandardOutput">The bytes the client wrote to stdout, unchanged.</param>
/// <param name="StandardError">The text the client wrote to stderr.</param>
public sealed record UpstreamTestCaseClientRun(int? ExitCode, byte[] StandardOutput, string StandardError)
{
    /// <summary>
    /// Gets a value indicating whether the run was stopped at its timeout rather than exiting.
    /// </summary>
    public bool TimedOut => ExitCode is null;

    /// <summary>
    /// Takes the run of a pinned upstream curl build.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <returns>The run in the shared shape.</returns>
    public static UpstreamTestCaseClientRun From(UpstreamCurlRunResult run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return new UpstreamTestCaseClientRun(run.ExitCode, run.StandardOutput, run.StandardError);
    }

    /// <summary>
    /// Takes the run of the pinned Curl port client.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <returns>The run in the shared shape.</returns>
    public static UpstreamTestCaseClientRun From(CurlPortClientRunResult run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return new UpstreamTestCaseClientRun(run.ExitCode, run.StandardOutput, run.StandardError);
    }
}
