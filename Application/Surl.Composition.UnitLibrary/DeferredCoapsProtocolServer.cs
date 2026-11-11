using System.Security.Cryptography.X509Certificates;
using System.Text;
using Surl.Dtls;
using Surl.Extended.Protocol.Coap;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The <c>coaps</c> server, built when its first flow arrives: the registration builds servers before
/// server TLS has made the certificate, and <see cref="SecureCoapProtocolServer"/> takes its
/// credentials when it is constructed (BL-476). The credentials are the served certificate (when
/// <c>--cert</c> or <c>--self-signed</c> is given), the accounts as pre-shared keys, and the
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
        var accounts = services.PreSharedKeyAccounts.Where(account => account.MayLogInOn(Scheme)).ToArray();
        var credentials = new CoapDtlsCredentials(
            source.Certificate is { } certificate ? CoapsDtlsCertificate.From(certificate) : null,
            accounts.Length == 0 ? null : LookUpKey(accounts),
            source.ClientTrustAnchors.Count == 0 ? null : new X509Certificate2Collection(source.ClientTrustAnchors.ToArray()));
        return new SecureCoapProtocolServer(
            services.ContentStore, ExtendedProtocolServers.CoapAccessRulesFor(services), services.CoapObservations, credentials);
    }

    internal static DtlsPreSharedKeyLookup LookUpKey(IReadOnlyList<Surl.Authentication.Account> accounts) =>
        identity =>
        {
            var name = Encoding.UTF8.GetString(identity);
            var account = accounts.FirstOrDefault(candidate => string.Equals(candidate.UserName, name, StringComparison.Ordinal));
            return account is null ? null : Encoding.UTF8.GetBytes(account.Password);
        };
}
