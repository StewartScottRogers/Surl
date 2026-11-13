using Surl.Authentication;
using Surl.Content;
using Surl.Core;
using Surl.Extended.Protocol.DnsQuery;
using Surl.Extended.Protocol.Kafka;
using Surl.MailStore;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Ldap;
using Surl.Protocol.Ssh;

namespace Surl.Composition;

/// <summary>
/// What a <see cref="ProtocolServerRegistration"/> builds its server from: the services composed
/// once per start and shared by every server (ADR-0124, decision 2), and the servers earlier
/// entries of a composition over them have built.
/// </summary>
/// <param name="contentStore">The one content store every protocol server reads.</param>
/// <param name="serviceState">The message bus, the mail store, the LDAP server and the object metadata index.</param>
/// <param name="authenticationPolicy">The one policy every login is judged by.</param>
/// <param name="timeProvider">The one clock.</param>
/// <param name="sshHostKeys">The SSH server's host keys.</param>
/// <param name="sshAlgorithms">The SSH algorithms offered.</param>
/// <param name="isTlsUpgradeAvailable">Whether a certificate is configured, so a TLS upgrade can be offered.</param>
/// <param name="echoesWebSocketMessages">Whether <c>--ws-echo</c> is given.</param>
/// <param name="advertisesHttp3">Whether <c>--http3</c> is given.</param>
/// <remarks>
/// Public because more than one host calls it, but not a supported programming interface: it may
/// change in any release (ADR-0099, decision 1).
/// </remarks>
public sealed class ComposedProtocolServices(
    ContentStore contentStore,
    ServiceState serviceState,
    AuthenticationPolicy authenticationPolicy,
    TimeProvider timeProvider,
    SshHostKeySet sshHostKeys,
    SshAlgorithmOffer sshAlgorithms,
    bool isTlsUpgradeAvailable,
    bool echoesWebSocketMessages,
    bool advertisesHttp3)
{
    private readonly Dictionary<string, IProtocolServer> composedServers = new(StringComparer.Ordinal);

    /// <summary>Gets the one content store every protocol server reads.</summary>
    public ContentStore ContentStore { get; } = contentStore;

    /// <summary>Gets the one mail store the mail servers share.</summary>
    public MailboxStore MailStore { get; } = serviceState.MailStore;

    /// <summary>Gets the one message bus the messaging servers share.</summary>
    public MessageBus.MessageBus MessageBus { get; } = serviceState.MessageBus;

    /// <summary>Gets the LDAP server over its loaded directory.</summary>
    public LdapProtocolServer LdapServer { get; } = serviceState.LdapServer;

    /// <summary>
    /// Gets the one object metadata index of the content store's served root, which the
    /// object-store servers (the S3 server) keep each object's metadata and ETag in (ADR-0100, decision 2.3).
    /// </summary>
    public ObjectMetadataIndex ObjectMetadataIndex { get; } = serviceState.ObjectMetadataIndex;

    /// <summary>Gets the zones the DNS server answers from (ADR-0092 decision 1).</summary>
    public DnsZoneSet DnsZones { get; } = serviceState.DnsZones;

    /// <summary>
    /// Gets a value indicating whether <c>--allow-zone-transfer</c> is given, so the DNS server
    /// answers AXFR and IXFR (ADR-0092 decision 4); off unless set.
    /// </summary>
    public bool AllowsZoneTransfer { get; init; }

    /// <summary>
    /// Gets the codec of the record batches the Kafka broker serves to a fetch, from
    /// <c>--kafka-fetch-compression</c> (ADR-0105 decision 4); none unless set.
    /// </summary>
    public KafkaCompressionCodec KafkaFetchCompression { get; init; }

    /// <summary>
    /// Gets the source of the certificate QUIC's TLS 1.3 serves, which the servers answering over QUIC
    /// read when a connection arrives and the composition fills once it has made the certificate.
    /// </summary>
    public QuicCertificateSource QuicCertificate { get; } = new();

    /// <summary>Gets the accounts <c>coaps</c> handshakes with as pre-shared keys: each identity is a user name and its key the password (ADR-0097 decision 6); none unless set.</summary>
    public IReadOnlyList<Account> PreSharedKeyAccounts { get; init; } = [];

    /// <summary>Gets the observers of the process, which <c>coap</c> and <c>coaps</c> share so a change notifies an observer over either (ADR-0097 decision 0).</summary>
    public Surl.Extended.Protocol.Coap.CoapObservations CoapObservations { get; } = new();

    /// <summary>Gets the one policy, over every account.</summary>
    public AuthenticationPolicy AuthenticationPolicy { get; } = authenticationPolicy;

    /// <summary>
    /// Gets the policy a server answering a scheme judges logins by, over the accounts allowed on it
    /// (ADR-0102, decision 4.3; <see cref="SchemeAuthenticationPolicies.For"/>); <see langword="null"/>,
    /// as when only schemes are wanted, for <see cref="AuthenticationPolicy"/> on every scheme. A
    /// server with a login is composed with <see cref="AuthenticationPolicyFor"/> its scheme.
    /// </summary>
    public Func<string, AuthenticationPolicy>? AuthenticationPolicyForScheme { get; init; }

    /// <summary>
    /// The policy a server answering <paramref name="scheme"/> judges logins by: its scheme's view
    /// of the accounts, or <see cref="AuthenticationPolicy"/> when no view is composed.
    /// </summary>
    /// <param name="scheme">The server's scheme, the plaintext one of a TLS twin.</param>
    /// <returns>The policy.</returns>
    public AuthenticationPolicy AuthenticationPolicyFor(string scheme) =>
        AuthenticationPolicyForScheme is { } policyFor ? policyFor(scheme) : AuthenticationPolicy;

    /// <summary>Gets the one clock.</summary>
    public TimeProvider TimeProvider { get; } = timeProvider;

    /// <summary>
    /// Gets the instance's one event stream, which the stores, the policy and the serving engine
    /// publish on (ADR-0124, decision 1); a publisher that publishes nothing unless set.
    /// </summary>
    public ISurlEventPublisher Events { get; init; } = DiscardingSurlEventPublisher.Instance;

    /// <summary>
    /// Gets the instance's one fault plan, which the stores, the policy and the serving engine
    /// consult (ADR-0124, decision 4); a plan with no rules unless set.
    /// </summary>
    public IFaultPlan Faults { get; init; } = EmptyFaultPlan.Instance;

    /// <summary>
    /// Gets <c>--idle-timeout</c> as the serving engine applies it (ADR-0006 section 1),
    /// <see cref="TimeSpan.Zero"/> when it is off: what the NATS server paces its own PINGs by
    /// (ADR-0113 decision 5); 120 seconds, the option's default, unless set.
    /// </summary>
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Gets the source of identifiers a server draws once, when it is created, named by the server (ADR-0124
    /// decision 6, BL-1001): from <c>--random-seed</c> and the name alone when a seed is given, so the Kafka cluster ID
    /// and the NATS <c>server_id</c> repeat every run; the cryptographic source unless set.
    /// </summary>
    public Func<string, IIdentifierRandom> IdentifiersForServer { get; init; } = _ => CryptographicIdentifierRandom.Instance;

    /// <summary>
    /// Gets every <c>--mail-domain</c>, which the XMPP server serves beside the listen URL host (ADR-0114 decision 1); none unless set.
    /// </summary>
    public IReadOnlyList<string> MailDomains { get; init; } = [];

    /// <summary>
    /// Gets how the proxies reach origins (ADR-0088 decision 2): the connector over the allowed-origin
    /// rule the start composed, which the SOCKS server shares; <see langword="null"/> when none is composed.
    /// </summary>
    public IOriginConnector? OriginConnector { get; init; }

    /// <summary>
    /// Gets the policy the HTTP proxy judges <c>Proxy-Authorization</c> by (ADR-0088 decision 5): over
    /// the proxy's own accounts, or one that asks for no login when it has none.
    /// </summary>
    public IAuthenticationPolicy ProxyAuthenticationPolicy { get; init; } = new AnonymousAuthenticationPolicy();

    /// <summary>Gets the SSH server's host keys.</summary>
    public SshHostKeySet SshHostKeys { get; } = sshHostKeys;

    /// <summary>Gets the SSH algorithms offered.</summary>
    public SshAlgorithmOffer SshAlgorithms { get; } = sshAlgorithms;

    /// <summary>Gets a value indicating whether a certificate is configured, so a TLS upgrade can be offered.</summary>
    public bool IsTlsUpgradeAvailable { get; } = isTlsUpgradeAvailable;

    /// <summary>Gets a value indicating whether <c>--ws-echo</c> is given.</summary>
    public bool EchoesWebSocketMessages { get; } = echoesWebSocketMessages;

    /// <summary>Gets a value indicating whether <c>--http3</c> is given, so the HTTP server adds <c>Alt-Svc</c> on https.</summary>
    public bool AdvertisesHttp3 { get; } = advertisesHttp3;

    /// <summary>
    /// Finds the server an earlier entry of a composition over these services built for
    /// <paramref name="scheme"/>, so a second registration (an implicit-TLS scheme) shares it.
    /// </summary>
    /// <param name="scheme">A scheme an earlier entry registered.</param>
    /// <returns>The server.</returns>
    /// <exception cref="InvalidOperationException">No earlier entry registered <paramref name="scheme"/>.</exception>
    public IProtocolServer ComposedServerFor(string scheme) =>
        composedServers.TryGetValue(scheme, out var server)
            ? server
            : throw new InvalidOperationException($"No protocol server registered before this entry answers {scheme}.");

    internal void RecordComposed(ProtocolServerRegistration registration, IProtocolServer server)
    {
        foreach (var scheme in registration.Schemes)
        {
            composedServers[scheme] = server;
        }
    }
}
