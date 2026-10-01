namespace Surl.Protocol.Ldap;

/// <summary>
/// The three values a filter takes against an entry (RFC 4511, section 4.5.1.7): only
/// <see cref="True"/> returns the entry.
/// </summary>
internal enum LdapFilterResult
{
    /// <summary>The filter does not match.</summary>
    False,

    /// <summary>The filter matches.</summary>
    True,

    /// <summary>The filter cannot be evaluated: a rule that does not apply, or a value it cannot read.</summary>
    Undefined,
}
