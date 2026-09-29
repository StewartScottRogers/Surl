namespace Surl.Cli;

/// <summary>
/// Composes the text <c>surl --version</c> writes to stdout (ADR-0007 section 6):
/// <c>surl &lt;version&gt; (&lt;runtime&gt;)</c>, then <c>Protocols: &lt;schemes&gt;</c>.
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
    public static string Compose(string? informationalVersion, string runtimeIdentifier, IEnumerable<string> servedSchemes)
    {
        ArgumentNullException.ThrowIfNull(runtimeIdentifier);
        ArgumentNullException.ThrowIfNull(servedSchemes);

        var version = StripBuildMetadata(informationalVersion);
        var protocolsLine = string.Join(' ', servedSchemes.Order(StringComparer.Ordinal).Prepend("Protocols:"));
        return $"surl {version} ({runtimeIdentifier}){Environment.NewLine}{protocolsLine}{Environment.NewLine}";
    }

    private static string StripBuildMetadata(string? informationalVersion)
    {
        var plus = informationalVersion?.IndexOf('+', StringComparison.Ordinal) ?? -1;
        var version = plus < 0 ? informationalVersion : informationalVersion![..plus];
        return string.IsNullOrEmpty(version) ? VersionWhenNoneIsSet : version;
    }
}
