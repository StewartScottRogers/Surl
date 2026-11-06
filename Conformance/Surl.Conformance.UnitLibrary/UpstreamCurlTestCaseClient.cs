using System.Diagnostics.CodeAnalysis;

namespace Surl.Conformance;

/// <summary>
/// A pinned upstream curl build as the client of an upstream test case: the oracle (ADR-0003).
/// </summary>
/// <param name="runner">The runner of the build <see cref="UpstreamCurlLocator"/> verified.</param>
public sealed class UpstreamCurlTestCaseClient(UpstreamCurlRunner runner) : IUpstreamTestCaseClient
{
    private readonly UpstreamCurlRunner runner = runner ?? throw new ArgumentNullException(nameof(runner));

    /// <inheritdoc/>
    public string ExecutablePath => runner.Build.DefaultPath;

    /// <inheritdoc/>
    public string Description => $"upstream curl {runner.Build.Sha256} ({runner.Build.Version})";

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
