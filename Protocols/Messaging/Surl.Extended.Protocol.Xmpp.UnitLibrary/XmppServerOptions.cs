namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// What the composition tells the XMPP server beyond its exchange limits (ADR-0114 decisions 0
/// and 1).
/// </summary>
public sealed record XmppServerOptions
{
    /// <summary>The options when nothing is given: no mail domains and the cryptographic random source.</summary>
    public static XmppServerOptions Default { get; } = new();

    /// <summary>
    /// Every <c>--mail-domain</c>, served beside the listen URL's host (ADR-0114 decision 1).
    /// </summary>
    public IReadOnlyList<string> MailDomains { get; init; } = [];

    /// <summary>Where stream IDs come from; <see cref="CryptographicXmppRandomSource.Instance"/> unless given.</summary>
    public IXmppRandomSource RandomSource { get; init; } = CryptographicXmppRandomSource.Instance;
}
