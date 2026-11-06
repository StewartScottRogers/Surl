using System.Text;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// The JIDs a login and a bind give a stream (ADR-0114 decisions 3 and 4; RFC 7622 section 3):
/// the bare JID an account logs in as, and whether a resource a client asks for is usable.
/// </summary>
internal static class XmppJids
{
    /// <summary>The most bytes a localpart or a resourcepart may hold (RFC 7622 sections 3.3 and 3.4).</summary>
    public const int MaxPartBytes = 1023;

    /// <summary>How many random hexadecimal digits follow <see cref="AnonymousPrefix"/> and <see cref="GeneratedResourcePrefix"/>.</summary>
    public const int RandomHexDigits = 16;

    /// <summary>What an anonymous login's localpart starts with (decision 3).</summary>
    public const string AnonymousPrefix = "anon-";

    /// <summary>What a resource the server generates starts with (decision 4).</summary>
    public const string GeneratedResourcePrefix = "surl-";

    private const string LocalpartForbidden = "\"&'/:<>@";

    /// <summary>
    /// The bare JID a login gives the stream: <c>&lt;name&gt;@&lt;domain&gt;</c> for an account name
    /// without <c>@</c>; the account name itself when it is <c>&lt;local&gt;@&lt;domain&gt;</c> on the
    /// stream's domain; a new <c>anon-</c> name when no account was named (a login accepted unchecked).
    /// </summary>
    /// <param name="accountName">The account the login was accepted as, or <see langword="null"/> when accepted unchecked.</param>
    /// <param name="domain">The stream's domain.</param>
    /// <param name="randomSource">Where an anonymous name's digits come from.</param>
    /// <returns>The bare JID, or <see langword="null"/> when the account cannot log in over XMPP.</returns>
    public static string? BareJid(string? accountName, string domain, IXmppRandomSource randomSource)
    {
        if (accountName is null)
        {
            return $"{AnonymousPrefix}{randomSource.NextHexDigits(RandomHexDigits)}@{domain}";
        }

        var at = accountName.IndexOf('@', StringComparison.Ordinal);
        var localpart = at < 0 ? accountName : accountName[..at];
        var isOnStreamDomain = at < 0 || XmppDomains.Normalized(accountName[(at + 1)..]) == domain;

        return isOnStreamDomain && IsValidLocalpart(localpart) ? $"{localpart}@{domain}" : null;
    }

    /// <summary>
    /// Whether a resource a client asked for is a resourcepart (RFC 7622 section 3.4): 1 to
    /// <see cref="MaxPartBytes"/> bytes and no control character.
    /// </summary>
    /// <param name="resource">The resource as the client sent it.</param>
    /// <returns><see langword="true"/> when it may be bound.</returns>
    public static bool IsValidResourcepart(string resource) =>
        HasPartLength(resource) && !resource.Any(char.IsControl);

    /// <summary>
    /// Whether a domainpart, as <see cref="XmppDomains.Normalized"/> writes it, may be addressed
    /// (RFC 7622 section 3.2): 1 to <see cref="MaxPartBytes"/> bytes, and no whitespace, control
    /// character or <c>@</c>.
    /// </summary>
    /// <param name="domain">The normalized domainpart.</param>
    /// <returns><see langword="true"/> when it is a domainpart.</returns>
    public static bool IsValidDomainpart(string domain) =>
        HasPartLength(domain) && !domain.Any(character => char.IsWhiteSpace(character) || char.IsControl(character) || character == '@');

    /// <summary>
    /// Whether a localpart may be addressed (decision 3; RFC 7622 section 3.3): 1 to
    /// <see cref="MaxPartBytes"/> bytes and none of <c>"&amp;'/:&lt;&gt;@</c> or whitespace.
    /// </summary>
    /// <param name="localpart">The localpart.</param>
    /// <returns><see langword="true"/> when it is a localpart.</returns>
    public static bool IsValidLocalpart(string localpart) =>
        HasPartLength(localpart) && !localpart.Any(character => char.IsWhiteSpace(character) || LocalpartForbidden.Contains(character));

    private static bool HasPartLength(string part) =>
        part.Length > 0 && Encoding.UTF8.GetByteCount(part) <= MaxPartBytes;
}
