using System.Diagnostics.CodeAnalysis;

namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port client as the client of an upstream test case, the thing Phase 7 measures
/// beside pinned upstream curl (ADR-0089 decision 1(b)). Never the oracle: a case it fails that
/// upstream passes is a port finding, never a Surl change (ADR-0003 decision 6).
/// </summary>
/// <param name="runner">The runner of the client <see cref="CurlPortClientLocator"/> verified.</param>
public sealed class CurlPortTestCaseClient(CurlPortClientRunner runner) : IUpstreamTestCaseClient
{
    private readonly CurlPortClientRunner runner = runner ?? throw new ArgumentNullException(nameof(runner));

    /// <inheritdoc/>
    public string ExecutablePath => runner.Path;

    /// <inheritdoc/>
    public string Description => $"Curl port client {runner.Client.Commit} ({runner.Client.Sha256})";

    /// <inheritdoc/>
    // Excluded from coverage: it starts a process through the runner, which the fast tests never do;
    // the Integration tests in Surl.Conformance.UnitTests run it.
    [ExcludeFromCodeCoverage]
    public async Task<UpstreamTestCaseClientRun> RunAsync(
        IReadOnlyList<string> arguments,
        ReadOnlyMemory<byte> standardInput,
        IReadOnlyDictionary<string, string> environment,
        string workingDirectory,
        CancellationToken cancellationToken) =>
        UpstreamTestCaseClientRun.From(await runner.RunAsync(arguments, standardInput, environment, workingDirectory, cancellationToken));
}
