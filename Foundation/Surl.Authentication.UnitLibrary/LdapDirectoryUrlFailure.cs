namespace Surl.Authentication;

/// <summary>
/// Why <see cref="LdapDirectoryUrl.TryParse"/> refused a URL (ADR-0149, decision 7); none of them
/// ever echoes the URL.
/// </summary>
public enum LdapDirectoryUrlFailure
{
    /// <summary>The URL is not <c>ldap://</c> or <c>ldaps://</c>.</summary>
    NotAnLdapUrl,

    /// <summary>The URL holds a user name or password.</summary>
    HasUserInformation,

    /// <summary>The URL names no host.</summary>
    NoHost,

    /// <summary>The port is not 1 to 65535.</summary>
    BadPort,

    /// <summary>The base DN is missing or not an RFC 4514 DN.</summary>
    BadBaseDn,

    /// <summary>The URL names more than one attribute.</summary>
    MultipleAttributes,

    /// <summary>The attribute is not an attribute description.</summary>
    BadAttribute,

    /// <summary>The scope is not <c>base</c>, <c>one</c> or <c>sub</c>.</summary>
    BadScope,

    /// <summary>The filter is not an RFC 4515 filter.</summary>
    BadFilter,

    /// <summary>The URL has an extension.</summary>
    HasExtensions,
}
