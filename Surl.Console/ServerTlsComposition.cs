using System.Security.Cryptography.X509Certificates;
using Surl.Cli;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// The process's server-side TLS settings as the command line asks for them (ADR-0010,
/// section 3; ADR-0020; ADR-0032, section 10): none when no listen URL is TLS from the first
/// byte and none can be upgraded with a certificate given; otherwise the <c>--cert</c> certificate,
/// or a throwaway one with <c>--self-signed</c>, with the <c>--cacert</c> trust anchors and the
/// accepted TLS versions. It owns every certificate it loaded or made,
/// and disposing it disposes them.
/// </summary>
internal sealed class ServerTlsComposition : IDisposable
{
    private readonly IReadOnlyList<IDisposable> owned;

    private ServerTlsComposition(ServerTlsSettings? settings, string? throwawayCertificateFingerprint, IReadOnlyList<IDisposable> owned)
    {
        Settings = settings;
        ThrowawayCertificateFingerprint = throwawayCertificateFingerprint;
        this.owned = owned;
    }

    /// <summary>
    /// The listen URL schemes whose server can upgrade a plaintext connection to TLS in the
    /// middle of it (SMTP's <c>STARTTLS</c>, ADR-0053 decision 5; IMAP's <c>STARTTLS</c>, ADR-0055
    /// decision 11; FTP's <c>AUTH TLS</c>, ADR-0052 decision 5), which it does only when a
    /// certificate is configured.
    /// </summary>
    private static readonly HashSet<string> UpgradableSchemes = new(StringComparer.OrdinalIgnoreCase) { "ftp", "imap", "smtp" };

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
    /// Finds the first listen URL, in command-line order, that is TLS from the first byte and
    /// has no certificate to serve because neither <c>--cert</c> nor <c>--self-signed</c> was
    /// given (ADR-0032, section 10).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns>That listen URL, or <see langword="null"/> when every listen URL has what it needs.</returns>
    public static ListenUrl? FindListenUrlWithoutCertificate(SurlCommandLine commandLine) =>
        commandLine.CertificateFile is null && !commandLine.SelfSigned
            ? commandLine.ListenUrls.FirstOrDefault(listenUrl => TlsSchemes.IsImplicitTls(listenUrl.Scheme))
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
    /// <returns>The composition, which the caller disposes once serving has ended.</returns>
    /// <exception cref="TlsFileLoadException">A <c>--cert</c>, <c>--key</c> or <c>--cacert</c> file cannot be loaded.</exception>
    public static ServerTlsComposition Compose(SurlCommandLine commandLine, TimeProvider timeProvider)
    {
        if (!commandLine.ListenUrls.Any(listenUrl => NeedsTls(commandLine, listenUrl.Scheme)))
        {
            return new ServerTlsComposition(null, null, []);
        }

        var trustAnchors = commandLine.CaCertificateFile is { } caCertificateFile
            ? ClientTrustAnchorFileLoader.Load(caCertificateFile)
            : [];

        try
        {
            return commandLine.CertificateFile is { } certificateFile
                ? ComposeFromFiles(commandLine, certificateFile, trustAnchors, timeProvider)
                : ComposeWithThrowaway(commandLine, trustAnchors, timeProvider);
        }
        catch (TlsFileLoadException)
        {
            DisposeAll(trustAnchors);
            throw;
        }
    }

    private static bool NeedsTls(SurlCommandLine commandLine, string scheme) =>
        TlsSchemes.IsImplicitTls(scheme) || (IsCertificateConfigured(commandLine) && UpgradableSchemes.Contains(scheme));

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
        TimeProvider timeProvider)
    {
        var loaded = ServerCertificateFileLoader.Load(
            certificateFile,
            MapCertificateFormat(commandLine.CertificateType),
            commandLine.KeyFile,
            MapKeyFormat(commandLine.KeyType),
            commandLine.KeyPassphrase);
        var settings = CreateSettings(commandLine, loaded.Certificate, loaded.IntermediateCertificates, trustAnchors, timeProvider);

        return new ServerTlsComposition(settings, null, [loaded, .. trustAnchors]);
    }

    private static ServerTlsComposition ComposeWithThrowaway(
        SurlCommandLine commandLine, IReadOnlyList<X509Certificate2> trustAnchors, TimeProvider timeProvider)
    {
        var throwaway = ThrowawayServerCertificate.Create(
            timeProvider, commandLine.ListenUrls.Select(listenUrl => listenUrl.Host));
        var settings = CreateSettings(commandLine, throwaway, [], trustAnchors, timeProvider);

        return new ServerTlsComposition(
            settings, ThrowawayServerCertificate.Sha256FingerprintOf(throwaway), [throwaway, .. trustAnchors]);
    }

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
