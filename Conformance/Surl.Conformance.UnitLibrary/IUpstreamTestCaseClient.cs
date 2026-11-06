namespace Surl.Conformance;

/// <summary>
/// The client that runs an upstream test case's command line against Surl's server halves: a pinned
/// upstream curl build (<see cref="UpstreamCurlTestCaseClient"/>), the oracle, or the pinned Curl port
/// (<see cref="CurlPortTestCaseClient"/>), the thing Phase 7 measures beside it (ADR-0089 decision
/// 1(b)). Each implementation takes only its own verified location, so an upstream run never starts the
/// port's binary and a port run never starts an upstream build (ADR-0083 decision 2).
/// </summary>
public interface IUpstreamTestCaseClient
{
    /// <summary>
    /// Gets the path of the executable the client runs, which a case's <c>%CURL</c> names.
    /// </summary>
    string ExecutablePath { get; }

    /// <summary>
    /// Gets a one-line description of the client for a test's log: which client, and its pin.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Runs the client once with a case's command line, until it exits or its timeout passes.
    /// </summary>
    /// <param name="arguments">The arguments, each passed as one argument, unquoted.</param>
    /// <param name="standardInput">The bytes the client reads on stdin; empty for none.</param>
    /// <param name="environment">The environment variables to set, by name.</param>
    /// <param name="workingDirectory">The client's working directory, the case's folder.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>What the run produced.</returns>
    Task<UpstreamTestCaseClientRun> RunAsync(
        IReadOnlyList<string> arguments,
        ReadOnlyMemory<byte> standardInput,
        IReadOnlyDictionary<string, string> environment,
        string workingDirectory,
        CancellationToken cancellationToken);
}
