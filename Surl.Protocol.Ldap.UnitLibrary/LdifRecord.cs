namespace Surl.Protocol.Ldap;

/// <summary>
/// One content record of an LDIF file, read into a directory entry.
/// </summary>
/// <param name="Line">The one-based number of the physical line its <c>dn</c> starts on.</param>
/// <param name="Entry">The entry the record describes.</param>
internal sealed record LdifRecord(int Line, LdapEntry Entry);
