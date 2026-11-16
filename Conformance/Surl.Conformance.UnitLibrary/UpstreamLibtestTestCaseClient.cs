using System.Diagnostics.CodeAnalysis;

namespace Surl.Conformance;

/// <summary>
/// A pinned libtest program as the client of an upstream test case (ADR-0174 decision 3): the
/// multi-program binary run as <c>libtests &lt;program&gt; &lt;arguments&gt;</c> where the case would
/// run <c>curl &lt;arguments&gt;</c>.
/// </summary>
/// <param name="runner">The runner of the <c>libtests</c> binary <see cref="UpstreamCurlLocator.LocateLibtest"/> verified.</param>
/// <param name="programName">The program the case runs, such as <c>lib2402</c>.</param>
public sealed class UpstreamLibtestTestCaseClient(UpstreamCurlRunner runner, string programName) : IUpstreamTestCaseClient
{
    private readonly UpstreamCurlRunner runner = runner ?? throw new ArgumentNullException(nameof(runner));
    private readonly string programName = string.IsNullOrWhiteSpace(programName) ? throw new ArgumentException("A libtest program needs a name.", nameof(programName)) : programName;

    /// <inheritdoc/>
    public string ExecutablePath => runner.Build.DefaultPath;

    /// <inheritdoc/>
    public string Description => $"upstream libtest {programName} from {runner.Build.Sha256}";

    /// <summary>
    /// Puts the program's name in front of the case's arguments.
    /// </summary>
    /// <param name="arguments">The case's command arguments.</param>
    /// <returns>The arguments <c>libtests</c> is started with.</returns>
    public IReadOnlyList<string> ArgumentsFor(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return [programName, .. arguments];
    }

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
        UpstreamTestCaseClientRun.From(await runner.RunAsync(ArgumentsFor(arguments), standardInput, environment, workingDirectory, cancellationToken));
}
