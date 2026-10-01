namespace Surl.Kerberos;

/// <summary>
/// One key a keytab holds for a principal (ADR-0057 decision 1), in one of the encryption types
/// surl accepts.
/// </summary>
/// <param name="Principal">The principal the key belongs to, such as <c>HTTP/www.example.com@EXAMPLE.COM</c>.</param>
/// <param name="KeyVersionNumber">The key version number: the trailing 32-bit one when present and not 0, else the 8-bit one.</param>
/// <param name="EncryptionType">The key's encryption type.</param>
/// <param name="Key">The key, as long as its encryption type's keys are.</param>
public sealed record KerberosKeytabEntry(KerberosPrincipalName Principal, uint KeyVersionNumber, KerberosEncryptionType EncryptionType, ReadOnlyMemory<byte> Key);
