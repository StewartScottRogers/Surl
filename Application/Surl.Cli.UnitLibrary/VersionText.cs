namespace Surl.Cli;

/// <summary>
/// Composes the text <c>surl --version</c> writes to stdout (ADR-0007 section 6):
/// <c>surl &lt;version&gt; (&lt;runtime&gt;)</c>, then <c>Protocols: &lt;schemes&gt;</c>, then, while
/// extended protocol servers are registered, <c>Extended: &lt;schemes&gt;</c> (ADR-0083, decision 8).
/// </summary>
/// <remarks>
/// The version, runtime and schemes are the caller's to supply: only <c>Surl.Console</c>
/// knows the <c>surl</c> assembly's version and which protocol servers it registered.
/// </remarks>
public static class VersionText
{
    private const string VersionWhenNoneIsSet = "1.0.0";

    /// <summary>Composes the two lines of <c>--version</c>, each ending with <see cref="Environment.NewLine"/>.</summary>
    /// <param name="informationalVersion">
    /// The <c>surl</c> assembly's <c>AssemblyInformationalVersionAttribute</c>, or
    /// <see langword="null"/>; a <c>+</c> and what follows it is removed, and an empty or
    /// missing version is <c>1.0.0</c>.
    /// </param>
    /// <param name="runtimeIdentifier">
    /// <c>RuntimeInformation.RuntimeIdentifier</c> (<c>win-x64</c>, <c>linux-arm64</c>).
    /// </param>
    /// <param name="servedSchemes">Every scheme a registered protocol server claims, in any order.</param>
    /// <returns>The version text, the schemes in ordinal order separated by single spaces.</returns>
    public static string Compose(string? informationalVersion, string runtimeIdentifier, IEnumerable<string> servedSchemes) =>
        Compose(informationalVersion, runtimeIdentifier, servedSchemes, []);

    /// <summary>
    /// Composes <c>--version</c>: the two lines of <see cref="Compose(string?, string, IEnumerable{string})"/>,
    /// then, when <paramref name="extendedSchemes"/> holds any, <c>Extended: &lt;schemes&gt;</c>
    /// (ADR-0083, decision 8). The <c>Protocols:</c> line never lists an extended scheme.
    /// </summary>
    /// <param name="informationalVersion">As <see cref="Compose(string?, string, IEnumerable{string})"/> takes it.</param>
    /// <param name="runtimeIdentifier">As <see cref="Compose(string?, string, IEnumerable{string})"/> takes it.</param>
    /// <param name="servedSchemes">Every scheme a registered original protocol server claims, in any order.</param>
    /// <param name="extendedSchemes">Every scheme a registered extended protocol server claims, in any order; empty for none.</param>
    /// <returns>The version text, each scheme list in ordinal order separated by single spaces.</returns>
    public static string Compose(
        string? informationalVersion, string runtimeIdentifier, IEnumerable<string> servedSchemes, IEnumerable<string> extendedSchemes)
    {
        ArgumentNullException.ThrowIfNull(runtimeIdentifier);
        ArgumentNullException.ThrowIfNull(servedSchemes);
        ArgumentNullException.ThrowIfNull(extendedSchemes);

        var version = StripBuildMetadata(informationalVersion);
        var protocolsLine = SchemeLine("Protocols:", servedSchemes);
        var extended = extendedSchemes.ToArray();
        var extendedLine = extended.Length == 0 ? string.Empty : SchemeLine("Extended:", extended) + Environment.NewLine;
        return $"surl {version} ({runtimeIdentifier}){Environment.NewLine}{protocolsLine}{Environment.NewLine}{extendedLine}";
    }

    private static string SchemeLine(string label, IEnumerable<string> schemes) =>
        string.Join(' ', schemes.Order(StringComparer.Ordinal).Prepend(label));

    private static string StripBuildMetadata(string? informationalVersion)
    {
        var plus = informationalVersion?.IndexOf('+', StringComparison.Ordinal) ?? -1;
        var version = plus < 0 ? informationalVersion : informationalVersion![..plus];
        return string.IsNullOrEmpty(version) ? VersionWhenNoneIsSet : version;
    }
}
