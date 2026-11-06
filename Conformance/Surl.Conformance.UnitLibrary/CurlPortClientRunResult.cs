namespace Surl.Conformance;

/// <summary>
/// What one run of a pinned Curl port client produced: its exit code, the bytes it wrote to
/// stdout and the text it wrote to stderr, or that it was stopped at its timeout. It shows that
/// a real client completed an exchange; it is never an expected value (ADR-0083 decision 1).
/// </summary>
public sealed class CurlPortClientRunResult
{
    private CurlPortClientRunResult(PinnedCurlPortClient client, int? exitCode, byte[] standardOutput, string standardError)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        Client = client;
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    /// <summary>
    /// Gets the pinned client that ran.
    /// </summary>
    public PinnedCurlPortClient Client { get; }

    /// <summary>
    /// Gets the client's exit code, or <see langword="null"/> when the run was stopped at its timeout.
    /// </summary>
    public int? ExitCode { get; }

    /// <summary>
    /// Gets a value indicating whether the run was stopped at its timeout rather than exiting.
    /// </summary>
    public bool TimedOut => ExitCode is null;

    /// <summary>
    /// Gets the bytes the client wrote to stdout, unchanged.
    /// </summary>
    public byte[] StandardOutput { get; }

    /// <summary>
    /// Gets the text the client wrote to stderr.
    /// </summary>
    public string StandardError { get; }

    /// <summary>
    /// Creates the result of a run that exited by itself.
    /// </summary>
    /// <param name="client">The pinned client that ran.</param>
    /// <param name="exitCode">The client's exit code.</param>
    /// <param name="standardOutput">The bytes the client wrote to stdout.</param>
    /// <param name="standardError">The text the client wrote to stderr.</param>
    /// <returns>An exited result.</returns>
    public static CurlPortClientRunResult Exited(
        PinnedCurlPortClient client, int exitCode, byte[] standardOutput, string standardError) =>
        new(client, exitCode, standardOutput, standardError);

    /// <summary>
    /// Creates the result of a run stopped at its timeout, keeping whatever the client had written.
    /// </summary>
    /// <param name="client">The pinned client that ran.</param>
    /// <param name="standardOutput">The bytes the client wrote to stdout before it was stopped.</param>
    /// <param name="standardError">The text the client wrote to stderr before it was stopped.</param>
    /// <returns>A timed-out result, with no exit code.</returns>
    public static CurlPortClientRunResult StoppedAtTimeout(
        PinnedCurlPortClient client, byte[] standardOutput, string standardError) =>
        new(client, null, standardOutput, standardError);

    /// <summary>
    /// Describes the result in one line for a test's output: the client's commit and SHA-256,
    /// then the exit code or the timeout.
    /// </summary>
    /// <returns>The description.</returns>
    public override string ToString() =>
        $"Curl port client {Client.Commit} ({Client.Sha256}) {(TimedOut ? "was stopped at its timeout" : $"exited {ExitCode}")}";
}
