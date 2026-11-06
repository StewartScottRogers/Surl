namespace Surl.Kerberos;

/// <summary>
/// A keytab entry skipped because surl does not accept its encryption type (ADR-0057 decision 3),
/// such as an <c>rc4-hmac</c> (23) or DES key; the command line warns about each one.
/// </summary>
/// <param name="Principal">The principal the skipped key belongs to.</param>
/// <param name="EncryptionTypeNumber">The skipped key's enctype number, as the IANA registry numbers it.</param>
public sealed record KerberosKeytabSkippedEntry(KerberosPrincipalName Principal, int EncryptionTypeNumber);
