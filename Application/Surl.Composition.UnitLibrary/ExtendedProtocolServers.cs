using Surl.Extended.Protocol.Coap;
using Surl.Extended.Protocol.DnsQuery;
using Surl.Extended.Protocol.Nats;
using Surl.Extended.Protocol.S3;
using Surl.Extended.Protocol.SocketIo;
using Surl.Extended.Protocol.Stomp;
using Surl.Extended.Protocol.WebTransport;
using Surl.Extended.Protocol.Xmpp;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The one place <c>surl</c> composes its extended protocol servers, the
/// <c>Surl.Extended.Protocol.*</c> servers for schemes the Curl port adds and upstream curl does
/// not have, and the one switch that turns them all off (ADR-0083, decisions 6 and 7).
/// </summary>
/// <remarks>
/// An extended server joins with one entry in <see cref="Registrations"/>; its schemes' default ports
/// are already rows of <c>Surl.Cli</c>'s <c>ExtendedSchemeDefaultPorts</c>. The original servers
/// are <see cref="ProtocolServerRegistrations.Original"/> and are never touched by this class.
/// </remarks>
public static class ExtendedProtocolServers
{
    /// <summary>The environment variable whose value <see cref="TurnedOffValue"/> turns every extended server off.</summary>
    public const string SwitchVariable = "SURL_EXTENSIONS";

    /// <summary>The value of <see cref="SwitchVariable"/> that turns every extended server off.</summary>
    public const string TurnedOffValue = "0";

    /// <summary>
    /// Tells whether the extended servers are on: <see cref="SwitchVariable"/> unset, empty or
    /// any value but <see cref="TurnedOffValue"/> leaves them on, mirroring the Curl port's
    /// <c>CURL_EXTENSIONS=0</c>.
    /// </summary>
    /// <param name="environmentVariables">Reads the process's environment.</param>
    /// <returns><see langword="true"/> unless <c>SURL_EXTENSIONS=0</c>.</returns>
    public static bool AreTurnedOn(IEnvironmentVariableReader environmentVariables) =>
        environmentVariables.Read(SwitchVariable) != TurnedOffValue;

    /// <summary>
    /// Gets every extended protocol server's entry this build has, in ordinal order of its first
    /// scheme, on the same <see cref="ProtocolServerRegistration"/> as the originals (ADR-0124,
    /// decision 2): today the DNS server for <c>dns</c> over UDP and TCP (BL-1032), the NATS server for
    /// <c>nats</c> and <c>natss</c> (BL-528), the S3 server
    /// for <c>s3</c> (BL-421) the Socket.IO server for <c>socketio</c> and <c>socketios</c> (BL-455) the WebTransport server for <c>webtransport</c> (BL-461) the CoAP servers for <c>coap</c> and <c>coaps</c> (BL-476) the STOMP broker for <c>stomp</c> and <c>stomps</c> (BL-533) and the XMPP server for <c>xmpp</c> and <c>xmpps</c> (BL-540).
    /// </summary>
    /// <remarks>
    /// The DNS server is two entries for <c>dns</c>, a <see cref="DnsUdpProtocolServer"/> and a
    /// <see cref="DnsTcpProtocolServer"/>, so the serving engine binds UDP and TCP on the one port of
    /// each <c>dns</c> listen URL (ADR-0092 decisions 3 and 4); both answer from the one
    /// <see cref="ComposedProtocolServices.DnsZones"/> and answer AXFR and IXFR only with
    /// <c>--allow-zone-transfer</c>.
    /// The NATS server answers <c>nats</c> and <c>natss</c> over the one message bus the MQTT server
    /// shares (ADR-0100 decision 3.2), judging logins by the policy over the accounts allowed on
    /// <c>nats</c>, with <c>--idle-timeout</c> and <c>--allow-anonymous</c> (ADR-0113 decisions 2 and 5).
    /// <c>natss</c> starts in plain text and requires TLS before CONNECT (ADR-0113 decision 6), so it is
    /// no implicit-TLS scheme; <see cref="ServerTlsComposition"/> requires a certificate for it.
    /// The S3 server answers <c>s3</c> over the one content store, its object metadata index and the
    /// policy over the accounts allowed on <c>s3</c> (ADR-0108; ADR-0102 decision 4.3); <c>s3</c> is TLS from the first byte (ADR-0108, decision 5), which
    /// the serving engine reads from <see cref="TlsSchemes"/>, so it needs no second registration.
    /// The Socket.IO server answers <c>socketio</c> and <c>socketios</c> as one server, so its
    /// sessions and <c>broadcast</c> span every Socket.IO listen URL of the run; it serves the main
    /// namespace <c>/</c> only, because a listen URL has no path to name another (ADR-0120 amendment 2),
    /// and judges CONNECT logins by the policy over the accounts allowed on <c>socketio</c>.
    /// <c>socketios</c> is TLS from the first byte through <see cref="TlsSchemes"/>.
    /// The WebTransport server answers <c>webtransport</c> over QUIC and HTTP/3 on UDP, accepting a session
    /// on every path (a listen URL has no path to name one), judging each <c>CONNECT</c> by the policy over the
    /// accounts allowed on <c>webtransport</c>. It reads the certificate from
    /// <see cref="ComposedProtocolServices.QuicCertificate"/>, which the TLS composition fills because a
    /// <c>webtransport</c> listen URL needs a certificate (<c>--cert</c> or <c>--self-signed</c>).
    /// The STOMP broker answers <c>stomp</c> over the one message bus the MQTT server shares, judging logins by the policy over the accounts allowed on <c>stomp</c>, with <c>--idle-timeout</c>; <c>stomps</c> is TLS from the first byte through <see cref="TlsSchemes"/> and an <c>ImplicitTls</c> entry.
    /// The XMPP server answers <c>xmpp</c> (STARTTLS offered when a certificate is configured, required unless
    /// <c>--allow-plaintext-auth</c>), serving the listen URL host and every <c>--mail-domain</c>, and <c>xmpps</c>, the same server
    /// over a connection TLS from the first byte (XEP-0368, ALPN <c>xmpp-client</c>), judging logins by the policy over the accounts allowed on <c>xmpp</c>.
    /// </remarks>
    public static IReadOnlyList<ProtocolServerRegistration> Registrations { get; } =
    [
        new(["coap"], services => new CoapProtocolServer(services.ContentStore, CoapAccessRulesFor(services), services.CoapObservations)),
        new(["coaps"], services => new DeferredCoapsProtocolServer(services)),
        new(["dns"], services => new DnsUdpProtocolServer(services.DnsZones, services.AllowsZoneTransfer)),
        new(["dns"], services => new DnsTcpProtocolServer(services.DnsZones, services.AllowsZoneTransfer)),
        new(["nats", NatsServer.TlsScheme], services => new NatsServer(
            services.AuthenticationPolicyFor("nats"),
            services.MessageBus,
            new NatsServerOptions
            {
                IdleTimeout = services.IdleTimeout,
                AllowsAnonymous = services.AuthenticationPolicy.Settings.AllowAnonymous,
            })),
        new(["s3"], services => new S3ProtocolServer(
            services.ContentStore, services.ObjectMetadataIndex, services.AuthenticationPolicyFor("s3"), services.TimeProvider)),
        new([SocketIoProtocolServer.PlainScheme, SocketIoProtocolServer.TlsScheme], services => new SocketIoProtocolServer(
            SocketIoServedNamespaces.FromListenPath(null), new SocketIoSessions(), services.AuthenticationPolicyFor(SocketIoProtocolServer.PlainScheme))),
        new(["stomp"], services => new StompServer(
            services.AuthenticationPolicyFor("stomp"),
            services.MessageBus,
            new StompServerOptions { IdleTimeout = services.IdleTimeout })),
        ProtocolServerRegistration.ImplicitTls("stomps", "stomp"),
        new([WebTransportProtocolServer.Scheme], services => new WebTransportProtocolServer(
            services.AuthenticationPolicyFor(WebTransportProtocolServer.Scheme),
            () => services.QuicCertificate.Certificate is { } certificate ? new WebTransportServerCertificate(certificate.Chain, certificate.Key) : null,
            services.IdleTimeout)),
        new(["xmpp"], services => new XmppServer(new XmppServerOptions
        {
            MailDomains = services.MailDomains,
            IsTlsUpgradeAvailable = services.IsTlsUpgradeAvailable,
            AllowPlaintextAuthentication = services.AuthenticationPolicy.Settings.AllowPlaintextAuthentication,
            AuthenticationPolicy = services.AuthenticationPolicyFor("xmpp"),
        })),
        ProtocolServerRegistration.ImplicitTls("xmpps", "xmpp"),
    ];

    /// <summary>
    /// Who may read and write over <c>coap</c> and <c>coaps</c> (ADR-0097 decision 6): <c>--allow-anonymous</c>, and whether any account is allowed on <c>coap</c>.
    /// </summary>
    internal static CoapAccessRules CoapAccessRulesFor(ComposedProtocolServices services) => new(
        services.AuthenticationPolicy.Settings.AllowAnonymous, services.AuthenticationPolicyFor("coap").Settings.Accounts.HasAccounts);

    /// <summary>
    /// Builds every extended protocol server <see cref="Registrations"/> lists, in its order.
    /// </summary>
    /// <param name="services">What the servers are built from.</param>
    /// <returns>The servers.</returns>
    public static IReadOnlyList<IProtocolServer> Compose(ExtendedProtocolServerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return SurlServerComposition.ComposeProtocolServers(Registrations, services.ComposedServices);
    }
}
