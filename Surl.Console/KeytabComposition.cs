using Surl.Cli;
using Surl.Kerberos;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// The Kerberos acceptor <c>--keytab</c> asks for (ADR-0057, decisions 1, 6 and 7): the keytab
/// read before any listener binds, its refusals, and the warning lines it writes on start. No key
/// byte is ever written, at any log level.
/// </summary>
internal static class KeytabComposition
{
    private const string WarningPrefix = "surl: warning: ";

    // The names MIT krb5 gives the enctypes surl skips (ADR-0057 decision 3), by IANA number.
    private static readonly Dictionary<int, string> SkippedEncryptionTypeNames = new()
    {
        [1] = "des-cbc-crc",
        [2] = "des-cbc-md4",
        [3] = "des-cbc-md5",
        [16] = "des3-cbc-sha1",
        [23] = "rc4-hmac",
        [24] = "rc4-hmac-exp",
        [25] = "camellia128-cts-cmac",
        [26] = "camellia256-cts-cmac",
    };

    /// <summary>
    /// Reads the <c>--keytab</c> file through <paramref name="readFile"/> when given, and builds the
    /// acceptor over its keys, one replay cache for the process, <paramref name="timeProvider"/>
    /// and <see cref="RandomKerberosRandomSource"/>.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="readFile">Reads a file's bytes, given its path as given.</param>
    /// <param name="timeProvider">The clock the acceptor and its replay cache run on.</param>
    /// <returns>
    /// The acceptor (<see langword="null"/> without <c>--keytab</c>) and the entries skipped for their
    /// enctype; or, when the keytab cannot be used, <see langword="null"/> with the exit code and the
    /// message after the <c>surl: </c> prefix (ADR-0057, decision 1).
    /// </returns>
    public static (KerberosAcceptor? Acceptor, IReadOnlyList<KerberosKeytabSkippedEntry> SkippedEntries, SurlExitCode ExitCode, string? FailureMessage) Read(
        SurlCommandLine commandLine, Func<string, byte[]> readFile, TimeProvider timeProvider)
    {
        if (commandLine.KeytabFile is not { } keytabFile)
        {
            return (null, [], SurlExitCode.Ok, null);
        }

        byte[] content;
        try
        {
            content = readFile(keytabFile);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return (null, [], SurlExitCode.CouldNotReadFile, $"(37) Could not read keytab {keytabFile}");
        }

        var read = KerberosKeytab.Read(content);
        if (read.Keytab is not { } keytab)
        {
            return (null, [], SurlExitCode.FailedInit, $"(2) Keytab {keytabFile} is malformed at byte {read.MalformedOffset}");
        }

        return keytab.Entries.Count == 0
            ? (null, [], SurlExitCode.FailedInit, $"(2) Keytab {keytabFile} holds no key surl can use")
            : (new KerberosAcceptor(keytab, new KerberosReplayCache(timeProvider), timeProvider, new RandomKerberosRandomSource()),
                read.SkippedEntries,
                SurlExitCode.Ok,
                null);
    }

    /// <summary>
    /// Writes, from the info level up, a warning line for each keytab entry skipped for its enctype,
    /// in file order, then the unused warning when <c>--keytab</c> is given and <c>--auth</c> accepts
    /// neither <c>negotiate</c> nor <c>gssapi</c> (ADR-0057, decision 1; ADR-0032, section 9's form).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="skippedEntries">The entries <see cref="Read"/> skipped.</param>
    /// <param name="log">The log stream.</param>
    public static void WriteStartLines(SurlCommandLine commandLine, IReadOnlyList<KerberosKeytabSkippedEntry> skippedEntries, TextWriter log)
    {
        if (commandLine.LogLevel < LogLevel.Info || commandLine.KeytabFile is null)
        {
            return;
        }

        foreach (var skipped in skippedEntries)
        {
            log.WriteLine(WarningPrefix + $"--keytab: skipped the {NameEncryptionType(skipped.EncryptionTypeNumber)} key of {skipped.Principal}");
        }

        if (!AcceptsAKerberosMethod(commandLine))
        {
            log.WriteLine(WarningPrefix + "--keytab is unused: --auth accepts neither negotiate nor gssapi");
        }
    }

    /// <summary>
    /// Whether <c>--auth</c> names <c>gssapi</c> without <c>--keytab</c>, which a start refuses
    /// (ADR-0057, decision 1): a server offering <c>GSSAPI</c> with no key would break every curl
    /// login that picks it.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns><see langword="true"/> when the start is refused.</returns>
    public static bool IsGssapiWithoutKeytab(SurlCommandLine commandLine) =>
        commandLine.KeytabFile is null && commandLine.AcceptedAuthenticationMethods.Contains("gssapi");

    /// <summary>The enctype's MIT krb5 name, or <c>enctype &lt;number&gt;</c> for one without a name here.</summary>
    /// <param name="encryptionTypeNumber">The enctype number, as the IANA registry numbers it.</param>
    /// <returns>The name.</returns>
    internal static string NameEncryptionType(int encryptionTypeNumber) =>
        SkippedEncryptionTypeNames.TryGetValue(encryptionTypeNumber, out var name) ? name : $"enctype {encryptionTypeNumber}";

    private static bool AcceptsAKerberosMethod(SurlCommandLine commandLine) =>
        commandLine.AcceptedAuthenticationMethods.Contains("negotiate") || commandLine.AcceptedAuthenticationMethods.Contains("gssapi");
}
