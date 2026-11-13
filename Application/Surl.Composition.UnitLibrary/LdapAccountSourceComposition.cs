using System.Security.Cryptography.X509Certificates;
using System.Text.Unicode;
using Surl.Authentication;
using Surl.Cli;
using Surl.Networking;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Composes the <c>--user-ldap</c> account source (ADR-0149 decisions 7 and 9): the URL's structure checked
/// by <see cref="LdapDirectoryUrl"/>, <c>plaintext</c> held to <c>--allow-plaintext-auth</c>, the bind
/// password file and the CA file read once, and the source built over the TLS client connector. Nothing
/// about the directory is contacted at start. No refusal holds the URL, the bind DN or a password.
/// </summary>
internal static class LdapAccountSourceComposition
{
    /// <summary>The most bytes of a bind password file read (ADR-0149 decision 10).</summary>
    public const int MaxBindPasswordFileBytes = 64 * 1024;

    private const string Option = "--user-ldap";
    private const string WarningPrefix = "surl: warning: ";

    /// <summary>
    /// Builds the source <paramref name="commandLine"/> names, or none when <c>--user-ldap</c> is not given.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="readFile">Reads a start file's bytes, given its path as given.</param>
    /// <param name="timeProvider">The clock the check's timeout runs on.</param>
    /// <param name="connector">The seam a test replaces; the TLS client connector when <see langword="null"/>.</param>
    /// <param name="note">Receives each refused login's note; none when <see langword="null"/>.</param>
    /// <returns>The source, or <see langword="null"/> with the exit code and the message after <c>surl: </c> when none is composed.</returns>
    public static (LdapAccountSource? Source, SurlExitCode ExitCode, string? FailureMessage) Compose(
        SurlCommandLine commandLine,
        Func<string, byte[]> readFile,
        TimeProvider timeProvider,
        ILdapDirectoryConnector? connector,
        Action<string>? note)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(readFile);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (commandLine.UserLdap is not { } configured)
        {
            return (null, SurlExitCode.Ok, null);
        }

        var (url, refusal) = CheckStructure(commandLine, configured);
        if (refusal is not null)
        {
            return (null, SurlExitCode.FailedInit, refusal);
        }

        var (bindPassword, passwordExitCode, passwordFailure) = ReadBindPassword(configured, readFile);
        if (passwordFailure is not null)
        {
            return (null, passwordExitCode, passwordFailure);
        }

        var (anchors, anchorExitCode, anchorFailure) = ReadTrustAnchors(configured, readFile);
        if (anchorFailure is not null)
        {
            return (null, anchorExitCode, anchorFailure);
        }

        var settings = new LdapAccountSourceSettings(url!)
        {
            BindDn = configured.BindDn,
            BindPassword = bindPassword,
            TimeoutSeconds = configured.TimeoutSeconds,
            Protocols = configured.Protocols,
            IsPlaintext = configured.IsPlaintext,
        };
        var seam = connector ?? new TlsClientLdapDirectoryConnector(
            new TlsClientConnector(anchors, timeProvider), TimeSpan.FromSeconds(configured.TimeoutSeconds));

        return (new LdapAccountSource(settings, seam, timeProvider, note), SurlExitCode.Ok, null);
    }

    /// <summary>
    /// Writes ADR-0149 decision 9's warning for <c>;plaintext</c>, from the info level up.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="log">The log stream.</param>
    public static void WriteWarning(SurlCommandLine commandLine, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(log);
        if (commandLine.LogLevel >= LogLevel.Info && commandLine.UserLdap is { IsPlaintext: true } configured
            && LdapDirectoryUrl.TryParse(configured.Url, out var url, out _))
        {
            log.WriteLine(WarningPrefix + $"{Option}: passwords are sent to {url!.Host}:{url.Port} without TLS");
        }
    }

    /// <summary>
    /// Writes <c>Account source: LDAP directory &lt;scheme&gt;://&lt;host&gt;:&lt;port&gt;</c> from the verbose level
    /// up, never the base DN, the filter or the bind DN (ADR-0149 decision 9).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="log">The log stream.</param>
    public static void WriteStartLine(SurlCommandLine commandLine, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(log);
        if (commandLine.LogLevel >= LogLevel.Verbose && commandLine.UserLdap is { } configured
            && LdapDirectoryUrl.TryParse(configured.Url, out var url, out _))
        {
            var scheme = url!.UsesTlsFromTheStart ? "ldaps" : "ldap";
            log.WriteLine($"Account source: LDAP directory {scheme}://{url.Host}:{url.Port}");
        }
    }

    private static (LdapDirectoryUrl? Url, string? Refusal) CheckStructure(SurlCommandLine commandLine, ConfiguredLdapAccountSource configured)
    {
        if (!LdapDirectoryUrl.TryParse(configured.Url, out var url, out var failure))
        {
            return (null, $"(2) {Option}: {Describe(failure)}");
        }

        return configured.IsPlaintext && !commandLine.AllowPlaintextAuthentication
            ? (null, $"(2) {Option} plaintext needs --allow-plaintext-auth")
            : (url, null);
    }

    private static string Describe(LdapDirectoryUrlFailure failure) => failure switch
    {
        LdapDirectoryUrlFailure.HasUserInformation => "the URL holds a user name or password; use bind-dn and bind-password-file",
        LdapDirectoryUrlFailure.NoHost => "the URL names no host",
        LdapDirectoryUrlFailure.BadPort => "the URL's port is not 1 to 65535",
        LdapDirectoryUrlFailure.BadBaseDn => "the URL's base DN is missing or malformed",
        LdapDirectoryUrlFailure.MultipleAttributes => "the URL names more than one attribute",
        LdapDirectoryUrlFailure.BadAttribute => "the URL's attribute is not an attribute description",
        LdapDirectoryUrlFailure.BadScope => "the URL's scope is not base, one or sub",
        LdapDirectoryUrlFailure.BadFilter => "the URL's filter is malformed",
        LdapDirectoryUrlFailure.HasExtensions => "the URL's extensions are not supported",
        _ => "the URL is not ldap:// or ldaps://",
    };

    // The first line of the file, to LF with one CR before it dropped (ADR-0149 decision 7).
    private static (ReadOnlyMemory<byte> Password, SurlExitCode ExitCode, string? FailureMessage) ReadBindPassword(
        ConfiguredLdapAccountSource configured, Func<string, byte[]> readFile)
    {
        if (configured.BindPasswordFile is not { } path)
        {
            return (ReadOnlyMemory<byte>.Empty, SurlExitCode.Ok, null);
        }

        var content = TryRead(readFile, path);
        if (content is null || content.Length > MaxBindPasswordFileBytes)
        {
            return (default, SurlExitCode.CouldNotReadFile, $"(37) Could not read bind password file {path}");
        }

        var lineEnd = Array.IndexOf(content, (byte)'\n');
        var line = content.AsMemory(0, lineEnd < 0 ? content.Length : lineEnd);
        if (!line.IsEmpty && line.Span[^1] == '\r')
        {
            line = line[..^1];
        }

        return line.IsEmpty || !Utf8.IsValid(line.Span)
            ? (default, SurlExitCode.FailedInit, $"(2) Bind password file {path}: the password is empty or not UTF-8")
            : (line, SurlExitCode.Ok, null);
    }

    private static (IReadOnlyList<X509Certificate2> Anchors, SurlExitCode ExitCode, string? FailureMessage) ReadTrustAnchors(
        ConfiguredLdapAccountSource configured, Func<string, byte[]> readFile)
    {
        if (configured.CaFile is not { } path)
        {
            return ([], SurlExitCode.Ok, null);
        }

        var content = TryRead(readFile, path);
        if (content is null)
        {
            return ([], SurlExitCode.CaCertificateBadFile, $"(77) Could not read CA file {path}");
        }

        try
        {
            return (ClientTrustAnchorFileLoader.Load(path, _ => content), SurlExitCode.Ok, null);
        }
        catch (TlsFileLoadException)
        {
            return ([], SurlExitCode.CaCertificateBadFile, $"(77) CA file {path} holds no certificate");
        }
    }

    private static byte[]? TryRead(Func<string, byte[]> readFile, string path)
    {
        try
        {
            return readFile(path);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
