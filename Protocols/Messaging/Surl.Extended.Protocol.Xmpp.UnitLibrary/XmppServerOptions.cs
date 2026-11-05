using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// What the composition tells the XMPP server beyond its exchange limits (ADR-0114 decisions 0,
/// 1 and 2).
/// </summary>
public sealed record XmppServerOptions
{
    /// <summary>
    /// The options when nothing is given: no mail domains, the cryptographic random source, no
    /// certificate, plain-text logins refused, and the policy that lets everyone in.
    /// </summary>
    public static XmppServerOptions Default { get; } = new();

    /// <summary>
    /// Every <c>--mail-domain</c>, served beside the listen URL's host (ADR-0114 decision 1).
    /// </summary>
    public IReadOnlyList<string> MailDomains { get; init; } = [];

    /// <summary>Where stream IDs come from; <see cref="CryptographicXmppRandomSource.Instance"/> unless given.</summary>
    public IXmppRandomSource RandomSource { get; init; } = CryptographicXmppRandomSource.Instance;

    /// <summary>
    /// Whether a certificate is configured (<c>--cert</c> or <c>--self-signed</c>), so an
    /// <c>xmpp</c> stream offers STARTTLS and answers <c>&lt;starttls/&gt;</c> with
    /// <c>&lt;proceed/&gt;</c> (ADR-0114 decision 2). <see langword="false"/> unless given.
    /// </summary>
    public bool IsTlsUpgradeAvailable { get; init; }

    /// <summary>
    /// Whether <c>--allow-plaintext-auth</c> was given, so STARTTLS is offered without
    /// <c>&lt;required/&gt;</c> beside the plain-text SASL mechanisms (ADR-0114 decision 2).
    /// <see langword="false"/> unless given: with a certificate, STARTTLS is required.
    /// </summary>
    public bool AllowPlaintextAuthentication { get; init; }

    /// <summary>
    /// The policy whose SASL mechanisms the stream features offer (ADR-0114 decision 3);
    /// <see cref="AnonymousAuthenticationPolicy"/> unless given.
    /// </summary>
    public ISaslAuthenticationPolicy AuthenticationPolicy { get; init; } = new AnonymousAuthenticationPolicy();
}
