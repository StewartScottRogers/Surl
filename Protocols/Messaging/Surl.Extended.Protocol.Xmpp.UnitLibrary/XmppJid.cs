namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// A JID a stanza names in <c>to</c> or <c>from</c> (RFC 7622 section 3): an optional localpart,
/// a domainpart in the form <see cref="XmppDomains.Normalized"/> writes it, and an optional
/// resourcepart.
/// </summary>
/// <param name="Localpart">The localpart; <see langword="null"/> for a JID that names a domain itself.</param>
/// <param name="Domain">The normalized domainpart.</param>
/// <param name="Resource">The resourcepart; <see langword="null"/> for a bare JID.</param>
internal sealed record XmppJid(string? Localpart, string Domain, string? Resource)
{
    /// <summary>The JID without its resourcepart.</summary>
    public string Bare => Localpart is null ? Domain : $"{Localpart}@{Domain}";

    /// <summary>
    /// Reads a JID: everything after the first <c>/</c> is the resourcepart, and everything before
    /// the first <c>@</c> ahead of it the localpart (RFC 7622 section 3.1).
    /// </summary>
    /// <param name="text">The JID as a client wrote it.</param>
    /// <returns>The JID, or <see langword="null"/> when a part is not valid (the stanza error <c>jid-malformed</c>).</returns>
    public static XmppJid? Parse(string text)
    {
        var slash = text.IndexOf('/', StringComparison.Ordinal);
        var bare = slash < 0 ? text : text[..slash];
        var resource = slash < 0 ? null : text[(slash + 1)..];
        var at = bare.IndexOf('@', StringComparison.Ordinal);
        var localpart = at < 0 ? null : bare[..at];
        var domain = XmppDomains.Normalized(bare[(at + 1)..]);

        return XmppJids.IsValidDomainpart(domain) &&
            (localpart is null || XmppJids.IsValidLocalpart(localpart)) &&
            (resource is null || XmppJids.IsValidResourcepart(resource))
            ? new XmppJid(localpart, domain, resource)
            : null;
    }

    /// <summary>
    /// Whether this JID's bare part is <paramref name="bareJid"/>: the localpart compared without
    /// case, as RFC 7622 section 3.3's case folding makes it, and the domain already lower-cased.
    /// </summary>
    /// <param name="bareJid">A bound stream's bare JID.</param>
    /// <returns><see langword="true"/> when both name the same account.</returns>
    public bool HasBare(string bareJid) => string.Equals(Bare, bareJid, StringComparison.OrdinalIgnoreCase);
}
