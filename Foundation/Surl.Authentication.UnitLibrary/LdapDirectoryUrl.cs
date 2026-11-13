using System.Globalization;

namespace Surl.Authentication;

/// <summary>
/// An RFC 4516 LDAP URL as <c>--user-ldap</c> names a directory (ADR-0149, decision 3.1):
/// <c>ldap[s]://host[:port]/base-dn[?attribute[?scope[?filter]]]</c>, with <c>uid</c>, <c>sub</c> and
/// <c>(objectClass=*)</c> for what is left out. Built only by <see cref="TryParse"/>.
/// </summary>
public sealed class LdapDirectoryUrl
{
    /// <summary>The attribute the user name is matched on when the URL names none.</summary>
    public const string DefaultAttribute = "uid";

    /// <summary>The filter used when the URL names none.</summary>
    public const string DefaultFilter = "(objectClass=*)";

    private LdapDirectoryUrl(
        bool isTls, string host, int port, string baseDn, string attribute, LdapSearchScope scope, byte[] filter)
    {
        UsesTlsFromTheStart = isTls;
        Host = host;
        Port = port;
        BaseDn = baseDn;
        Attribute = attribute;
        Scope = scope;
        EncodedFilter = filter;
    }

    /// <summary>Whether the scheme is <c>ldaps</c>: TLS from the first byte.</summary>
    public bool UsesTlsFromTheStart { get; }

    /// <summary>The host, without brackets for an IPv6 literal.</summary>
    public string Host { get; }

    /// <summary>The port: the URL's, else 636 for <c>ldaps</c> and 389 for <c>ldap</c>.</summary>
    public int Port { get; }

    /// <summary>The base DN, percent-decoded.</summary>
    public string BaseDn { get; }

    /// <summary>The one attribute the user name is matched on.</summary>
    public string Attribute { get; }

    /// <summary>The search scope.</summary>
    public LdapSearchScope Scope { get; }

    internal byte[] EncodedFilter { get; }

    /// <summary>
    /// Reads <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The URL as written.</param>
    /// <param name="url">The URL, when it is well formed.</param>
    /// <param name="failure">Why it is not, when it is not.</param>
    /// <returns>Whether <paramref name="text"/> is a URL this class accepts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static bool TryParse(string text, out LdapDirectoryUrl? url, out LdapDirectoryUrlFailure failure)
    {
        ArgumentNullException.ThrowIfNull(text);
        url = null;
        var isTls = text.StartsWith("ldaps://", StringComparison.OrdinalIgnoreCase);
        if (!isTls && !text.StartsWith("ldap://", StringComparison.OrdinalIgnoreCase))
        {
            failure = LdapDirectoryUrlFailure.NotAnLdapUrl;
            return false;
        }

        var rest = text[(isTls ? 8 : 7)..];
        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        var authority = slash < 0 ? rest : rest[..slash];
        var tail = slash < 0 ? string.Empty : rest[(slash + 1)..];
        if (!TryReadAuthority(authority, isTls ? 636 : 389, out var host, out var port, out failure))
        {
            return false;
        }

        if (!TryReadPath(tail, out var baseDn, out var attribute, out var scope, out var filter, out failure))
        {
            return false;
        }

        url = new LdapDirectoryUrl(isTls, host, port, baseDn, attribute, scope, filter);
        return true;
    }

    private static bool TryReadPath(
        string tail, out string baseDn, out string attribute, out LdapSearchScope scope, out byte[] filter, out LdapDirectoryUrlFailure failure)
    {
        var fields = tail.Split('?');
        baseDn = Uri.UnescapeDataString(fields[0]);
        attribute = DefaultAttribute;
        scope = LdapSearchScope.Subtree;
        filter = [];
        failure = LdapDirectoryUrlFailure.BadBaseDn;
        if (!LdapDistinguishedName.IsWellFormed(baseDn))
        {
            return false;
        }

        failure = LdapDirectoryUrlFailure.HasExtensions;
        return !fields.Skip(4).Any(extension => extension.Length > 0)
            && TryReadSearch(fields, out attribute, out scope, out filter, out failure);
    }

    private static bool TryReadAuthority(string authority, int defaultPort, out string host, out int port, out LdapDirectoryUrlFailure failure)
    {
        host = string.Empty;
        port = defaultPort;
        failure = LdapDirectoryUrlFailure.HasUserInformation;
        if (authority.Contains('@', StringComparison.Ordinal))
        {
            return false;
        }

        // A colon inside an IPv6 literal's brackets is no port separator.
        var portStart = authority.LastIndexOf(':');
        if (portStart < authority.IndexOf(']', StringComparison.Ordinal))
        {
            portStart = -1;
        }

        var hostText = portStart >= 0 ? authority[..portStart] : authority;
        host = hostText.Trim('[', ']');
        failure = LdapDirectoryUrlFailure.NoHost;
        if (host.Length == 0)
        {
            return false;
        }

        failure = LdapDirectoryUrlFailure.BadPort;
        if (portStart >= 0 && !(int.TryParse(authority[(portStart + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out port) && port is >= 1 and <= 65535))
        {
            return false;
        }

        return true;
    }

    private static string FieldAt(string[] fields, int index) => index < fields.Length ? fields[index] : string.Empty;

    private static bool TryReadSearch(
        string[] fields, out string attribute, out LdapSearchScope scope, out byte[] filter, out LdapDirectoryUrlFailure failure)
    {
        scope = LdapSearchScope.Subtree;
        filter = [];
        if (!TryReadAttribute(Uri.UnescapeDataString(FieldAt(fields, 1)), out attribute, out failure))
        {
            return false;
        }

        failure = LdapDirectoryUrlFailure.BadScope;
        if (!TryReadScope(FieldAt(fields, 2), out scope))
        {
            return false;
        }

        failure = LdapDirectoryUrlFailure.BadFilter;
        var filterText = Uri.UnescapeDataString(FieldAt(fields, 3));
        var encoded = LdapFilterEncoder.TryEncode(filterText.Length > 0 ? filterText : DefaultFilter);
        filter = encoded ?? [];

        return encoded is not null;
    }

    private static bool TryReadAttribute(string text, out string attribute, out LdapDirectoryUrlFailure failure)
    {
        attribute = text.Length > 0 ? text : DefaultAttribute;
        failure = LdapDirectoryUrlFailure.MultipleAttributes;
        if (text.Contains(',', StringComparison.Ordinal))
        {
            return false;
        }

        failure = LdapDirectoryUrlFailure.BadAttribute;
        return text.Length == 0 || LdapFilterEncoder.IsAttributeDescription(text);
    }

    private static bool TryReadScope(string text, out LdapSearchScope scope)
    {
        scope = LdapSearchScope.Subtree;
        if (text.Length == 0 || text.Equals("sub", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (text.Equals("base", StringComparison.OrdinalIgnoreCase))
        {
            scope = LdapSearchScope.Base;
            return true;
        }

        scope = LdapSearchScope.OneLevel;
        return text.Equals("one", StringComparison.OrdinalIgnoreCase);
    }
}
