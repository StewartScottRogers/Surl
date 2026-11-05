using System.Globalization;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Judges one Phase 7 case from its two runs (ADR-0089 decisions 4 to 6): whether upstream's run is
/// what the mirrored test asserts, then every compared item after
/// <see cref="CurlPortResultNormaliser"/>, then the verdict. It takes the two runners' results, never
/// a path, so neither runner can be handed the other's binary (ADR-0083 decision 2).
/// </summary>
public static class CurlPortComparison
{
    private const string ReferenceRelease = "curl 8.21.0 ";

    /// <summary>
    /// Compares the Curl port's run of a case with pinned upstream curl's and gives the verdict.
    /// </summary>
    /// <param name="measurementCase">The case both clients ran.</param>
    /// <param name="upstream">What the pinned upstream build produced.</param>
    /// <param name="upstreamSurl">What upstream's own <c>surl</c> shows.</param>
    /// <param name="port">What the pinned Curl port client produced.</param>
    /// <param name="portSurl">What the port's own <c>surl</c> shows.</param>
    /// <param name="surlCommit">The Surl commit measured, named in a finding.</param>
    /// <returns>The verdict.</returns>
    public static CurlPortVerdict Compare(
        CurlPortMeasurementCase measurementCase,
        UpstreamCurlRunResult upstream,
        CurlPortSurlSide upstreamSurl,
        CurlPortClientRunResult port,
        CurlPortSurlSide portSurl,
        string surlCommit)
    {
        ArgumentNullException.ThrowIfNull(measurementCase);
        ArgumentException.ThrowIfNullOrWhiteSpace(surlCommit);
        var upstreamRun = CurlPortMeasuredRun.Of(upstream, upstreamSurl);
        var portRun = CurlPortMeasuredRun.Of(port, portSurl);

        var surlDefect = measurementCase.CheckMirroredAssertions(upstreamRun);
        if (surlDefect is not null)
        {
            return CurlPortVerdict.SurlDefect(surlDefect);
        }

        var differences = FindDifferences(measurementCase, upstreamRun, portRun);
        if (differences.Count == 0)
        {
            return CurlPortVerdict.Agreement();
        }

        if (measurementCase.ReleaseDifference is not null && !upstream.Build.Version.StartsWith(ReferenceRelease, StringComparison.Ordinal))
        {
            return CurlPortVerdict.NotComparable(
                $"release difference: {upstream.Build.Version}: {measurementCase.ReleaseDifference}");
        }

        return CurlPortVerdict.PortDefect(new CurlPortFinding(
            measurementCase.Name,
            measurementCase.Mirrors,
            measurementCase.CommandLine,
            upstream.Build,
            port.Client,
            surlCommit,
            upstreamRun,
            portRun,
            differences));
    }

    /// <summary>
    /// Every compared item on which the two runs differ after normalisation, in ADR-0089 decision 4's
    /// order.
    /// </summary>
    internal static IReadOnlyList<CurlPortDifference> FindDifferences(
        CurlPortMeasurementCase measurementCase, CurlPortMeasuredRun upstream, CurlPortMeasuredRun port)
    {
        var differences = new List<CurlPortDifference>();
        if (upstream.ExitCode != port.ExitCode)
        {
            differences.Add(new("exit code", FormatExitCode(upstream.ExitCode), FormatExitCode(port.ExitCode)));
        }

        var arguments = measurementCase.Arguments;
        AddTextDifference(
            differences,
            "standard output",
            Encoding.Latin1.GetString(CurlPortResultNormaliser.NormaliseOutput(upstream.StandardOutput, upstream.Surl.Port, arguments)),
            Encoding.Latin1.GetString(CurlPortResultNormaliser.NormaliseOutput(port.StandardOutput, port.Surl.Port, arguments)));
        AddTextDifference(
            differences,
            "standard error",
            CurlPortResultNormaliser.NormaliseStandardError(upstream.StandardError, upstream.Surl.Port, arguments, measurementCase.AssertedStandardErrorFragments),
            CurlPortResultNormaliser.NormaliseStandardError(port.StandardError, port.Surl.Port, arguments, measurementCase.AssertedStandardErrorFragments));
        AddTextDifference(
            differences,
            "surl notes",
            CurlPortResultNormaliser.NormaliseSurlNotes(upstream.Surl.Notes, upstream.Surl.Port),
            CurlPortResultNormaliser.NormaliseSurlNotes(port.Surl.Notes, port.Surl.Port));
        AddRequestBytesDifference(differences, upstream.Surl, port.Surl);
        foreach (var name in measurementCase.ComparedStoredFiles)
        {
            AddBytesDifference(differences, $"stored file {name}", upstream.Surl.StoredFiles.GetValueOrDefault(name), port.Surl.StoredFiles.GetValueOrDefault(name));
        }

        return differences;
    }

    private static string FormatExitCode(int? exitCode) =>
        exitCode?.ToString(CultureInfo.InvariantCulture) ?? "stopped at its timeout";

    private static void AddTextDifference(List<CurlPortDifference> differences, string item, string upstream, string port)
    {
        if (upstream == port)
        {
            return;
        }

        var upstreamLines = upstream.Split('\n');
        var portLines = port.Split('\n');
        var line = 0;
        while (line < upstreamLines.Length && line < portLines.Length && upstreamLines[line] == portLines[line])
        {
            line++;
        }

        differences.Add(new(item, DescribeLine(upstreamLines, line), DescribeLine(portLines, line)));
    }

    private static string DescribeLine(string[] lines, int line) =>
        line < lines.Length
            ? $"line {line + 1}: {lines[line].TrimEnd('\r')}"
            : $"line {line + 1}: (no such line)";

    private static void AddRequestBytesDifference(List<CurlPortDifference> differences, CurlPortSurlSide upstream, CurlPortSurlSide port)
    {
        if (upstream.ClientRequestBytes is null || port.ClientRequestBytes is null)
        {
            return;
        }

        var connections = Math.Max(upstream.ClientRequestBytes.Count, port.ClientRequestBytes.Count);
        for (var connection = 0; connection < connections; connection++)
        {
            AddBytesDifference(
                differences,
                $"request bytes on connection {connection}",
                NormalisedConnection(upstream, connection),
                NormalisedConnection(port, connection));
        }
    }

    private static byte[]? NormalisedConnection(CurlPortSurlSide surl, int connection) =>
        connection < surl.ClientRequestBytes!.Count
            ? CurlPortResultNormaliser.NormaliseRequestBytes(surl.ClientRequestBytes[connection], surl.Port)
            : null;

    private static void AddBytesDifference(List<CurlPortDifference> differences, string item, byte[]? upstream, byte[]? port)
    {
        if (upstream is not null && port is not null)
        {
            AddPresentBytesDifference(differences, item, upstream, port);
        }
        else if (upstream is not null || port is not null)
        {
            differences.Add(new(item, DescribePresence(upstream), DescribePresence(port)));
        }
    }

    private static void AddPresentBytesDifference(List<CurlPortDifference> differences, string item, byte[] upstream, byte[] port)
    {
        if (!upstream.AsSpan().SequenceEqual(port))
        {
            var offset = upstream.AsSpan().CommonPrefixLength(port);
            differences.Add(new(item, DescribeByte(upstream, offset), DescribeByte(port, offset)));
        }
    }

    private static string DescribePresence(byte[]? bytes) =>
        bytes is null ? "(absent)" : $"{bytes.Length} bytes";

    private static string DescribeByte(byte[] bytes, int offset) =>
        offset < bytes.Length
            ? $"byte 0x{bytes[offset]:X2} at offset {offset}"
            : $"end at offset {offset}";
}
