namespace Surl.Protocol.Ldap;

/// <summary>
/// The <c>substrings</c> choice (RFC 4511, section 4.5.1.7.2): at most one <c>initial</c>, first;
/// any number of <c>any</c>; at most one <c>final</c>, last; at least one of them.
/// </summary>
/// <param name="AttributeDescription">The attribute matched.</param>
/// <param name="Initial">The <c>initial</c> substring; <see langword="null"/> when absent.</param>
/// <param name="Any">The <c>any</c> substrings, in the order sent.</param>
/// <param name="Final">The <c>final</c> substring; <see langword="null"/> when absent.</param>
internal sealed record LdapSubstringsFilter(string AttributeDescription, byte[]? Initial, IReadOnlyList<byte[]> Any, byte[]? Final) : LdapFilter;
