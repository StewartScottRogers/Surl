using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// What the composition tells the XMPP server beyond its exchange limits (ADR-0114 decisions 0,
/// 1 and 2).
/// </summary>
public sealed record XmppServerOptions
{
    /// <summary>
    /// The options when nothing is given: no mail domains, the cryptographic random source, no TLS
    /// upgrade available, <see cref="AllowPlaintextAuthentication"/> and <see cref="OffersAnonymous"/>
    /// off, and the policy that lets everyone in.
    /// </summary>
    public static XmppServerOptions Default { get; } = new();

    /// <summary>
    /// Every <c>--mail-domain</c>, served beside the listen URL's host (ADR-0114 decision 1).
    /// </summary>
    public IReadOnlyList<string> MailDomains { get; init; } = [];

    /// <summary>Where stream IDs, generated resources and anonymous names come from; <see cref="CryptographicXmppRandomSource.Instance"/> unless given.</summary>
    public IXmppRandomSource RandomSource { get; init; } = CryptographicXmppRandomSource.Instance;

    /// <summary>
    /// Whether a TLS upgrade is available - a certificate is configured (<c>--cert</c> or
    /// <c>--self-signed</c>) or TLS-SRP is served - so an <c>xmpp</c> stream not yet TLS offers
    /// STARTTLS and answers <c>&lt;starttls/&gt;</c> with <c>&lt;proceed/&gt;</c> (ADR-0114 decision 2;
    /// ADR-0122 decision 5). <see langword="false"/> unless given.
    /// </summary>
    public bool IsTlsUpgradeAvailable { get; init; }

    /// <summary>
    /// Whether <c>--allow-plaintext-auth</c> was given, so STARTTLS is offered without
    /// <c>&lt;required/&gt;</c> beside the plain-text SASL mechanisms (ADR-0114 decision 2).
    /// <see langword="false"/> unless given: when <see cref="IsTlsUpgradeAvailable"/>, STARTTLS is then
    /// required before TLS.
    /// </summary>
    public bool AllowPlaintextAuthentication { get; init; }

    /// <summary>
    /// Whether <c>--allow-anonymous</c> was given, so the stream features end with the SASL
    /// <c>ANONYMOUS</c> mechanism (RFC 4505) and <c>&lt;auth mechanism='ANONYMOUS'/&gt;</c> logs in as a new
    /// <c>anon-</c> JID (ADR-0114 decision 3). <see langword="false"/> unless given.
    /// </summary>
    public bool OffersAnonymous { get; init; }

    /// <summary>
    /// The policy whose SASL mechanisms the stream features offer and whose exchanges run every
    /// login (ADR-0114 decision 3);
    /// <see cref="AnonymousAuthenticationPolicy"/> unless given.
    /// </summary>
    public ISaslAuthenticationPolicy AuthenticationPolicy { get; init; } = new AnonymousAuthenticationPolicy();
}
