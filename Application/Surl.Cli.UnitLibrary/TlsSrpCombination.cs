using System.Collections.Frozen;
using System.Security.Authentication;
using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// Checks the TLS-SRP options once the whole command line is read (ADR-0122 decision 6 and its
/// Amendment 2): each needs its partner, <c>--tlsauthtype SRP</c> needs an account, a TLS listen
/// URL must use them, and they cannot be combined with an option TLS-SRP cannot serve. Each
/// refusal is <see cref="SurlExitCode.FailedInit"/> followed by the try-help line.
/// </summary>
internal static class TlsSrpCombination
{
    /// <summary>A partner option missing, after the option given alone.</summary>
    public const string NeedsPartnerFormat = "needs {0}";

    /// <summary><c>--tlsauthtype SRP</c> with no account from either source.</summary>
    public const string NeedsAnAccount = "needs --tlsuser and --tlspassword, or --srp-verifiers and --srp-groups";

    /// <summary>TLS-SRP options with no listen URL that uses TLS.</summary>
    public const string NeedsATlsListenUrl = "needs a listen URL that uses TLS";

    /// <summary>
    /// The listen URL schemes that start in plain text and can upgrade to TLS, which TLS-SRP then
    /// secures: <c>STARTTLS</c> and its kin (ADR-0122 decision 5), as <c>Surl.Composition</c>'s
    /// <c>ServerTlsComposition</c> lists them.
    /// </summary>
    private static readonly FrozenSet<string> UpgradableSchemes =
        FrozenSet.Create(StringComparer.OrdinalIgnoreCase, "ftp", "imap", "ldap", "natss", "pop3", "smtp", "xmpp");

    /// <summary>
    /// Refuses the first TLS-SRP combination that cannot work, in this order: <c>--tlsuser</c>
    /// without <c>--tlspassword</c> or the reverse; <c>--srp-verifiers</c> without
    /// <c>--srp-groups</c> or the reverse; <c>--tlsauthtype</c> with no account; no listen URL that
    /// uses TLS; then TLS-SRP beside <c>--http3</c>, <c>--cacert</c>, <c>--tls-max</c> below 1.2,
    /// <c>--tlsv1.3</c>, <c>--cert</c> or <c>--self-signed</c>. Null when none is, and whenever no
    /// TLS-SRP option is given.
    /// </summary>
    /// <param name="commandLine">The command line read so far.</param>
    /// <param name="listenUrls">The listen URLs given.</param>
    /// <returns>The refusal, naming the option at fault, or <see langword="null"/>.</returns>
    public static CommandLineFailure? Refuse(SurlCommandLine commandLine, IReadOnlyList<ListenUrl> listenUrls)
    {
        if (!commandLine.ServesTlsSrp)
        {
            return null;
        }

        return RefuseMissingPartner(commandLine.TlsUser, "--tlsuser", commandLine.TlsPassword, "--tlspassword")
            ?? RefuseMissingPartner(commandLine.SrpVerifierFile, "--srp-verifiers", commandLine.SrpGroupFile, "--srp-groups")
            ?? RefuseWithoutAccount(commandLine)
            ?? RefuseWithoutTlsListenUrl(commandLine, listenUrls)
            ?? RefuseBeside(commandLine);
    }

    private static CommandLineFailure? RefuseMissingPartner(string? first, string firstName, string? second, string secondName) =>
        (first is null, second is null) switch
        {
            (false, true) => Refused(firstName, string.Format(System.Globalization.CultureInfo.InvariantCulture, NeedsPartnerFormat, secondName)),
            (true, false) => Refused(secondName, string.Format(System.Globalization.CultureInfo.InvariantCulture, NeedsPartnerFormat, firstName)),
            _ => null,
        };

    // Partners are checked first, so one of each pair given means both are.
    private static CommandLineFailure? RefuseWithoutAccount(SurlCommandLine commandLine) =>
        commandLine.TlsUser is null && commandLine.SrpVerifierFile is null
            ? Refused("--tlsauthtype", NeedsAnAccount)
            : null;

    private static CommandLineFailure? RefuseWithoutTlsListenUrl(SurlCommandLine commandLine, IReadOnlyList<ListenUrl> listenUrls) =>
        listenUrls.Any(listenUrl => UsesTls(listenUrl)) || commandLine.ProxyListenUrls.Any(listenUrl => UsesTls(listenUrl))
            ? null
            : Refused(FirstOptionName(commandLine), NeedsATlsListenUrl);

    private static bool UsesTls(ListenUrl listenUrl) => listenUrl.IsImplicitTls || UpgradableSchemes.Contains(listenUrl.Scheme);

    private static CommandLineFailure? RefuseBeside(SurlCommandLine commandLine)
    {
        var beside = FindRefusedBeside(commandLine);
        return beside is null ? null : Refused(FirstOptionName(commandLine), $"cannot be used with {beside}");
    }

    // QUIC is TLS 1.3, which has no SRP; the client proves itself with its password, so no client
    // certificate is asked for; TLS-SRP is TLS 1.2 only; the signed suites a certificate would add
    // are BL-911's (ADR-0122 Amendment 2). In the order they are refused.
    private static readonly (Func<SurlCommandLine, bool> IsGiven, string Option)[] RefusedBesideTlsSrp =
    [
        (commandLine => commandLine.Http3, "--http3"),
        (commandLine => commandLine.CaCertificateFile is not null, "--cacert"),
        (commandLine => commandLine.HighestTlsVersion < SslProtocols.Tls12, "--tls-max below 1.2"),
        (commandLine => commandLine.LowestTlsVersion == SslProtocols.Tls13, "--tlsv1.3"),
        (commandLine => commandLine.CertificateFile is not null, "--cert"),
        (commandLine => commandLine.SelfSigned, "--self-signed"),
    ];

    private static string? FindRefusedBeside(SurlCommandLine commandLine) =>
        RefusedBesideTlsSrp.FirstOrDefault(refused => refused.IsGiven(commandLine)).Option;

    // The option that turned TLS-SRP on, in the order the options are documented.
    private static string FirstOptionName(SurlCommandLine commandLine) => commandLine switch
    {
        { TlsAuthenticationType: not null } => "--tlsauthtype",
        { TlsUser: not null } => "--tlsuser",
        _ => "--srp-verifiers",
    };

    private static CommandLineFailure Refused(string optionName, string reason) =>
        new(SurlExitCode.FailedInit, $"option {optionName}: {reason}", FollowedByTryHelpLine: true);
}
