using System.Text;

namespace Surl.Conformance;

/// <summary>
/// What one run of a test case produced, as its <c>&lt;verify&gt;</c> section is checked against
/// it: curl's exit code, stdout and stderr, the file runtests' <c>--output</c> names, and the bytes
/// the server half recorded receiving (ADR-0080 decision 2).
/// </summary>
/// <param name="ExitCode">curl's exit code, or <see langword="null"/> when it was stopped at its timeout.</param>
/// <param name="StandardOutput">The bytes curl wrote to stdout.</param>
/// <param name="StandardError">The bytes curl wrote to stderr.</param>
/// <param name="OutputFile">
/// The bytes of the file runtests' <c>--output</c> names (<c>%LOGDIR/curl&lt;N&gt;.out</c>), which
/// the reply's <c>&lt;data&gt;</c> or <c>&lt;datacheck&gt;</c> is checked against; empty when curl
/// wrote no such file, as upstream's <c>loadarray</c> reads a missing one.
/// </param>
/// <param name="ServerReceived">The bytes the server half received, in arrival order, for <c>&lt;protocol&gt;</c>.</param>
/// <param name="UploadReceived">The upload the server half stored, for <c>&lt;upload&gt;</c>.</param>
/// <param name="ProxyReceived">The bytes an HTTP proxy received, for <c>&lt;proxy&gt;</c>; empty while no proxy is built.</param>
public sealed record UpstreamTestCaseRunRecord(
    int? ExitCode,
    byte[] StandardOutput,
    byte[] StandardError,
    byte[] OutputFile,
    byte[] ServerReceived,
    byte[] UploadReceived,
    byte[] ProxyReceived)
{
    /// <summary>
    /// The DNS server half's query log (<c>dnsd.input</c>), one <c>QNAME &lt;name&gt; QTYPE &lt;type&gt;</c>
    /// line per query, for <c>&lt;verify&gt;&lt;dns&gt;</c> (ADR-0123 decision 1); empty when no DNS server half ran.
    /// </summary>
    public byte[] DnsQueryLog { get; init; } = [];

    /// <summary>
    /// Makes the record of a run of a pinned upstream curl build and what its server half recorded.
    /// </summary>
    /// <param name="run">The run; its stderr text is turned back into UTF-8 bytes, as the runner decoded it.</param>
    /// <param name="outputFile">The bytes of the <c>--output</c> file, empty when there is none.</param>
    /// <param name="serverReceived">The bytes the server half received.</param>
    /// <param name="uploadReceived">The upload the server half stored.</param>
    /// <returns>The record, with no proxy bytes.</returns>
    public static UpstreamTestCaseRunRecord FromRun(
        UpstreamCurlRunResult run, byte[] outputFile, byte[] serverReceived, byte[] uploadReceived) =>
        FromRun(UpstreamTestCaseClientRun.From(run), outputFile, serverReceived, uploadReceived);

    /// <summary>
    /// Makes the record of a run of whichever client played the case - a pinned upstream build or the
    /// pinned Curl port (<see cref="IUpstreamTestCaseClient"/>) - and what its server half recorded.
    /// </summary>
    /// <param name="run">The run; its stderr text is turned back into UTF-8 bytes, as the runner decoded it.</param>
    /// <param name="outputFile">The bytes of the <c>--output</c> file, empty when there is none.</param>
    /// <param name="serverReceived">The bytes the server half received.</param>
    /// <param name="uploadReceived">The upload the server half stored.</param>
    /// <returns>The record, with no proxy bytes.</returns>
    public static UpstreamTestCaseRunRecord FromRun(
        UpstreamTestCaseClientRun run, byte[] outputFile, byte[] serverReceived, byte[] uploadReceived)
    {
        ArgumentNullException.ThrowIfNull(run);

        return new UpstreamTestCaseRunRecord(
            run.ExitCode,
            run.StandardOutput,
            Encoding.UTF8.GetBytes(run.StandardError),
            outputFile,
            serverReceived,
            uploadReceived,
            []);
    }
}
