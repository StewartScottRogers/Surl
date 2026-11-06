namespace Surl.Conformance;

/// <summary>
/// One driver's run of a <see cref="CurlPortLibcurlCase"/>: how the driver exited, the report it printed
/// one line per call or step, and what its own <c>surl</c> shows.
/// </summary>
/// <param name="ExitCode">The driver's exit code: 0 once every step has run, 1 when the WebSocket driver's opening perform failed.</param>
/// <param name="Lines">The report's lines, in order.</param>
/// <param name="StandardError">What the driver wrote to standard error.</param>
/// <param name="Surl">What the run's own <c>surl</c> shows.</param>
public sealed record CurlPortLibcurlRun(int ExitCode, IReadOnlyList<string> Lines, string StandardError, CurlPortSurlSide Surl);
