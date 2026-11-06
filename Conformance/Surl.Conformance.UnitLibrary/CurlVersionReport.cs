namespace Surl.Conformance;

/// <summary>
/// What a client's <c>curl --version</c> says about it, read as upstream's runtests reads it
/// (<c>checksystemfeatures</c> in <c>tests/runtests.pl</c> at tag <c>curl-8_21_0</c>): the first line,
/// the version token runtests calls <c>CURLVERSION</c> (a case's <c>%VERSION</c>) and its leading
/// digits and dots (<c>CURLVERNUM</c>, <c>%VERNUM</c>), and the <c>Protocols:</c> and
/// <c>Features:</c> words. Both an upstream pin and the Curl port's client are described this way,
/// so a case runs with each client's own variables and features, as runtests would run it.
/// </summary>
/// <param name="VersionLine">The first line, <c>curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 ...</c>.</param>
/// <param name="Version">The word after <c>curl </c> on the first line.</param>
/// <param name="VersionNumber">The leading digits and dots of <see cref="Version"/>.</param>
/// <param name="Protocols">The <c>Protocols:</c> words, in order.</param>
/// <param name="Features">The <c>Features:</c> words, in order.</param>
public sealed record CurlVersionReport(
    string VersionLine,
    string Version,
    string VersionNumber,
    IReadOnlyList<string> Protocols,
    IReadOnlyList<string> Features)
{
    private const string CurlPrefix = "curl ";

    /// <summary>
    /// Reads the text a client's <c>--version</c> wrote.
    /// </summary>
    /// <param name="text">The whole output; lines end in LF or CRLF.</param>
    /// <returns>The report.</returns>
    /// <exception cref="FormatException">No line starts with <c>curl </c>, which runtests needs.</exception>
    public static CurlVersionReport Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        var versionLine = lines.FirstOrDefault(line => line.StartsWith(CurlPrefix, StringComparison.Ordinal))
            ?? throw new FormatException("The --version output has no line starting \"curl \".");

        return FromVersionLine(versionLine, Words(lines, "Protocols:"), Words(lines, "Features:"));
    }

    /// <summary>
    /// Describes a pinned upstream build from its pin: its <c>version</c>, <c>protocols</c> and
    /// <c>features</c> lines, which were copied from its <c>--version</c> output.
    /// </summary>
    /// <param name="build">The pin.</param>
    /// <returns>The report.</returns>
    public static CurlVersionReport FromPin(PinnedUpstreamCurlBuild build)
    {
        ArgumentNullException.ThrowIfNull(build);

        return FromVersionLine(build.Version, build.Protocols, build.Features);
    }

    private static CurlVersionReport FromVersionLine(string versionLine, IReadOnlyList<string> protocols, IReadOnlyList<string> features)
    {
        var version = versionLine.StartsWith(CurlPrefix, StringComparison.Ordinal)
            ? versionLine[CurlPrefix.Length..].Split(' ')[0]
            : string.Empty;
        var number = new string([.. version.TakeWhile(character => char.IsAsciiDigit(character) || character == '.')]);

        return new CurlVersionReport(versionLine, version, number, protocols, features);
    }

    private static string[] Words(IEnumerable<string> lines, string label) =>
        lines.FirstOrDefault(line => line.StartsWith(label, StringComparison.Ordinal)) is { } line
            ? line[label.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries)
            : [];
}
