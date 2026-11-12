using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Surl.Cli;
using Surl.Networking;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The process's server-side TLS settings as the command line asks for them (ADR-0010,
/// section 3; ADR-0020; ADR-0032, section 10): none when no listen URL is TLS from the first
/// byte and none can be upgraded with a certificate given; otherwise the <c>--cert</c> certificate,
/// or a throwaway one with <c>--self-signed</c>, with the <c>--cacert</c> trust anchors and the
/// accepted TLS versions. It owns every certificate it loaded or made,
/// and disposing it disposes them.
/// </summary>
public sealed class ServerTlsComposition : IDisposable
{
    private readonly IReadOnlyList<IDisposable> owned;

    private ServerTlsComposition(
        ServerTlsSettings? settings, string? throwawayCertificateFingerprint, QuicServerCertificate? quicCertificate, IReadOnlyList<IDisposable> owned,
        IReadOnlyList<X509Certificate2>? clientTrustAnchors = null, IReadOnlyList<string>? srpGroupIndexesOutsideRfc5054 = null)
    {
        ClientTrustAnchors = clientTrustAnchors ?? [];
        SrpGroupIndexesOutsideRfc5054 = srpGroupIndexesOutsideRfc5054 ?? [];
        Settings = settings;
        QuicCertificate = quicCertificate;
        ThrowawayCertificateFingerprint = throwawayCertificateFingerprint;
        this.owned = owned;
    }

    /// <summary>
    /// The listen URL schemes whose server can upgrade a plaintext connection to TLS in the
    /// middle of it (SMTP's <c>STARTTLS</c>, ADR-0053 decision 5; IMAP's <c>STARTTLS</c>, ADR-0055
    /// decision 11; POP3's <c>STLS</c>, ADR-0056 decision 8; FTP's <c>AUTH TLS</c>, ADR-0052
    /// decision 5; LDAP's <c>StartTLS</c>, ADR-0072 decision 5), XMPP's <c>STARTTLS</c>, ADR-0114 decision 2), which it does only when a
    /// certificate is configured.
    /// </summary>
    private static readonly HashSet<string> UpgradableSchemes = new(StringComparer.OrdinalIgnoreCase) { "ftp", "imap", "ldap", "pop3", "smtp", "xmpp" };

    /// <summary>
    /// The listen URL schemes that start in plain text but whose server requires TLS before a login,
    /// so they need a certificate as a scheme TLS from the first byte does: the extended
    /// <c>natss</c> (ADR-0113 decision 6), the extended <c>coaps</c> when no account gives it a pre-shared key (ADR-0087 decision 3, BL-476), and the extended <c>webtransport</c>, which is UDP and
    /// QUIC, whose TLS 1.3 needs a certificate from its first packet (BL-461).
    /// </summary>
    private static readonly HashSet<string> UpgradeRequiredSchemes = new(StringComparer.OrdinalIgnoreCase) { "natss", WebTransportScheme };

    private const string WebTransportScheme = "webtransport";

    private const string CoapsScheme = "coaps";

    /// <summary>
    /// The settings every listener secures its connections with, or <see langword="null"/>
    /// when no listen URL is TLS from the first byte and none can be upgraded.
    /// </summary>
    public ServerTlsSettings? Settings { get; }

    /// <summary>
    /// The throwaway certificate's SHA-256 fingerprint when one is served, for the verbose
    /// note; <see langword="null"/> when <c>--cert</c> was given or no TLS is served.
    /// </summary>
    public string? ThrowawayCertificateFingerprint { get; }

    /// <summary>
    /// The same certificate as QUIC's TLS 1.3 serves it, with <c>--http3</c> (ADR-0082, decision 6);
    /// <see langword="null"/> without it or when no TLS is served.
    /// </summary>
    public QuicServerCertificate? QuicCertificate { get; }

    /// <summary>The <c>--cacert</c> anchors a <c>coaps</c> client certificate must chain to; none when <c>--cacert</c> is not given.</summary>
    public IReadOnlyList<X509Certificate2> ClientTrustAnchors { get; }

    /// <summary>
    /// The <c>--srp-groups</c> indexes whose group is not one of RFC 5054 Appendix A's, in file order
    /// (ADR-0122 decision 4); empty when TLS-SRP is not served or every group is Appendix A's.
    /// </summary>
    public IReadOnlyList<string> SrpGroupIndexesOutsideRfc5054 { get; }

    /// <summary>
    /// Writes ADR-0122 decision 4's note, once per <c>--srp-groups</c> group outside RFC 5054
    /// Appendix A, from the verbose level up: OpenSSL's client, which upstream curl uses for TLS-SRP,
    /// refuses such a group, so a login against it fails however right the password is.
    /// </summary>
    /// <param name="logLevel">The log level.</param>
    /// <param name="log">The log stream.</param>
    public void WriteStartLines(LogLevel logLevel, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (logLevel < LogLevel.Verbose)
        {
            return;
        }

        foreach (var index in SrpGroupIndexesOutsideRfc5054)
        {
            log.WriteLine($"* --srp-groups: the group with index {index} is not one of RFC 5054 Appendix A's; OpenSSL's client refuses it");
        }
    }

    /// <summary>
    /// Finds the first listen URL, in command-line order, that is TLS from the first byte or
    /// requires TLS before a login (<c>natss</c>, ADR-0113 decision 6), and has no certificate to serve because neither <c>--cert</c> nor <c>--self-signed</c> was
    /// given (ADR-0032, section 10).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns>That listen URL, or <see langword="null"/> when every listen URL has what it needs.</returns>
    public static ListenUrl? FindListenUrlWithoutCertificate(SurlCommandLine commandLine) =>
        commandLine.CertificateFile is null && !commandLine.SelfSigned
            ? commandLine.ListenUrls.FirstOrDefault(listenUrl => RequiresCertificate(commandLine, listenUrl) && !IsSecuredByTlsSrp(commandLine, listenUrl.Scheme))
            : null;

    // Under TLS-SRP every TCP listener is secured without a certificate (ADR-0122 decisions 2 and 5);
    // QUIC (webtransport) and DTLS (coaps) are not TLS-SRP's, so they still need one.
    private static bool IsSecuredByTlsSrp(SurlCommandLine commandLine, string scheme) =>
        commandLine.ServesTlsSrp && scheme is not (WebTransportScheme or CoapsScheme);

    /// <summary>
    /// Whether a server that can upgrade a plaintext connection to TLS offers it: a certificate is
    /// configured, or TLS-SRP is served (ADR-0053 decision 5; ADR-0122 decision 5).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns><see langword="true"/> when either is so.</returns>
    public static bool IsTlsUpgradeAvailable(SurlCommandLine commandLine) =>
        IsCertificateConfigured(commandLine) || commandLine.ServesTlsSrp;

    /// <summary>
    /// Whether a certificate is configured, <c>--cert</c> or <c>--self-signed</c>: what makes a
    /// server that can upgrade a plaintext connection offer it (ADR-0053, decision 5).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns><see langword="true"/> when either is given.</returns>
    public static bool IsCertificateConfigured(SurlCommandLine commandLine) =>
        commandLine.CertificateFile is not null || commandLine.SelfSigned;

    /// <summary>
    /// Reads the TLS option files and builds the settings, when a listen URL needs them: one
    /// TLS from the first byte, or, with a certificate configured, one that can be upgraded. The
    /// files are read only then, as upstream curl reads <c>--cert</c> only for a TLS transfer.
    /// Without <c>--cert</c> it makes the throwaway certificate, which the caller allows only
    /// with <c>--self-signed</c> (<see cref="FindListenUrlWithoutCertificate"/>).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="timeProvider">Supplies "now" for the throwaway certificate and client-chain verification.</param>
    /// <param name="readFile">Reads a TLS option file's bytes (the <c>ReadStartFile</c> seam); <see langword="null"/> reads the disk.</param>
    /// <returns>The composition, which the caller disposes once serving has ended.</returns>
    /// <exception cref="TlsFileLoadException">A <c>--cert</c>, <c>--key</c> or <c>--cacert</c> file cannot be loaded.</exception>
    /// <exception cref="TlsSrpAccountsException">A <c>--srp-verifiers</c> or <c>--srp-groups</c> file cannot be read, or a TLS-SRP user is given twice.</exception>
    public static ServerTlsComposition Compose(SurlCommandLine commandLine, TimeProvider timeProvider, Func<string, byte[]>? readFile = null)
    {
        if (!commandLine.ListenUrls.Any(listenUrl => NeedsTls(commandLine, listenUrl)))
        {
            return new ServerTlsComposition(null, null, null, []);
        }

        if (commandLine.ServesTlsSrp)
        {
            return ComposeTlsSrp(commandLine, readFile);
        }

        var trustAnchors = commandLine.CaCertificateFile is { } caCertificateFile
            ? ClientTrustAnchorFileLoader.Load(caCertificateFile, readFile)
            : [];

        try
        {
            return commandLine.CertificateFile is { } certificateFile
                ? ComposeFromFiles(commandLine, certificateFile, trustAnchors, timeProvider, readFile)
                : ComposeWithThrowaway(commandLine, trustAnchors, timeProvider);
        }
        catch (TlsFileLoadException)
        {
            DisposeAll(trustAnchors);
            throw;
        }
    }

    private static bool NeedsTls(SurlCommandLine commandLine, ListenUrl listenUrl) =>
        RequiresCertificate(commandLine, listenUrl)
        || (IsTlsUpgradeAvailable(commandLine) && UpgradableSchemes.Contains(listenUrl.Scheme))
        || (IsCertificateConfigured(commandLine) && listenUrl.Scheme == CoapsScheme);

    // TLS-SRP settings (ADR-0122 decisions 4 and 5): the accounts, and one random source for them and
    // every handshake, owned by the composition.
    private static ServerTlsComposition ComposeTlsSrp(SurlCommandLine commandLine, Func<string, byte[]>? readFile)
    {
        var random = RandomNumberGenerator.Create();
        try
        {
            var composed = TlsSrpAccountsComposition.Compose(commandLine, random, readFile);
            return new ServerTlsComposition(
                ServerTlsSettings.ForTlsSrp(composed.Accounts, random), null, null, [random], null, composed.GroupIndexesOutsideRfc5054);
        }
        catch (TlsSrpAccountsException)
        {
            random.Dispose();
            throw;
        }
    }

    // coaps needs a certificate or an account, whose pre-shared key it can handshake with (ADR-0087 decision 3).
    private static bool RequiresCertificate(SurlCommandLine commandLine, ListenUrl listenUrl) =>
        listenUrl.IsImplicitTls
        || UpgradeRequiredSchemes.Contains(listenUrl.Scheme)
        || (listenUrl.Scheme == CoapsScheme && commandLine.Accounts.Count == 0 && commandLine.UserFile is null);

    /// <summary>
    /// Formats the message for a TLS option file that could not be loaded, after the
    /// <c>surl: </c> prefix, and the exit code it ends surl with (ADR-0010 section 3, ADR-0020).
    /// </summary>
    /// <param name="failure">The failure the loader threw.</param>
    /// <param name="caCertificateFile">The <c>--cacert</c> file as given, named by the "does not exist" lines.</param>
    /// <returns>The exit code and the message lines.</returns>
    public static (SurlExitCode ExitCode, string[] Lines) DescribeTlsFileFailure(
        TlsFileLoadException failure, string? caCertificateFile) => failure.Failure switch
        {
            TlsFileLoadFailure.CaCertificateNotFound => (
                SurlExitCode.FailedInit,
                [$"The file '{caCertificateFile}' provided to --cacert does not exist", "option --cacert: is badly used here"]),
            TlsFileLoadFailure.CaCertificateUnreadable => (
                SurlExitCode.CaCertificateBadFile, [$"(77) Could not load the CA certificates: {failure.Message}"]),
            _ => (SurlExitCode.CertificateProblem, [$"(58) Could not load the server certificate: {failure.Message}"]),
        };

    /// <summary>
    /// The <see cref="ServerCertificateFormat"/> a <c>--cert-type</c> word names.
    /// </summary>
    /// <param name="format">The parsed <c>--cert-type</c>.</param>
    /// <returns>The format the certificate loader reads.</returns>
    internal static ServerCertificateFormat MapCertificateFormat(CertificateFileFormat format) => format switch
    {
        CertificateFileFormat.Der => ServerCertificateFormat.Der,
        CertificateFileFormat.Pkcs12 => ServerCertificateFormat.P12,
        _ => ServerCertificateFormat.Pem,
    };

    /// <summary>
    /// The <see cref="ServerKeyFormat"/> a <c>--key-type</c> word names; the command line
    /// allows only <c>PEM</c> and <c>DER</c> there.
    /// </summary>
    /// <param name="format">The parsed <c>--key-type</c>.</param>
    /// <returns>The format the key is read in.</returns>
    internal static ServerKeyFormat MapKeyFormat(CertificateFileFormat format) =>
        format == CertificateFileFormat.Der ? ServerKeyFormat.Der : ServerKeyFormat.Pem;

    /// <summary>
    /// Disposes the settings and every certificate loaded or made for them.
    /// </summary>
    public void Dispose()
    {
        Settings?.Dispose();
        DisposeAll(owned);
    }

    private static ServerTlsComposition ComposeFromFiles(
        SurlCommandLine commandLine,
        string certificateFile,
        IReadOnlyList<X509Certificate2> trustAnchors,
        TimeProvider timeProvider,
        Func<string, byte[]>? readFile)
    {
        var loaded = ServerCertificateFileLoader.Load(
            certificateFile,
            MapCertificateFormat(commandLine.CertificateType),
            commandLine.KeyFile,
            MapKeyFormat(commandLine.KeyType),
            commandLine.KeyPassphrase,
            readFile);
        var settings = CreateSettings(commandLine, loaded.Certificate, loaded.IntermediateCertificates, trustAnchors, timeProvider);

        var quicCertificate = ComposeQuicCertificate(commandLine, loaded.Certificate, loaded.IntermediateCertificates);

        return new ServerTlsComposition(settings, null, quicCertificate, [loaded, .. trustAnchors, .. OwnedKey(quicCertificate)], trustAnchors);
    }

    private static ServerTlsComposition ComposeWithThrowaway(
        SurlCommandLine commandLine, IReadOnlyList<X509Certificate2> trustAnchors, TimeProvider timeProvider)
    {
        var throwaway = ThrowawayServerCertificate.Create(
            timeProvider, commandLine.ListenUrls.Select(listenUrl => listenUrl.Host));
        var settings = CreateSettings(commandLine, throwaway, [], trustAnchors, timeProvider);

        var quicCertificate = ComposeQuicCertificate(commandLine, throwaway, []);

        return new ServerTlsComposition(
            settings,
            ThrowawayServerCertificate.Sha256FingerprintOf(throwaway),
            quicCertificate,
            [throwaway, .. trustAnchors, .. OwnedKey(quicCertificate)],
            trustAnchors);
    }

    private static QuicServerCertificate? ComposeQuicCertificate(
        SurlCommandLine commandLine, X509Certificate2 certificate, IReadOnlyList<X509Certificate2> intermediateCertificates) =>
        (commandLine.Http3 || commandLine.ListenUrls.Any(NeedsQuicCertificate)) ? QuicServerCertificate.From(certificate, intermediateCertificates) : null;

    // webtransport, coaps, and the datagram side of dns that is TLS from the first byte (DNS over QUIC, BL-1019).
    private static bool NeedsQuicCertificate(ListenUrl listenUrl) =>
        listenUrl.Scheme is WebTransportScheme or CoapsScheme
        || (listenUrl.Scheme == "dns" && listenUrl.IsImplicitTls && listenUrl.OnlyTransport == ListenTransport.Datagrams);

    private static IEnumerable<IDisposable> OwnedKey(QuicServerCertificate? quicCertificate) =>
        quicCertificate is null ? [] : [quicCertificate.Key];

    private static ServerTlsSettings CreateSettings(
        SurlCommandLine commandLine,
        X509Certificate2 certificate,
        IReadOnlyList<X509Certificate2> intermediateCertificates,
        IReadOnlyList<X509Certificate2> trustAnchors,
        TimeProvider timeProvider) =>
        new(certificate, intermediateCertificates, trustAnchors, timeProvider)
        {
            AcceptedVersions = new TlsVersionRange(commandLine.LowestTlsVersion, commandLine.HighestTlsVersion),
        };

    private static void DisposeAll(IEnumerable<IDisposable> disposables)
    {
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }
    }
}
