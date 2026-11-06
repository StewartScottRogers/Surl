using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// Judges one Phase 7 libcurl row (ADR-0089 decision 1 (c)) from upstream's libcurl driver's run and the
/// Curl port's twin's, as <see cref="CurlPortComparison"/> judges a curl case (decisions 4 to 6): whether
/// upstream's report is what the mirrored test asserts, then the driver's exit code, its report line by
/// line, <c>surl</c>'s notes and the stored files the row names, after <see cref="CurlPortLibcurlReportNormaliser"/>, then the verdict.
/// </summary>
public static class CurlPortLibcurlComparison
{
    /// <summary>
    /// Compares the port's run of a libcurl row with upstream's and gives the verdict.
    /// </summary>
    /// <param name="libcurlCase">The row both drivers ran.</param>
    /// <param name="upstream">What upstream's driver produced, calling the pinned upstream libcurl.</param>
    /// <param name="upstreamLibrary">The upstream libcurl pin the driver called, named in a finding.</param>
    /// <param name="port">What the port's twin produced, calling the pinned port <c>Libcurl</c>.</param>
    /// <param name="portLibrary">The port library pin the twin called, named in a finding.</param>
    /// <param name="surlCommit">The Surl commit measured, named in a finding.</param>
    /// <returns>The verdict.</returns>
    public static CurlPortVerdict Compare(
        CurlPortLibcurlCase libcurlCase,
        CurlPortLibcurlRun upstream,
        PinnedUpstreamCurlBuild upstreamLibrary,
        CurlPortLibcurlRun port,
        PinnedCurlPortClient portLibrary,
        string surlCommit)
    {
        ArgumentNullException.ThrowIfNull(libcurlCase);
        ArgumentNullException.ThrowIfNull(upstream);
        ArgumentNullException.ThrowIfNull(upstreamLibrary);
        ArgumentNullException.ThrowIfNull(port);
        ArgumentNullException.ThrowIfNull(portLibrary);
        ArgumentException.ThrowIfNullOrWhiteSpace(surlCommit);

        var surlDefect = upstream.ExitCode == 0
            ? libcurlCase.CheckMirroredAssertions(upstream)
            : $"upstream's driver exited {upstream.ExitCode}: {upstream.StandardError}";
        if (surlDefect is not null)
        {
            return CurlPortVerdict.SurlDefect(surlDefect);
        }

        var differences = FindDifferences(libcurlCase, upstream, port);
        return differences.Count == 0
            ? CurlPortVerdict.Agreement()
            : CurlPortVerdict.PortDefect(new CurlPortFinding(
                libcurlCase.Name,
                libcurlCase.Mirrors,
                libcurlCase.CommandLine,
                upstreamLibrary,
                portLibrary,
                surlCommit,
                AsMeasuredRun(upstream),
                AsMeasuredRun(port),
                differences));
    }

    /// <summary>
    /// Normalises a run's <c>surl</c> notes as <see cref="CurlPortResultNormaliser.NormaliseSurlNotes"/>
    /// does, and every 16-hex-digit session ID <c>surl</c> drew as <see cref="CurlPortLibcurlReportNormaliser.SessionPlaceholder"/>.
    /// </summary>
    /// <param name="surl">What the run's <c>surl</c> shows.</param>
    /// <returns>The normalised notes.</returns>
    public static string NormaliseNotes(CurlPortSurlSide surl)
    {
        ArgumentNullException.ThrowIfNull(surl);
        return SessionId.Replace(CurlPortResultNormaliser.NormaliseSurlNotes(surl.Notes, surl.Port), CurlPortLibcurlReportNormaliser.SessionPlaceholder);
    }

    private static List<CurlPortDifference> FindDifferences(CurlPortLibcurlCase libcurlCase, CurlPortLibcurlRun upstream, CurlPortLibcurlRun port)
    {
        var differences = new List<CurlPortDifference>();
        if (upstream.ExitCode != port.ExitCode)
        {
            differences.Add(new("driver exit code", upstream.ExitCode.ToString(CultureInfo.InvariantCulture), port.ExitCode.ToString(CultureInfo.InvariantCulture)));
        }

        CurlPortComparison.AddTextDifference(differences, "driver report", NormaliseReport(upstream), NormaliseReport(port));
        CurlPortComparison.AddTextDifference(differences, "surl notes", NormaliseNotes(upstream.Surl), NormaliseNotes(port.Surl));
        foreach (var name in libcurlCase.ComparedStoredFiles)
        {
            CurlPortComparison.AddBytesDifference(differences, $"stored file {name}", upstream.Surl.StoredFiles.GetValueOrDefault(name), port.Surl.StoredFiles.GetValueOrDefault(name));
        }
        return differences;
    }

    private static string NormaliseReport(CurlPortLibcurlRun run) =>
        string.Join('\n', run.Lines.Select(line => CurlPortLibcurlReportNormaliser.NormaliseLine(line, run.Surl.Port)));

    private static CurlPortMeasuredRun AsMeasuredRun(CurlPortLibcurlRun run) =>
        new(run.ExitCode, Encoding.UTF8.GetBytes(string.Join('\n', run.Lines)), run.StandardError, run.Surl);

    private static readonly Regex SessionId = new(@"\b[0-9A-F]{16}\b", RegexOptions.CultureInvariant);
}
