using System.Security.Cryptography.X509Certificates;
using System.Text;
using Surl.Authentication;
using Surl.Dtls;
using Surl.Extended.Protocol.Coap;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The <c>coaps</c> server, built when its first flow arrives: the registration builds servers before
/// server TLS has made the certificate, and <see cref="SecureCoapProtocolServer"/> takes its
/// credentials when it is constructed (BL-476). The credentials are the served certificate (when
/// <c>--cert</c> or <c>--self-signed</c> is given), the live account book as pre-shared keys (always offered and read per
/// handshake, so a handshake while no account admits <c>coaps</c> ends as an unknown identity does, ADR-0170), and the
/// <c>--cacert</c> anchors (ADR-0087 decision 3, ADR-0097 decision 6).
/// </summary>
internal sealed class DeferredCoapsProtocolServer(ComposedProtocolServices services) : IDatagramProtocolServer
{
    private const string Scheme = "coaps";

    private readonly Lazy<SecureCoapProtocolServer> server = new(() => Build(services));

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly([Scheme]);

    /// <inheritdoc/>
    public Task ServeAsync(IDatagramFlow flow, ExchangeContext context) => server.Value.ServeAsync(flow, context);

    private static SecureCoapProtocolServer Build(ComposedProtocolServices services)
    {
        var source = services.QuicCertificate;
        var book = services.AuthenticationPolicy.Settings.Accounts;
        var certificate = source.Certificate is { } served ? CoapsDtlsCertificate.From(served) : null;
        // The lookup is given even while no account exists: this runs once, and an account added later must find it (ADR-0170 amendment 1).
        var credentials = new CoapDtlsCredentials(
            certificate,
            LookUpKey(book),
            source.ClientTrustAnchors.Count == 0 ? null : new X509Certificate2Collection(source.ClientTrustAnchors.ToArray()));
        return new SecureCoapProtocolServer(
            services.ContentStore, ExtendedProtocolServers.CoapAccessRulesFor(services), services.CoapObservations, credentials);
    }

    // Asked for every handshake, so an account added, removed, reset or restored changes who may log in (ADR-0170 decision 5).
    internal static DtlsPreSharedKeyLookup LookUpKey(AccountBook book) =>
        identity => book.TryFindPreSharedKey(Encoding.UTF8.GetString(identity), Scheme, out var key) ? key : null;
}
