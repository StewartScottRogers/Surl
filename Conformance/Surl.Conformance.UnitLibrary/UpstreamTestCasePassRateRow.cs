namespace Surl.Conformance;

/// <summary>
/// One row of the pass-rate file's current or releases table (ADR-0080 decision 5), as far as the
/// guard reads it.
/// </summary>
/// <param name="Version">The release's version in the releases table; <see langword="null"/> in the current table.</param>
/// <param name="Platform">The pin's platform, such as <c>win-x64</c>.</param>
/// <param name="Passed">The number of cases that passed; <see langword="null"/> when the row says <c>not run</c>.</param>
/// <param name="TestServerChoice">
/// The number of cases skipped as <see cref="UpstreamTestCaseSkipReasons.TestServerChoice"/>;
/// <see langword="null"/> when the row says <c>not run</c> or the table has no such column.
/// </param>
public sealed record UpstreamTestCasePassRateRow(string? Version, string Platform, int? Passed, int? TestServerChoice = null);
