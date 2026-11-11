using System.Security.Cryptography.X509Certificates;
using Surl.Cli;
using Surl.Networking;
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
        IReadOnlyList<X509Certificate2>? clientTrustAnchors = null)
    {
        ClientTrustAnchors = clientTrustAnchors ?? [];
        Settings = settings;
        QuicCertificate = quicCertificate;
        ThrowawayCertificateFingerprint = throwawayCertificateFingerprint;
        this.owned = owned;
    }

    /// <summary>
    /// The listen URL schemes whose server can upgrade a plaintext connection to TLS in the
    /// middle of it (SMTP's <c>STARTTLS</c>, ADR-0053 decision 5; IMAP's <c>STARTTLS</c>, ADR-0055
    /// decision 11; POP3's <c>STLS</c>, ADR-0056 decision 8; FTP's <c>AUTH TLS</c>, ADR-0052
    /// decision 5; LDAP's <c>StartTLS</c>, ADR-0072 decision 5), which it does only when a
    /// certificate is configured.
    /// </summary>
    private static readonly HashSet<string> UpgradableSchemes = new(StringComparer.OrdinalIgnoreCase) { "ftp", "imap", "ldap", "pop3", "smtp" };

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
    /// Finds the first listen URL, in command-line order, that is TLS from the first byte or
    /// requires TLS before a login (<c>natss</c>, ADR-0113 decision 6), and has no certificate to serve because neither <c>--cert</c> nor <c>--self-signed</c> was
    /// given (ADR-0032, section 10).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns>That listen URL, or <see langword="null"/> when every listen URL has what it needs.</returns>
    public static ListenUrl? FindListenUrlWithoutCertificate(SurlCommandLine commandLine) =>
        commandLine.CertificateFile is null && !commandLine.SelfSigned
            ? commandLine.ListenUrls.FirstOrDefault(listenUrl => RequiresCertificate(commandLine, listenUrl.Scheme))
            : null;

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
    public static ServerTlsComposition Compose(SurlCommandLine commandLine, TimeProvider timeProvider, Func<string, byte[]>? readFile = null)
    {
        if (!commandLine.ListenUrls.Any(listenUrl => NeedsTls(commandLine, listenUrl.Scheme)))
        {
            return new ServerTlsComposition(null, null, null, []);
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

    private static bool NeedsTls(SurlCommandLine commandLine, string scheme) =>
        RequiresCertificate(commandLine, scheme)
        || (IsCertificateConfigured(commandLine) && (UpgradableSchemes.Contains(scheme) || scheme == CoapsScheme));

    // coaps needs a certificate or an account, whose pre-shared key it can handshake with (ADR-0087 decision 3).
    private static bool RequiresCertificate(SurlCommandLine commandLine, string scheme) =>
        TlsSchemes.IsImplicitTls(scheme)
        || UpgradeRequiredSchemes.Contains(scheme)
        || (scheme == CoapsScheme && commandLine.Accounts.Count == 0 && commandLine.UserFile is null);

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
        (commandLine.Http3 || commandLine.ListenUrls.Any(listenUrl => listenUrl.Scheme is WebTransportScheme or CoapsScheme)) ? QuicServerCertificate.From(certificate, intermediateCertificates) : null;

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
