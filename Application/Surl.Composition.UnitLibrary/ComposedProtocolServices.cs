using Surl.Authentication;
using Surl.Content;
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

    /// <summary>Gets the one policy every login is judged by.</summary>
    public AuthenticationPolicy AuthenticationPolicy { get; } = authenticationPolicy;

    /// <summary>Gets the one clock.</summary>
    public TimeProvider TimeProvider { get; } = timeProvider;

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
